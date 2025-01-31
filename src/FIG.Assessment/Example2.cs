using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIG.Assessment;

/// <summary>
///     Handles user authentication.
/// </summary>
public class Example2(UserDbContextE2 dbContextE2, IPasswordHasher<UserE2> passwordHasher, ILogger<Example2> logger)
    : Controller
{
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromForm] LoginPostModel model)
    {
        if (string.IsNullOrWhiteSpace(model.UserName) || string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(string.Empty, "Username and password are required.");
            return View(model);
        }

        var user = await dbContextE2.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == model.UserName);

        if (user == null)
        {
            logger.LogWarning("Invalid login attempt for username: {UserName}", model.UserName);
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(model);
        }

        var passwordVerification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (passwordVerification != PasswordVerificationResult.Success)
        {
            logger.LogWarning("Invalid password attempt for user ID: {UserId}", user.UserId);
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        return LocalRedirect(model.ReturnUrl ?? "/");
    }
}

/// <summary>
///     Represents the login form model.
/// </summary>
public class LoginPostModel
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}

#region Database Entires

/// <summary>
///     Represents a User entity in the database for Example 2.
/// </summary>
public abstract class UserE2
{
    [Key]
    public required int UserId { get; init; }

    [MaxLength(50)] public string UserName { get; set; } = string.Empty;

    [MaxLength(128)] public string PasswordHash { get; set; } = string.Empty;
}

/// <summary>
///     Entity Framework Core database context for User authentication for Example2.
/// </summary>
public class UserDbContextE2(DbContextOptions<UserDbContextE2> options) : DbContext(options)
{
    public DbSet<UserE2> Users { get; set; }
}

#endregion