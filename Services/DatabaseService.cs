using Library.Services;
using MySql.Data.MySqlClient;
using System.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


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
        Task ExecuteAsync(string v, Dictionary<string, object> dictionary);
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            // Read connection string from environment variable first
            var env = Environment.GetEnvironmentVariable("LIBRARY_DB_CONN");
            if (!string.IsNullOrWhiteSpace(env))
            {
                _connectionString = env;
            }
            else
            {
                // Fallback (development) - replace with your local credentials only for dev/testing
                _connectionString = "Server=localhost;Database=global_library_system;Uid=root;Pwd=edra@k;";
            }
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
            // Root cause: MySqlDataAdapter.Fill is synchronous and runs on the calling (UI) thread.
            // Fix: run the blocking Fill on a thread-pool thread so the UI thread is not blocked.
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

            // Run the synchronous adapter.Fill on a background thread to avoid blocking the UI thread.
            await Task.Run(() =>
            {
                using var adapter = new MySqlDataAdapter(command);
                adapter.Fill(dataTable);
            });

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

        List<T> ConvertDataTableToList<T>(DataTable dataTable) where T : class, new()
        {
            var list = new List<T>();
            var properties = typeof(T).GetProperties();

            // Build a lookup of normalized column name -> DataColumnName
            // Normalization: lowercase + remove underscores
            var columnLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn col in dataTable.Columns)
            {
                var normalized = NormalizeColumnName(col.ColumnName);
                if (!columnLookup.ContainsKey(normalized))
                    columnLookup[normalized] = col.ColumnName;
            }

            foreach (DataRow row in dataTable.Rows)
            {
                var obj = new T();

                foreach (var prop in properties)
                {
                    var propNormalized = NormalizeColumnName(prop.Name);

                    if (!columnLookup.TryGetValue(propNormalized, out var matchingColumnName))
                        continue;

                    var value = row[matchingColumnName];

                    if (value == DBNull.Value)
                    {
                        // If property is a reference type or nullable value type, set null.
                        var propType = prop.PropertyType;
                        var underlyingNullable = Nullable.GetUnderlyingType(propType);

                        if (!propType.IsValueType || underlyingNullable != null)
                        {
                            prop.SetValue(obj, null);
                        }
                        // For non-nullable value types, leave default (do nothing).
                    }
                    else
                    {
                        // Convert.ChangeType cannot convert to Nullable<T> directly, so map to underlying type when needed.
                        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        try
                        {
                            // Handle conversion for enums and common types
                            object? safeValue;
                            if (targetType.IsEnum)
                            {
                                safeValue = Enum.ToObject(targetType, value);
                            }
                            else
                            {
                                safeValue = Convert.ChangeType(value, targetType);
                            }

                            prop.SetValue(obj, safeValue);
                        }
                        catch
                        {
                            // If conversion fails, skip setting the property to avoid crashing the mapping.
                        }
                    }
                }

                list.Add(obj);
            }

            return list;
        }

        // Helper: normalize names by lowercasing and removing underscores so "branch_id" -> "branchid" matches "BranchId"
        private static string NormalizeColumnName(string name)
        {
            return name?.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant() ?? string.Empty;
        }

        public async Task ExecuteAsync(string v, Dictionary<string, object> dictionary)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = v;
            foreach (var p in dictionary)
                cmd.Parameters.AddWithValue(p.Key, p.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}