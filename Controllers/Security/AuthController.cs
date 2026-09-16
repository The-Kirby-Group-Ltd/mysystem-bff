namespace mysystem_bff.Controllers.Security;

using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Auth;
using mysystem_bff.Models.Portal;
using mysystem_bff.Services.Interfaces.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MySqlConnection _db;
    private readonly IMiddlewareAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(MySqlConnection db, IMiddlewareAuthService authService, IConfiguration configuration)
    {
        _db = db;
        _authService = authService;
        _configuration = configuration;
    }

    // =======================================================
    // log in a user
    // =======================================================

    [HttpPost("login")]
    [EnableRateLimiting("Login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _db.QuerySingleOrDefaultAsync<UserLoginRow>(
            """
            SELECT 
                CAST(user_id AS CHAR) AS UserId,
                username AS Username,
                email AS Email,
                password_hash AS PasswordHash,
                first_name AS FirstName,
                last_name AS LastName,
                is_active AS IsActive
            FROM users
            WHERE username = @Username OR email = @Username
            LIMIT 1;
            """,
            new { request.Username }
        );

        if (user is null)
            return Unauthorized("Invalid username or password.");

        if (!user.IsActive)
            return Unauthorized("User account is inactive.");

        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!passwordValid)
            return Unauthorized("Invalid username or password.");

        var roles = (await _db.QueryAsync<string>(
            """
            SELECT r.role_name
            FROM roles r
            INNER JOIN user_roles ur ON ur.role_id = r.role_id
            WHERE CAST(ur.user_id AS CHAR) = @UserId;
            """,
            new { user.UserId }
        )).ToList();

        if (!roles.Any())
            return Unauthorized("User has no assigned role.");

        var token = GenerateJwt(user, roles);

        return Ok(new LoginResponse
        {
            Token = token,
            User = new AuthUserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles
            }
        });
    }

    // =======================================================
    // logged in user details
    // =======================================================

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<PortalUserDetails>> Me()
    {
        var user = new AuthUserDto
        {
            UserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier) ?? "",

            Username =
                User.FindFirstValue(
                    ClaimTypes.Name) ?? "",

            Email =
                User.FindFirstValue(
                    ClaimTypes.Email) ?? "",

            FirstName =
                User.FindFirstValue(
                    "firstName") ?? "",

            LastName =
                User.FindFirstValue(
                    "lastName") ?? "",

            Roles =
                User.FindAll(
                        ClaimTypes.Role)
                    .Select(role =>
                        role.Value)
                    .ToList()
        };

        var result =
            await _authService
                .GetUserDetailsAsync(user);

        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode,
                result.Error);
        }

        return Ok(
            result.Data);
    }

    // =======================================================
    // generate backend auth token
    // =======================================================

    private string GenerateJwt(UserLoginRow user, List<string> roles)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new Exception("JWT key is missing.");

        var expiryMinutes = int.Parse(
            _configuration["Jwt:ExpiryMinutes"] ?? "120"
        );

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("firstName", user.FirstName),
            new("lastName", user.LastName)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private class UserLoginRow
    {
        public string UserId { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public bool IsActive { get; set; }
    }
}