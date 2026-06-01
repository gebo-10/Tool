using LiteDB;

namespace Backend.Data;

public class UserDbContext
{
    public LiteDatabase Database { get; }

    public UserDbContext(string connectionString)
    {
        Database = new LiteDatabase(connectionString);
    }

    // 可选：直接暴露 User 集合的快捷方法
    public ILiteCollection<User> Users => Database.GetCollection<User>("users");
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "user";
}


// ---------- 请求 DTO ----------
public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}