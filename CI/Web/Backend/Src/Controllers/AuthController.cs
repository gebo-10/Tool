using Backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly UserDbContext _userDb;

    public AuthController(IConfiguration config, UserDbContext userDb)
    {
        _config = config;
        _userDb = userDb;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("用户名和密码不能为空");

        if (_userDb.Users.Exists(u => u.Username == request.Username))
            return Conflict("用户名已被注册");

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _userDb.Users.Insert(user);
        //await _userDb.SaveChangesAsync();
        return Ok(new { message = "注册成功" });
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var user = _userDb.Users.FindOne(u => u.Username == request.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("用户名或密码错误");

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddYears(100),
            signingCredentials: creds
        );

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), username = user.Username });
    }
}