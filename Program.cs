using FinalOrm.Attributes;
using FinalOrm.ScriptGenerator;
using System.Reflection;

//[Table("Users")]
//public class User
//{
//    [PrimaryKey(IsIdentity = true)]
//    [Column("Id", IsNullable = false)]
//    public int Id { get; set; }

//    [Column("Username", IsNullable = false, Length = 50)]
//    [Unique]
//    public string Username { get; set; }

//    [Column("PasswordHash", IsNullable = false, Length = 256)]
//    public string PasswordHash { get; set; }

//    [Column("CreatedAt", IsNullable = false)]
//    [DefaultValue("GETDATE()")]
//    public DateTime CreatedAt { get; set; }
//}

//[Table("Posts")]
//public class Post
//{
//    [PrimaryKey(IsIdentity = true)]
//    [Column("Id", IsNullable = false)]
//    public int Id { get; set; }

//    [Column("Title", IsNullable = false, Length = 100)]
//    public string Title { get; set; }

//    [Column("Content", IsNullable = false)]
//    public string Content { get; set; }

//    [ForeignKey("Users", "Id")]
//    [Column("AuthorId", IsNullable = false)]
//    public int AuthorId { get; set; }
//}

class Program
{
    static void Main()
    {
        string connectionString = "Server=COGNINE-L105;Database=bb2;Trusted_Connection=True;Trust Server Certificate=True;";
        DatabaseHelper.VerifyAndGenerateScripts(connectionString);
        //var modelGenerator = new ModelGenerator();



        //modelGenerator.GenerateModels(connectionString);
    }
}
