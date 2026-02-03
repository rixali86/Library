using MySql.Data.MySqlClient;
using System.Data;
using System.Configuration;
using Microsoft.Extensions.Configuration;

namespace Library.Services
{
    public interface IDatabaseService
    {
        Task<MySqlConnection> GetConnectionAsync();
        Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object>? parameters = null);
        Task<object?> ExecuteScalarAsync(string query, Dictionary<string, object>? parameters = null);
        Task<DataTable> ExecuteQueryAsync(string query, Dictionary<string, object>? parameters = null);
        Task<List<T>> QueryAsync<T>(string query, Dictionary<string, object>? parameters = null) where T : class, new();
        Task<T?> QuerySingleAsync<T>(string query, Dictionary<string, object>? parameters = null) where T : class, new();
    }



    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            // You can load this from appsettings.json or secrets
            _connectionString = "Server=localhost;Database=global_library_system;Uid=root;Pwd=edra@k;";
        }

        public async Task<MySqlConnection> GetConnectionAsync()
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object>? parameters = null)
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new MySqlCommand(query, connection);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }

            return await command.ExecuteNonQueryAsync();
        }

        public async Task<object?> ExecuteScalarAsync(string query, Dictionary<string, object>? parameters = null)
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new MySqlCommand(query, connection);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }

            return await command.ExecuteScalarAsync();
        }

        public async Task<DataTable> ExecuteQueryAsync(string query, Dictionary<string, object>? parameters = null)
        {
            await using var connection = await GetConnectionAsync();
            await using var command = new MySqlCommand(query, connection);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }

            var dataTable = new DataTable();
            var adapter = new MySqlDataAdapter(command);
            adapter.Fill(dataTable);

            return dataTable;
        }

        public async Task<List<T>> QueryAsync<T>(string query, Dictionary<string, object>? parameters = null) where T : class, new()
        {
            var dataTable = await ExecuteQueryAsync(query, parameters);
            return ConvertDataTableToList<T>(dataTable);
        }

        public async Task<T?> QuerySingleAsync<T>(string query, Dictionary<string, object>? parameters = null) where T : class, new()
        {
            var results = await QueryAsync<T>(query, parameters);
            return results.FirstOrDefault();
        }

        private List<T> ConvertDataTableToList<T>(DataTable dataTable) where T : class, new()
        {
            var list = new List<T>();

            foreach (DataRow row in dataTable.Rows)
            {
                var obj = new T();
                var properties = typeof(T).GetProperties();

                foreach (var prop in properties)
                {
                    if (dataTable.Columns.Contains(prop.Name))
                    {
                        var value = row[prop.Name];
                        if (value != DBNull.Value)
                        {
                            prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
                        }
                    }
                }

                list.Add(obj);
            }


            return list;
        }
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                return connection.State == ConnectionState.Open;
            }
            catch
            {
                return false;
            }
        }
    }


}