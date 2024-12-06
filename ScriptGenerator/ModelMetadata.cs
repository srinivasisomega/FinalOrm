using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinalOrm.Attributes;
using System.Reflection;
namespace FinalOrm.ScriptGenerator
{
   

    public static class ModelMetadata
    {
        public static string GetTableName(Type type)
        {
            var tableAttribute = type.GetCustomAttribute<TableAttribute>();
            return tableAttribute?.Name ?? throw new InvalidOperationException("Table attribute is missing.");
        }

        public static List<(string PropertyName, string ColumnName, bool IsNullable, bool IsPrimaryKey, bool IsIdentity)> GetProperties(Type type)
        {
            return type.GetProperties()
                       .Select(prop =>
                       {
                           var columnAttribute = prop.GetCustomAttribute<ColumnAttribute>();
                           if (columnAttribute == null) return default;

                           var primaryKeyAttribute = prop.GetCustomAttribute<PrimaryKeyAttribute>();
                           return (
                               PropertyName: prop.Name,
                               ColumnName: columnAttribute.Name,
                               IsNullable: columnAttribute.IsNullable,
                               IsPrimaryKey: primaryKeyAttribute != null,
                               IsIdentity: primaryKeyAttribute?.IsIdentity ?? false
                           );
                       })
                       .Where(p => p.ColumnName != null)
                       .ToList();
        }
    }

}
