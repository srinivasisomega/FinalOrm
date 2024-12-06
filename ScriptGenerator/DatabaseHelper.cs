using Microsoft.Data.SqlClient;
using System.Reflection;
using FinalOrm.Attributes;
using System.ComponentModel.DataAnnotations;
namespace FinalOrm.ScriptGenerator
{
    public static class DatabaseHelper
    {
        public static void VerifyAndGenerateScripts(string connectionString)
        {
            // Step 1: Retrieve all types in the namespace GeneratedModels with TableAttribute
            var modelTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.Namespace == "GeneratedModels" &&
                            t.GetCustomAttribute<FinalOrm.Attributes.TableAttribute>() != null)
                .ToList();

            // List to store types without corresponding tables in the database
            var missingTableTypes = new List<Type>();

            // Dictionary to store model types with schema discrepancies
            var schemaDiscrepancies = new Dictionary<Type, List<string>>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                foreach (var modelType in modelTypes)
                {
                    var tableAttribute = modelType.GetCustomAttribute<TableAttribute>();
                    string tableName = tableAttribute?.Name ?? modelType.Name;

                    // Check if the table exists
                    string tableExistsQuery = $"SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'";
                    using (var command = new SqlCommand(tableExistsQuery, connection))
                    {
                        var tableExists = command.ExecuteScalar();
                        if (tableExists == null)
                        {
                            // Table does not exist
                            missingTableTypes.Add(modelType);
                            continue;
                        }
                    }

                    // Step 2: Compare table schema with model properties
                    var discrepancies = CheckSchemaDiscrepancies(connection, modelType, tableName);
                    if (discrepancies.Any())
                    {
                        schemaDiscrepancies[modelType] = discrepancies;
                    }
                }
            }

            // Step 3: Generate and apply scripts for missing tables
            if (missingTableTypes.Any())
            {
                string createScripts = TableScriptGenerator.GenerateCreateScripts(missingTableTypes);
                Console.WriteLine("Generated Create Scripts:");
                Console.WriteLine(createScripts);

                // Optionally, execute the generated scripts in the database
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(createScripts, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }

            // Step 4: Generate and apply scripts for schema discrepancies
            if (schemaDiscrepancies.Any())
            {
                foreach (var kvp in schemaDiscrepancies)
                {
                    Console.WriteLine($"Discrepancies in table for model {kvp.Key.Name}:");
                    foreach (var discrepancy in kvp.Value)
                    {
                        Console.WriteLine($" - {discrepancy}");
                    }
                }

                string alterScripts = TableScriptGenerator.GenerateAlterScripts(schemaDiscrepancies);
                if (string.IsNullOrWhiteSpace(alterScripts))
                {
                    Console.WriteLine("No alter scripts generated.");
                    return;
                }

                Console.WriteLine("Generated Alter Scripts:");
                Console.WriteLine(alterScripts);

               

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(alterScripts, connection))
                    {
                        try
                        {
                            command.ExecuteNonQuery();
                            Console.WriteLine("Alter scripts executed successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error executing scripts: {ex.Message}");
                            throw;
                        }
                    }
                }

            }
            else
            {
                Console.WriteLine("No schema discrepancies found.");
            }
        }

        private static List<string> CheckSchemaDiscrepancies(SqlConnection connection, Type modelType, string tableName)
        {
            var discrepancies = new List<string>();
            var properties = modelType.GetProperties();

            // Get all columns from the table
            string tableColumnsQuery = $@"
        SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = '{tableName}'";
            var tableColumns = new Dictionary<string, (string DataType, int? MaxLength)>();

            using (var command = new SqlCommand(tableColumnsQuery, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string columnName = reader["COLUMN_NAME"].ToString();
                    string dataType = reader["DATA_TYPE"].ToString();
                    int? maxLength = reader["CHARACTER_MAXIMUM_LENGTH"] as int?;
                    tableColumns[columnName] = (dataType, maxLength);
                }
            }

            // Compare model properties with table columns
            foreach (var property in properties)
            {
                var columnAttribute = property.GetCustomAttribute<FinalOrm.Attributes.ColumnAttribute>();
                string columnName = columnAttribute?.Name ?? property.Name;
                string expectedDataType = GetSqlType(property.PropertyType);

                if (!tableColumns.ContainsKey(columnName))
                {
                    discrepancies.Add($"Missing column: {columnName}");
                }
                else
                {
                    var (actualDataType, actualMaxLength) = tableColumns[columnName];

                    if (!string.Equals(actualDataType, expectedDataType, StringComparison.OrdinalIgnoreCase))
                    {
                        discrepancies.Add($"Column {columnName} has mismatched data type: expected {expectedDataType}, found {actualDataType}");
                    }
                }
            }

            // Check for extra columns in the table
            foreach (var columnName in tableColumns.Keys)
            {
                if (!properties.Any(p => p.GetCustomAttribute<FinalOrm.Attributes.ColumnAttribute>()?.Name == columnName ||
                                         p.Name == columnName))
                {
                    string fkCheckQuery = $@"
                SELECT CONSTRAINT_NAME
                FROM INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE
                WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}'";
                    using (var command = new SqlCommand(fkCheckQuery, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            discrepancies.Add($"Extra column with FK constraint: {columnName}");
                        }
                        else
                        {
                            discrepancies.Add($"Extra column: {columnName}");
                        }
                    }
                }
            }

            // Check for FK constraints in the model
            // Check for FK constraints in the model
            var foreignKeys = properties.Where(p => p.GetCustomAttribute<ForeignKeyAttribute>() != null);
            foreach (var property in foreignKeys)
            {
                var fkAttribute = property.GetCustomAttribute<ForeignKeyAttribute>();
                string fkColumnName = property.GetCustomAttribute<FinalOrm.Attributes.ColumnAttribute>()?.Name ?? property.Name;

                string fkCheckQuery = $@"
        SELECT CONSTRAINT_NAME
        FROM INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE
        WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{fkColumnName}'";
                using (var command = new SqlCommand(fkCheckQuery, connection))
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        discrepancies.Add($"Missing FK constraint: Column [{fkColumnName}] references Table [{fkAttribute.ReferencedTable}] Column [{fkAttribute.ReferencedColumn}]");
                    }
                }
            }

            // Check for PK constraints in the table
            string pkQuery = $@"
        SELECT COLUMN_NAME
        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
        WHERE TABLE_NAME = '{tableName}'";
            using (var command = new SqlCommand(pkQuery, connection))
            using (var reader = command.ExecuteReader())
            {
                var primaryKeyColumns = new HashSet<string>();
                while (reader.Read())
                {
                    primaryKeyColumns.Add(reader["COLUMN_NAME"].ToString());
                }

                // Validate PK columns against model
                var modelPkProperties = properties.Where(p => p.GetCustomAttribute<KeyAttribute>() != null);
                foreach (var pkProperty in modelPkProperties)
                {
                    string pkColumnName = pkProperty.GetCustomAttribute<FinalOrm.Attributes.ColumnAttribute>()?.Name ?? pkProperty.Name;
                    if (!primaryKeyColumns.Contains(pkColumnName))
                    {
                        discrepancies.Add($"Missing PK constraint: {pkColumnName}");
                    }
                }
            }

            return discrepancies;
        }

        public static string GetSqlType(Type type)
        {
            // Handle nullable types by getting the underlying type
            if (Nullable.GetUnderlyingType(type) != null)
            {
                type = Nullable.GetUnderlyingType(type); // Get the non-nullable type
            }

            // Map C# types to SQL data types
            return type switch
            {
                _ when type == typeof(int) => "int",
                _ when type == typeof(string) => "nvarchar(MAX)", // Default to NVARCHAR(MAX)
                _ when type == typeof(DateTime) => "datetime",
                _ when type == typeof(bool) => "bit",
                _ when type == typeof(decimal) => "decimal(18, 2)", // Default to DECIMAL with precision
                _ => throw new NotSupportedException($"Type {type.Name} is not supported")
            };
        }

    }
}

