using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
namespace FinalOrm.ScriptGenerator
{
        public class ModelGenerator
        {
            public void GenerateModels(string connectionString)
            {
                string projectDirectory = GetProjectDirectory();
                if (string.IsNullOrEmpty(projectDirectory))
                    throw new InvalidOperationException("Unable to determine project directory.");

                string outputPath = Path.Combine(projectDirectory, "Models");
                Directory.CreateDirectory(outputPath);

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    var tables = connection.GetSchema("Tables");

                    foreach (DataRow table in tables.Rows)
                    {
                        string tableName = table["TABLE_NAME"].ToString();
                        GenerateModelClass(connection, tableName, outputPath, "GeneratedModels");
                    }
                }

                UpdateProjectFile(projectDirectory);
            }

        private void GenerateModelClass(SqlConnection connection, string tableName, string outputPath, string @namespace)
        {
            var columns = GetTableColumnsWithMetadata(connection, tableName);
            if (columns == null || !columns.Any())
            {
                Console.WriteLine($"No columns found for table {tableName}. Skipping.");
                return;
            }

            Console.WriteLine($"Generating model for table: {tableName}");

            var classBuilder = new StringBuilder();
            classBuilder.AppendLine($"using FinalOrm.Attributes;");
            classBuilder.AppendLine();
            classBuilder.AppendLine($"namespace {@namespace}");
            classBuilder.AppendLine("{");
            classBuilder.AppendLine($"    [Table(\"{tableName}\")]");
            classBuilder.AppendLine($"    public class {tableName}");
            classBuilder.AppendLine("    {");

            foreach (var column in columns)
            {
                Console.WriteLine($"Processing column: {column.ColumnName}, DataType: {column.DataType}, IsNullable: {column.IsNullable}");

                string propertyType = column.DataType; // Now directly using the string value

                classBuilder.Append($"        [Column(\"{column.ColumnName}\", IsNullable = {column.IsNullable.ToString().ToLower()}");
                if (column.MaxLength.HasValue)
                {
                    classBuilder.Append($", Length = {column.MaxLength}");
                }
                classBuilder.AppendLine(")]");

                if (column.IsPrimaryKey)
                {
                    classBuilder.AppendLine($"        [PrimaryKey(IsIdentity = {column.IsIdentity.ToString().ToLower()})]");
                }

                if (column.IsForeignKey)
                {
                    classBuilder.AppendLine($"        [ForeignKey(\"{column.ReferencedTable}\", \"{column.ReferencedColumn}\")]");
                }

                classBuilder.AppendLine($"        public {propertyType} {column.ColumnName} {{ get; set; }}");
            }

            classBuilder.AppendLine("    }");
            classBuilder.AppendLine("}");

            string filePath = Path.Combine(outputPath, $"{tableName}.cs");
            File.WriteAllText(filePath, classBuilder.ToString());
            Console.WriteLine($"Model for {tableName} generated at {filePath}");
        }

        private List<ColumnMetadata> GetTableColumnsWithMetadata(SqlConnection connection, string tableName)
        {
            var columns = new List<ColumnMetadata>();
            var schema = connection.GetSchema("Columns", new[] { null, null, tableName, null });

            foreach (DataRow row in schema.Rows)
            {
                var column = new ColumnMetadata
                {
                    ColumnName = row["COLUMN_NAME"].ToString(),
                    DataType = GetCSharpType(row["DATA_TYPE"].ToString()), // Now returns a string
                    IsNullable = row["IS_NULLABLE"].ToString() == "YES",
                    MaxLength = row["CHARACTER_MAXIMUM_LENGTH"] != DBNull.Value
                        ? Convert.ToInt32(row["CHARACTER_MAXIMUM_LENGTH"])
                        : (int?)null
                };

                Console.WriteLine($"Column: {column.ColumnName}, SQL Type: {row["DATA_TYPE"]}, C# Type: {column.DataType}, IsNullable: {column.IsNullable}");

                // Check for Primary Key and Identity
                var pkSchema = connection.GetSchema("IndexColumns", new[] { null, null, tableName });
                column.IsPrimaryKey = pkSchema.AsEnumerable().Any(r =>
                    string.Equals(r["COLUMN_NAME"].ToString(), column.ColumnName, StringComparison.OrdinalIgnoreCase));
                column.IsIdentity = column.IsPrimaryKey && IsColumnIdentity(connection, tableName, column.ColumnName);

                // Check for Foreign Key
                var fkSchema = connection.GetSchema("ForeignKeys");
                var fkRow = fkSchema.AsEnumerable()
                    .FirstOrDefault(r =>
                        string.Equals(r["FK_TABLE_NAME"].ToString(), tableName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r["FK_COLUMN_NAME"].ToString(), column.ColumnName, StringComparison.OrdinalIgnoreCase));

                if (fkRow != null)
                {
                    column.IsForeignKey = true;
                    column.ReferencedTable = fkRow["PK_TABLE_NAME"].ToString();
                    column.ReferencedColumn = fkRow["PK_COLUMN_NAME"].ToString();
                }

                columns.Add(column);
            }

            return columns;
        }

        private bool IsColumnIdentity(SqlConnection connection, string tableName, string columnName)
            {
                using (var command = new SqlCommand($"SELECT COLUMNPROPERTY(OBJECT_ID('{tableName}'), '{columnName}', 'IsIdentity')", connection))
                {
                    return (int)command.ExecuteScalar() == 1;
                }
            }

        private string GetCSharpType(string sqlType)
        {
            return sqlType.ToLower() switch
            {
                "varchar" or "nvarchar" or "char" or "text" => "string",
                "int" => "int",
                "bigint" => "long",
                "decimal" or "numeric" or "money" => "decimal",
                "datetime" or "smalldatetime" or "date" or "time" => "DateTime",
                "bit" => "bool",
                "float" => "double",
                "uniqueidentifier" => "Guid",
                _ => "object"
            };
        }


        private void UpdateProjectFile(string projectDirectory)
        {
            // Locate the .csproj file
            string csprojFile = Directory.GetFiles(projectDirectory, "*.csproj").FirstOrDefault();
            if (csprojFile == null)
            {
                Console.WriteLine("No .csproj file found in the project directory.");
                return;
            }

            var doc = new System.Xml.XmlDocument();
            doc.Load(csprojFile);

            // Find or create an <ItemGroup> element
            var namespaceManager = new System.Xml.XmlNamespaceManager(doc.NameTable);
            namespaceManager.AddNamespace("msbuild", "http://schemas.microsoft.com/developer/msbuild/2003");

            var itemGroup = doc.SelectSingleNode("//msbuild:ItemGroup", namespaceManager);
            if (itemGroup == null)
            {
                itemGroup = doc.CreateElement("ItemGroup", doc.DocumentElement.NamespaceURI);
                doc.DocumentElement.AppendChild(itemGroup);
            }

            // Check if the inclusion already exists
            string includeStatement = "Models\\**\\*.cs";
            var existingCompile = itemGroup.SelectSingleNode($"msbuild:Compile[@Include='{includeStatement}']", namespaceManager);

            if (existingCompile == null)
            {
                // Add a new <Compile Include> node
                var compileNode = doc.CreateElement("Compile", doc.DocumentElement.NamespaceURI);
                compileNode.SetAttribute("Include", includeStatement);
                itemGroup.AppendChild(compileNode);

                // Save the updated .csproj file
                doc.Save(csprojFile);
                Console.WriteLine("Updated .csproj file to include generated models.");
            }
            else
            {
                Console.WriteLine("Models inclusion already exists in .csproj file.");
            }
        }

        private string GetProjectDirectory()
            {
                string currentDirectory = Directory.GetCurrentDirectory();
                while (!Directory.GetFiles(currentDirectory, "*.csproj").Any())
                {
                    currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
                    if (string.IsNullOrEmpty(currentDirectory)) return null;
                }
                return currentDirectory;
            }
        }

    public class ColumnMetadata
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; } // Changed from Type to string
        public bool IsNullable { get; set; }
        public int? MaxLength { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsForeignKey { get; set; }
        public string ReferencedTable { get; set; }
        public string ReferencedColumn { get; set; }
    }


    public class ForeignKeyInfo
    {
        public string ColumnName { get; set; }
        public string ReferencedTable { get; set; }
    }


}
