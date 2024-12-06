using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FinalOrm.Attributes;
using Microsoft.Data.SqlClient;
namespace FinalOrm.ScriptGenerator
{

    public class TableScriptGenerator
    {
        public static string GenerateCreateScripts(IEnumerable<Type> entityTypes)
        {
            var scriptBuilder = new StringBuilder();

            foreach (var entityType in entityTypes)
            {
                // Get Table Attribute
                var tableAttribute = entityType.GetCustomAttribute<TableAttribute>();
                if (tableAttribute == null)
                    throw new InvalidOperationException($"Class {entityType.Name} does not have a Table attribute.");

                string tableName = tableAttribute.Name;
                scriptBuilder.AppendLine($"CREATE TABLE [{tableName}] (");

                var properties = entityType.GetProperties();
                var columnDefinitions = new List<string>();
                var foreignKeys = new List<string>();

                foreach (var property in properties)
                {
                    var columnDefinition = GenerateColumnDefinition(property, tableName, foreignKeys);
                    if (!string.IsNullOrEmpty(columnDefinition))
                        columnDefinitions.Add(columnDefinition);
                }

                scriptBuilder.AppendLine(string.Join(",\n", columnDefinitions));

                // Add foreign key constraints
                if (foreignKeys.Any())
                {
                    scriptBuilder.AppendLine(",");
                    scriptBuilder.AppendLine(string.Join(",\n", foreignKeys));
                }

                scriptBuilder.AppendLine(");");
                scriptBuilder.AppendLine();
            }

            return scriptBuilder.ToString();
        }

        public static string GenerateAlterScripts(Dictionary<Type, List<string>> schemaDiscrepancies)
        {
            var scriptBuilder = new StringBuilder();

            foreach (var kvp in schemaDiscrepancies)
            {
                var modelType = kvp.Key;
                var discrepancies = kvp.Value;

                var tableAttribute = modelType.GetCustomAttribute<TableAttribute>();
                if (tableAttribute == null)
                    throw new InvalidOperationException($"Class {modelType.Name} does not have a Table attribute.");

                string tableName = tableAttribute.Name;

                foreach (var discrepancy in discrepancies)
                {
                    if (discrepancy.StartsWith("Missing column:"))
                    {
                        // Add column to the table
                        string columnName = discrepancy.Replace("Missing column:", "").Trim();
                        var property = modelType.GetProperties()
                            .FirstOrDefault(p => p.GetCustomAttribute<ColumnAttribute>()?.Name == columnName || p.Name == columnName);

                        if (property != null)
                        {
                            string columnDefinition = GenerateColumnDefinition(property, tableName, new List<string>());
                            scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] ADD {columnDefinition};");
                        }
                    }
                    else if (discrepancy.StartsWith("Extra column with FK constraint:"))
                    {
                        string columnName = discrepancy.Replace("Extra column with FK constraint:", "").Trim();

                        // Drop FK constraint first
                        scriptBuilder.AppendLine($@"
        DECLARE @ConstraintName NVARCHAR(MAX);
        SELECT @ConstraintName = CONSTRAINT_NAME
        FROM INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE
        WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}';

        IF @ConstraintName IS NOT NULL
        BEGIN
            ALTER TABLE [{tableName}] DROP CONSTRAINT @ConstraintName;
        END;");

                        // Drop the column
                        scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] DROP COLUMN [{columnName}];");
                    }

                    else if (discrepancy.Contains("mismatched data type"))
                    {
                        // Update column data type
                        var parts = discrepancy.Split(':');
                        string columnName = parts[0].Replace("Column", "").Trim();
                        var property = modelType.GetProperties()
                            .FirstOrDefault(p => p.GetCustomAttribute<ColumnAttribute>()?.Name == columnName || p.Name == columnName);

                        if (property != null)
                        {
                            string newDataType = DatabaseHelper.GetSqlType(property.PropertyType);
                            scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] ALTER COLUMN [{columnName}] {newDataType};");
                        }
                    }
                    else if (discrepancy.StartsWith("Drop column:"))
                    {
                        // Drop column from the table
                        string columnName = discrepancy.Replace("Drop column:", "").Trim();

                        // Check for foreign key constraints on this column
                        scriptBuilder.AppendLine("DECLARE @ConstraintName NVARCHAR(MAX);");
                        scriptBuilder.AppendLine("DECLARE @Sql NVARCHAR(MAX);");
                        scriptBuilder.AppendLine();
                        scriptBuilder.AppendLine("-- Retrieve the constraint name");
                        scriptBuilder.AppendLine($"SELECT @ConstraintName = CONSTRAINT_NAME");
                        scriptBuilder.AppendLine($"FROM INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE");
                        scriptBuilder.AppendLine($"WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}';");
                        scriptBuilder.AppendLine();
                        scriptBuilder.AppendLine("-- Drop the constraint if it exists");
                        scriptBuilder.AppendLine("IF @ConstraintName IS NOT NULL");
                        scriptBuilder.AppendLine("BEGIN");
                        scriptBuilder.AppendLine("    -- Construct the SQL to drop the constraint");
                        scriptBuilder.AppendLine($"    SET @Sql = 'ALTER TABLE [{tableName}] DROP CONSTRAINT [' + @ConstraintName + ']';");
                        scriptBuilder.AppendLine();
                        scriptBuilder.AppendLine("    -- Execute the SQL");
                        scriptBuilder.AppendLine("    EXEC sp_executesql @Sql;");
                        scriptBuilder.AppendLine("END;");
                        scriptBuilder.AppendLine();
                        scriptBuilder.AppendLine($"-- Drop the column");


                        // Drop the column
                        scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] DROP COLUMN [{columnName}];");
                    }
                    else if (discrepancy.Contains("mismatched data type"))
                    {
                        var parts = discrepancy.Split(':');
                        string columnName = parts[0].Replace("Column", "").Trim();

                        // Find the property that matches this column
                        var property = modelType.GetProperties()
                            .FirstOrDefault(p => p.GetCustomAttribute<ColumnAttribute>()?.Name == columnName || p.Name == columnName);

                        if (property != null)
                        {
                            string newDataType = DatabaseHelper.GetSqlType(property.PropertyType);
                            scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] ALTER COLUMN [{columnName}] {newDataType};");
                        }
                        else
                        {
                            throw new InvalidOperationException($"No property found for column {columnName} in model {modelType.Name}");
                        }
                    }
                    else if (discrepancy.StartsWith("Extra column:"))
                    {
                        string columnName = discrepancy.Replace("Extra column:", "").Trim();
                        // Generate the script to drop the column
                        scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] DROP COLUMN [{columnName}];");
                    }


                }
            }

            return scriptBuilder.ToString();
        }

        private static string GenerateColumnDefinition(PropertyInfo property, string tableName, List<string> foreignKeys)
        {
            var columnAttribute = property.GetCustomAttribute<ColumnAttribute>();
            var primaryKeyAttribute = property.GetCustomAttribute<PrimaryKeyAttribute>();
            var foreignKeyAttribute = property.GetCustomAttribute<ForeignKeyAttribute>();
            var uniqueAttribute = property.GetCustomAttribute<UniqueAttribute>();
            var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();

            if (columnAttribute == null)
                return null;

            string columnName = columnAttribute.Name;
            string columnDefinition = $"[{columnName}] {GetSqlType(property.PropertyType, columnAttribute)}";

            if (!columnAttribute.IsNullable)
                columnDefinition += " NOT NULL";

            if (primaryKeyAttribute != null)
            {
                columnDefinition += primaryKeyAttribute.IsIdentity ? " IDENTITY(1,1)" : "";
                columnDefinition += " PRIMARY KEY";
            }

            if (uniqueAttribute != null)
                columnDefinition += " UNIQUE";

            if (defaultValueAttribute != null)
                columnDefinition += $" DEFAULT {FormatDefaultValue(defaultValueAttribute.Value)}";

            if (foreignKeyAttribute != null)
            {
                foreignKeys.Add($"CONSTRAINT FK_{tableName}_{columnName} FOREIGN KEY ([{columnName}]) REFERENCES [{foreignKeyAttribute.ReferencedTable}]([{foreignKeyAttribute.ReferencedColumn}])");
            }

            return columnDefinition;
        }

        private static string GetSqlType(Type type, ColumnAttribute columnAttribute)
        {
            // Handle nullable types
            if (Nullable.GetUnderlyingType(type) != null)
            {
                type = Nullable.GetUnderlyingType(type);
            }

            if (type == typeof(string))
                return columnAttribute.Length > 0 ? $"NVARCHAR({columnAttribute.Length})" : "NVARCHAR(MAX)";
            if (type == typeof(int))
                return "INT";
            if (type == typeof(long))
                return "BIGINT";
            if (type == typeof(decimal))
                return "DECIMAL(18,2)";
            if (type == typeof(bool))
                return "BIT";
            if (type == typeof(DateTime))
                return "DATETIME";

            throw new NotSupportedException($"Type {type.Name} is not supported.");
        }

        private static string FormatDefaultValue(object value)
        {
            return value switch
            {
                string s => $"'{s}'",
                DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                bool b => b ? "1" : "0",
                _ => value.ToString()
            };
        }
    }




}
