using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinalOrm.Attributes
{
   
        // For Primary Key
        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        public class PrimaryKeyAttribute : Attribute
        {
            public bool IsIdentity { get; set; } = false; // For auto-increment
        }

        // For Foreign Key
        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        public class ForeignKeyAttribute : Attribute
        {
            public string ReferencedTable { get; }
            public string ReferencedColumn { get; }

            public ForeignKeyAttribute(string referencedTable, string referencedColumn)
            {
                ReferencedTable = referencedTable;
                ReferencedColumn = referencedColumn;
            }
        }

        // For Unique Constraint
        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        public class UniqueAttribute : Attribute { }

        // For Default Value
        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        public class DefaultValueAttribute : Attribute
        {
            public object Value { get; }
            public DefaultValueAttribute(object value)
            {
                Value = value;
            }
        }

        // For Relationships
        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        public class OneToOneAttribute : Attribute
        {
            public string ForeignKeyProperty { get; }
            public OneToOneAttribute(string foreignKeyProperty)
            {
                ForeignKeyProperty = foreignKeyProperty;
            }
        }

        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        public class OneToManyAttribute : Attribute
        {
            public string ForeignKeyProperty { get; }
            public OneToManyAttribute(string foreignKeyProperty)
            {
                ForeignKeyProperty = foreignKeyProperty;
            }
        }

        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        public class ManyToManyAttribute : Attribute
        {
            public string JoinTable { get; }
            public string JoinColumn { get; }
            public string InverseJoinColumn { get; }

            public ManyToManyAttribute(string joinTable, string joinColumn, string inverseJoinColumn)
            {
                JoinTable = joinTable;
                JoinColumn = joinColumn;
                InverseJoinColumn = inverseJoinColumn;
            }
        }

        // For Column Mapping
        [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
        public class ColumnAttribute : Attribute
        {
            public string Name { get; }
            public bool IsNullable { get; set; } = true;
            public int Length { get; set; } = -1; // For variable-length columns
            public ColumnAttribute(string name)
            {
                Name = name;
            }
        }

    // For Table Mapping
    
        [AttributeUsage(AttributeTargets.Class, Inherited = false)]
        public class TableAttribute : Attribute
        {
            public string Name { get; set; }
            public TableAttribute(string name) => Name = name;
        }
    


}
