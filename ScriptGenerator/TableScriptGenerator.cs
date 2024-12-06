using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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

        public static string GenerateAlterScripts(Dictionary<Type, List<string>> schemaDiscrepancies, Utility utility)
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

                        // Use utility to get constraint name and drop it dynamically
                        string constraintName = utility.DropConstraintAndReturnName(tableName, columnName);

                        if (!string.IsNullOrEmpty(constraintName))
                        {
                            scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];");
                        }

                        // Drop the column
                        scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] DROP COLUMN [{columnName}];");
                    }
                    else if (discrepancy.StartsWith("Drop column:"))
                    {
                        string columnName = discrepancy.Replace("Drop column:", "").Trim();

                        // Use utility to drop the constraint and append query
                        string constraintName = utility.DropConstraintAndReturnName(tableName, columnName);

                        if (!string.IsNullOrEmpty(constraintName))
                        {
                            scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];");
                        }

                        // Drop the column
                        scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] DROP COLUMN [{columnName}];");
                    }
                    else if (discrepancy.Contains("mismatched data type"))
                    {
                        var parts = discrepancy.Split(':');
                        string columnName = parts[0].Replace("Column", "").Trim();

                        // Use utility to drop constraint if necessary
                        string constraintName = utility.DropConstraintAndReturnName(tableName, columnName);

                        if (!string.IsNullOrEmpty(constraintName))
                        {
                            scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];");
                        }

                        // Find the property that matches this column
                        var property = modelType.GetProperties()
                            .FirstOrDefault(p => p.GetCustomAttribute<ColumnAttribute>()?.Name == columnName || p.Name == columnName);

                        if (property != null)
                        {
                            string newDataType = GetSqlType(property.PropertyType, property.GetCustomAttribute<ColumnAttribute>());
                            scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] ALTER COLUMN [{columnName}] {newDataType};");
                        }
                    }
                    else if (discrepancy.StartsWith("Missing FK constraint:"))
                    {
                        // Add missing foreign key constraint
                        string details = discrepancy.Replace("Missing FK constraint:", "").Trim();
                        var match = Regex.Match(details, @"Column \[(.+)\] references Table \[(.+)\] Column \[(.+)\]");
                        if (match.Success)
                        {
                            string columnName = match.Groups[1].Value;
                            string referencedTable = match.Groups[2].Value;
                            string referencedColumn = match.Groups[3].Value;

                            scriptBuilder.AppendLine($"ALTER TABLE [{tableName}] ADD CONSTRAINT [FK_{tableName}_{columnName}] FOREIGN KEY ([{columnName}]) REFERENCES [{referencedTable}] ([{referencedColumn}]);");
                        }
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
