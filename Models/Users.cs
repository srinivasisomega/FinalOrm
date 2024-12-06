using FinalOrm.Attributes;

namespace GeneratedModels
{
    [Table("Users")]
    public class Users
    {
        [Column("Id", IsNullable = false)]
        [PrimaryKey(IsIdentity = true)]
        public int Id { get; set; }
        [Column("Username", IsNullable = false, Length = 50)]
        public string Username { get; set; }
        [Column("PasswordHash", IsNullable = false, Length = 256)]
        public string PasswordHash { get; set; }
        [Column("CreatedAt", IsNullable = false)]
        public DateTime CreatedAt { get; set; }
    }
    [Table("Roles")]
    public class Roles
    {
        [Column("Id", IsNullable = false)]
        [PrimaryKey(IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name", IsNullable = false, Length = 100)]
        public string Name { get; set; }

        [Column("Description", IsNullable = true, Length = 256)]
        public string Description { get; set; }
       
    }
    [Table("UserRoles")]
    public class UserRoles
    {
        [Column("UserId", IsNullable = false)]
        [ForeignKey("Users", "Id")]
        public int UserId { get; set; }

        [Column("RoleId", IsNullable = false)]
        [ForeignKey("Roles", "Id")]
        public int RoleId { get; set; }

        [Column("AssignedAt", IsNullable = false)]
        public DateTime AssignedAt { get; set; }
    }
    [Table("UserProfiles")]
    public class UserProfiles
    {
        [Column("Id", IsNullable = false)]
        [PrimaryKey(IsIdentity = true)]
        public int Id { get; set; }

        [Column("UserId", IsNullable = false)]
        [ForeignKey("Users", "Id")]
        public int UserId { get; set; }

        [Column("FirstName", IsNullable = false, Length = 50)]
        public string FirstName { get; set; }

        [Column("LastName", IsNullable = false, Length = 50)]
        public string LastName { get; set; }

        [Column("DateOfBirth", IsNullable = true)]
        public DateTime? DateOfBirth { get; set; }
    }

}
