using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Data;
namespace FinalOrm.ScriptGenerator
{
    

    public class Utility
    {
        private readonly string _connectionString;

        public Utility(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Drops the constraint for a specific column in a table and returns the constraint name.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="columnName">The name of the column.</param>
        /// <returns>The name of the dropped constraint, or null if no constraint existed.</returns>
        public string DropConstraintAndReturnName(string tableName, string columnName)
        {
            string constraintName = null;

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand())
                {
                    command.Connection = connection;

                    // Retrieve the constraint name
                    command.CommandText = @"
                    SELECT @ConstraintName = CONSTRAINT_NAME
                    FROM INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE
                    WHERE TABLE_NAME = @TableName AND COLUMN_NAME = @ColumnName;";

                    command.Parameters.AddWithValue("@TableName", tableName);
                    command.Parameters.AddWithValue("@ColumnName", columnName);
                    var constraintNameParam = new SqlParameter("@ConstraintName", SqlDbType.NVarChar, -1)
                    {
                        Direction = ParameterDirection.Output
                    };
                    command.Parameters.Add(constraintNameParam);

                    command.ExecuteNonQuery();

                    constraintName = constraintNameParam.Value as string;

                    // Drop the constraint if it exists
                    if (!string.IsNullOrEmpty(constraintName))
                    {
                        command.CommandText = $@"
                        DECLARE @Sql NVARCHAR(MAX);
                        SET @Sql = 'ALTER TABLE [{tableName}] DROP CONSTRAINT [' + @ConstraintName + ']';
                        EXEC sp_executesql @Sql;";

                        command.ExecuteNonQuery();
                    }
                }
            }

            return constraintName;
        }
    }

}
