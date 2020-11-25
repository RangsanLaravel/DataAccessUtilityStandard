using DataAccessUtility.Security;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Reflection;

namespace DataAccessUtility
{
    public class RepositoryBaseManagedCore
    {
        private ConnectionClientDetail clientDetail;

        public RepositoryBaseManagedCore()
        {

        }

        public RepositoryBaseManagedCore(string ConnectionString, ConnectionClientDetail ClientDetail = null)
        {
            try
            {
                CreateConstructor(ConnectionString);
                clientDetail = ClientDetail;
            }
            catch (Exception ex)
            {
                throw ManageException(ex);
            }
        }

        public RepositoryBaseManagedCore(string ConnectionString, bool Pooling, ConnectionClientDetail ClientDetail = null)
        {
            try
            {
                CreateConstructor(ConnectionString, Pooling);
                clientDetail = ClientDetail;
            }
            catch (Exception ex)
            {
                throw ManageException(ex);
            }
        }

        public RepositoryBaseManagedCore(OracleConnection _connection)
        {
            try
            {
                ConnectionString = _connection.ConnectionString;
                connection = _connection;
            }
            catch (Exception ex)
            {
                throw ManageException(ex);
            }
        }

        private void CreateConstructor(string ConnectionString)
        {
            CreateConstructor(ConnectionString, null);
        }

        private void CreateConstructor(string ConnectionString, bool? Pooling)
        {
            if (ConnectionString != null)
            {
                string WebconfigConnectionString = ConnectionString;
                if (CheckNotNull(WebconfigConnectionString))
                {
                    #region "Decrypt [ConnectionString]"
                    if (WebconfigConnectionString.Contains("DATA SOURCE"))
                    {
                        ConnectionString = WebconfigConnectionString;
                    }
                    else
                    {
                        ConnectionString = SecurityHelper.Decrypt(WebconfigConnectionString);
                    }
                    #endregion "Decrypt [ConnectionString]"
                    #region "Setting [Pooling]"
                    if (Pooling != null)
                    {
                        if (Pooling.Value)
                        {
                            if (ConnectionString.Contains("pooling=false;"))
                            {
                                ConnectionString = ConnectionString.Replace("pooling=false;", "pooling=true;");
                            }
                            else
                            {
                                ConnectionString += "pooling=true;";
                            }
                        }
                        else
                        {
                            if (!ConnectionString.Contains("pooling=false;"))
                            {
                                ConnectionString += "pooling=false;";
                            }
                        }
                    }
                    #endregion "Setting [Pooling]"
                    connection = new OracleConnection(ConnectionString);
                }
                else
                {
                    throw new Exception("ConnectionString Not Found!");
                }
            }
            else
            {
                throw new Exception("ConnectionString Not Found!");
            }
        }

        #region "[Connection]"

        private string _ConnectionString;

        public string ConnectionString
        {
            get { return _ConnectionString; }
            set
            {
                _ConnectionString = value;
                connection = new OracleConnection(value);
            }
        }


        protected OracleConnection connection;

        public OracleConnection Connection
        {
            get { return connection; }
            set { connection = value; }
        }

        public void OpenConnection()
        {
            connection.Open();
            if (clientDetail != null)
            {
                connection.ClientId = "Employee :" + clientDetail.UserId + " IPAddess :" + clientDetail.IPAddress;
            }
        }

        public void CloseConnection()
        {
            connection.Close();
            connection.Dispose();
        }

        #endregion "[Connection]"

        #region "[Transaction]"
        protected OracleTransaction transaction;
        public void beginTransaction()
        {
            transaction = connection.BeginTransaction();
        }

        public void commitTransaction()
        {
            transaction.Commit();
            transaction.Dispose();
        }

        public void rollbackTransaction()
        {
            transaction.Rollback();
            transaction.Dispose();
        }

        #endregion "[Transaction]"

        #region "[SysDate]"

        private DateTime _SysDate;

        public DateTime SysDate
        {
            get
            {
                if (_SysDate == default(DateTime))
                {
                    GetDbSysDate();
                    return _SysDate;
                }
                else
                {
                    return _SysDate;
                }
            }
            set { _SysDate = value; }
        }

        public void GetDbSysDate()
        {
            string sqlStr = "select sysdate from dual"; OracleCommand oCmd = new OracleCommand(sqlStr, connection);
            SysDate = Convert.ToDateTime(oCmd.ExecuteScalar());
        }

        #endregion "[SysDate]"

        public bool CheckNotNull(string Input)
        {
            if (Input != null)
            {
                Input = Input.Trim();
                if (!string.IsNullOrEmpty(Input) && !string.IsNullOrWhiteSpace(Input))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private Exception ManageException(Exception ex)
        {
            if (ex is OracleException)
            {
                return new Exception("Database Exception :" + ex.Message);
            }
            else
            {
                return ex;
            }
        }
    
    }
    public static class DataTableExtensions
    {
        public static IEnumerable<T> AsEnumerable<T>(this DataTable table) where T : new()
        {
            IList<PropertyInfo> properties = typeof(T).GetProperties().ToList();
            foreach (var row in table.Rows)
            {
                yield return CreateItemFromRow<T>(row as DataRow, properties);
            }
        }
        public static T AsEnumerable<T>(this DataRow row) where T : new()
        {
            IList<PropertyInfo> properties = typeof(T).GetProperties().ToList();
            return CreateItemFromRow<T>(row as DataRow, properties);
        }
        private static T CreateItemFromRow<T>(DataRow row, IList<PropertyInfo> properties) where T : new()
        {
            T item = new T();
            foreach (PropertyInfo property in properties)
            {
                if (!property.CanWrite) continue;
                if (!row.Table.Columns.Contains(property.Name)) continue;
                if (DBNull.Value != row[property.Name])
                {
                    property.SetValue(item, TypeExtension.ChangeType(row[property.Name], property.PropertyType), null);
                }
            }

            return item;
        }
    }
    public static class TypeExtension
    {
        public static bool IsNullable(this Type type)
        {
            return ((type.IsGenericType) && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)));
        }

        public static IEnumerable<string> AppendAll(this IEnumerable<string> text, string prefix, string suffix)
        {
            foreach (string s in text)
            {
                yield return string.Format("{0}{1}{2}", prefix, s, suffix);
            }
        }

        public static void ApplyAll(this IEnumerable items, string propertyName, object data)
        {
            foreach (var item in items)
            {
                PropertyInfo property = item.GetType().GetProperty(propertyName);
                if (property == null) throw new NullReferenceException();
                if (!property.CanWrite) throw new InvalidOperationException(string.Format("Property or indexer '{0}' cannot be assign to, it is read only.", property.Name));

                property.SetValue(item, TypeExtension.ChangeType(data, property.PropertyType), null);
                //yield return item;
            }
        }
        public static IEnumerable<TResult> ConvertAll<TResult>(this IEnumerable items)
        {
            foreach (var item in items)
            {
                yield return (TResult)TypeExtension.ChangeType(item, typeof(TResult));
            }
        }
        public static object ChangeType(object value, Type conversionType)
        {
            // Note: This if block was taken from Convert.ChangeType as is, and is needed here since we're
            // checking properties on conversionType below.
            if (conversionType == null)
            {
                throw new ArgumentNullException("conversionType");
            }
            // end if

            if (conversionType.IsEnum)
            {
                return ConvertToEnumType(value, conversionType);
            }

            // If it's not a nullable type, just pass through the parameters to Convert.ChangeType
            if (conversionType.IsGenericType && conversionType.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                // It's a nullable type, so instead of calling Convert.ChangeType directly which would throw a
                // InvalidCastException (per http://weblogs.asp.net/pjohnson/archive/2006/02/07/437631.aspx),
                // determine what the underlying type is
                // If it's null, it won't convert to the underlying type, but that's fine since nulls don't really
                // have a type--so just return null
                // Note: We only do this check if we're converting to a nullable type, since doing it outside
                // would diverge from Convert.ChangeType's behavior, which throws an InvalidCastException if
                // value is null and conversionType is a value type.
                if (value == null)
                {
                    return null;
                } // end if

                // It's a nullable type, and not null, so that means it can be converted to its underlying type,
                // so overwrite the passed-in conversion type with this underlying type
                NullableConverter nullableConverter = new NullableConverter(conversionType);
                conversionType = nullableConverter.UnderlyingType;
            } // end if

            // Now that we've guaranteed conversionType is something Convert.ChangeType can handle (i.e. not a
            // nullable type), pass the call on to Convert.ChangeType
            return Convert.ChangeType(value, conversionType);
        }

        private static object ConvertToEnumType(object value, Type type)
        {
            if (value is string)
            {
                return Enum.Parse(type, value as string);
            }
            else
            {
                if (!Enum.IsDefined(type, value))
                {
                    throw new FormatException("Undefined value for enum type");
                }

                return Enum.ToObject(type, value);
            }
        }
    }
}
