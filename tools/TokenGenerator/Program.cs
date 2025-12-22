using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

// 使用与服务器相同的配置
var issuer = "issuer";
var audience = "audience";
var secretKey = "Dorisoy-ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
var expiresSeconds = 25920000; // 约 300 天

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

Console.WriteLine("=== Dorisoy Meeting JWT Token Generator ===\n");
Console.WriteLine("配置:");
Console.WriteLine($"  Issuer: {issuer}");
Console.WriteLine($"  Audience: {audience}");
Console.WriteLine($"  SecretKey: {secretKey}");
Console.WriteLine($"  Expires: {expiresSeconds} seconds ({expiresSeconds / 86400} days)\n");
Console.WriteLine("生成的 Token:\n");

// 生成 10 个用户的 Token (0-9)
for (int i = 0; i < 10; i++)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.Name, i.ToString())
    };

    var utcNow = DateTime.UtcNow;
    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: utcNow,
        expires: utcNow.AddSeconds(expiresSeconds),
        signingCredentials: credentials
    );

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
    Console.WriteLine($"// Peer {i}{(i >= 8 ? " (Admin)" : "")}");
    Console.WriteLine($"\"{tokenString}\",\n");
}

Console.WriteLine("\n=== 复制上面的 Token 到 MainViewModel.cs 的 _accessTokens 数组中 ===");
