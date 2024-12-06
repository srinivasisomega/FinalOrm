using FinalOrm.Attributes;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FinalOrm.ScriptGenerator
{
    public static class RepositoryFactory
    {
        private static readonly string _connectionString = "your_connection_string_here";

        public static Repository<T> Create<T>() where T : new()
        {
            return new Repository<T>(_connectionString);
        }
    }

    public class Repository<T> where T : new()
    {
        private readonly string _connectionString;

        public Repository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Create(T entity)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var tableName = ModelMetadata.GetTableName(typeof(T));
            var properties = ModelMetadata.GetProperties(typeof(T)).Where(p => !p.IsIdentity);

            var columnNames = string.Join(", ", properties.Select(p => p.ColumnName));
            var values = string.Join(", ", properties.Select(p => $"@{p.ColumnName}"));

            var query = $@"INSERT INTO {tableName} ({columnNames}) VALUES ({values}); SELECT SCOPE_IDENTITY();";

            using var command = new SqlCommand(query, connection);

            foreach (var property in properties)
            {
                var value = typeof(T).GetProperty(property.PropertyName)?.GetValue(entity);
                command.Parameters.AddWithValue($"@{property.ColumnName}", value ?? DBNull.Value);
            }

            var idProperty = typeof(T).GetProperties().FirstOrDefault(p => p.GetCustomAttribute<PrimaryKeyAttribute>()?.IsIdentity == true);
            if (idProperty != null)
            {
                idProperty.SetValue(entity, Convert.ToInt32(command.ExecuteScalar()));
            }
        }

        public T? Read(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var tableName = ModelMetadata.GetTableName(typeof(T));
            var properties = ModelMetadata.GetProperties(typeof(T));
            var idColumn = properties.First(p => p.IsPrimaryKey).ColumnName;

            var query = $"SELECT * FROM {tableName} WHERE {idColumn} = @Id";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", id);

            using var reader = command.ExecuteReader();
            return reader.Read() ? MapEntity(reader, properties) : default;
        }

        public void Update(T entity)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var tableName = ModelMetadata.GetTableName(typeof(T));
            var properties = ModelMetadata.GetProperties(typeof(T)).Where(p => !p.IsPrimaryKey);

            var setClause = string.Join(", ", properties.Select(p => $"{p.ColumnName} = @{p.ColumnName}"));
            var idColumn = ModelMetadata.GetProperties(typeof(T)).First(p => p.IsPrimaryKey).ColumnName;

            var query = $@"UPDATE {tableName} SET {setClause} WHERE {idColumn} = @Id";

            using var command = new SqlCommand(query, connection);

            foreach (var property in properties)
            {
                var value = typeof(T).GetProperty(property.PropertyName)?.GetValue(entity);
                command.Parameters.AddWithValue($"@{property.ColumnName}", value ?? DBNull.Value);
            }

            var idValue = typeof(T).GetProperty(idColumn)?.GetValue(entity);
            command.Parameters.AddWithValue("@Id", idValue);

            command.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var tableName = ModelMetadata.GetTableName(typeof(T));
            var idColumn = ModelMetadata.GetProperties(typeof(T)).First(p => p.IsPrimaryKey).ColumnName;

            var query = $"DELETE FROM {tableName} WHERE {idColumn} = @Id";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", id);

            command.ExecuteNonQuery();
        }

        public List<T> ReadAll()
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var tableName = ModelMetadata.GetTableName(typeof(T));
            var properties = ModelMetadata.GetProperties(typeof(T));

            var query = $"SELECT * FROM {tableName}";

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            var results = new List<T>();
            while (reader.Read())
            {
                var entity = MapEntity(reader, properties);
                if (entity != null) results.Add(entity);
            }

            return results;
        }

        private T MapEntity(SqlDataReader reader, List<(string PropertyName, string ColumnName, bool IsNullable, bool IsPrimaryKey, bool IsIdentity)> properties)
        {
            var entity = new T();
            foreach (var property in properties)
            {
                var value = reader[property.ColumnName];
                if (value == DBNull.Value) value = null;
                typeof(T).GetProperty(property.PropertyName)?.SetValue(entity, value);
            }
            return entity;
        }
        public void BulkUpdate(IEnumerable<T> entities)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var tableName = ModelMetadata.GetTableName(typeof(T));
            var properties = ModelMetadata.GetProperties(typeof(T)).Where(p => !p.IsPrimaryKey);
            var idColumn = ModelMetadata.GetProperties(typeof(T)).First(p => p.IsPrimaryKey).ColumnName;

            foreach (var entity in entities)
            {
                var setClause = string.Join(", ", properties.Select(p => $"{p.ColumnName} = @{p.ColumnName}"));
                var query = $@"UPDATE {tableName} SET {setClause} WHERE {idColumn} = @Id";

                using var command = new SqlCommand(query, connection);

                foreach (var property in properties)
                {
                    var value = typeof(T).GetProperty(property.PropertyName)?.GetValue(entity);
                    command.Parameters.AddWithValue($"@{property.ColumnName}", value ?? DBNull.Value);
                }

                var idValue = typeof(T).GetProperty(idColumn)?.GetValue(entity);
                command.Parameters.AddWithValue("@Id", idValue);

                command.ExecuteNonQuery();
            }
        }

        public void BulkInsert(IEnumerable<T> entities)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            var tableName = ModelMetadata.GetTableName(typeof(T));
            var properties = ModelMetadata.GetProperties(typeof(T)).Where(p => !p.IsIdentity);

            var columnNames = string.Join(", ", properties.Select(p => p.ColumnName));
            var placeholders = string.Join(", ", properties.Select(p => $"@{p.ColumnName}"));

            foreach (var entity in entities)
            {
                var query = $@"INSERT INTO {tableName} ({columnNames}) VALUES ({placeholders});";
                using var command = new SqlCommand(query, connection);

                foreach (var property in properties)
                {
                    var value = typeof(T).GetProperty(property.PropertyName)?.GetValue(entity);
                    command.Parameters.AddWithValue($"@{property.ColumnName}", value ?? DBNull.Value);
                }

                command.ExecuteNonQuery();
            }
        }

    }

}
