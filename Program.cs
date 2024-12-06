using FinalOrm.Attributes;
using FinalOrm.ScriptGenerator;
using GeneratedModels;
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
        var userRepository = RepositoryFactory.Create<Users>();
        var usersRepo = new Repository<Users>(connectionString);
        var us = new Users { Username = "JohnDoe", PasswordHash = "hash1", CreatedAt = DateTime.Now };
        var users = new List<Users>
{
    new Users { Username = "JohnDoe", PasswordHash = "hash1", CreatedAt = DateTime.Now },
    new Users { Username = "JaneDoe", PasswordHash = "hash2", CreatedAt = DateTime.Now },
};
        usersRepo.Create(us);
        usersRepo.Delete(2);
        var xs = usersRepo.ReadAll();
        foreach (var user in xs)
        {
            Console.WriteLine($"Id: {user.Id}, Username: {user.Username}, PasswordHash: {user.PasswordHash}, CreatedAt: {user.CreatedAt}");
        }
        //var modelGenerator = new ModelGenerator();



        //modelGenerator.GenerateModels(connectionString);
    }
}
