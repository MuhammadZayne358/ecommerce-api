using ECommerce.API.Data;
using ECommerce.API.DTOs;
using ECommerce.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using ECommerce.API.Services;

namespace ECommerce.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;

        public AuthController(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {

                throw new UnauthorizedAccessException("Invalid user token");
            }
            return userId;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<AuthResponseDto>> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user token"
                });
            }
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found"
                });
            }
            return Ok(new AuthResponseDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role
            });
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register(
            RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName))
            {
                return BadRequest(new
                {
                    message = "Full name is required"
                });
            }

            if(string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(new
                {
                    message = "Email is required;"
                });
            }
            if(string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            {
                return BadRequest(new
                {
                    message = "Password must contain atleast 6 characters"
                });
            }
            var email = dto.Email.Trim().ToLowerInvariant();

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (existingUser != null)
            {
                return Conflict(new
                {
                    message = "An account with this email already exist."
                });
            }
            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Email = email,
                Role = "Customer",
                IsActive = true
            };
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                dto.Password);

            _context.Users.Add(user);
          await  _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            Response.Cookies.Append(
                "token",
                token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(60)
                });

            return Ok(new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
            });
        }

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName))
                return BadRequest(new { message = "Full name is required" });

            var userId = GetUserId();

            var user = await _context.Users.FindAsync(userId);

            if(user == null)
            
                return NotFound(new { message = "User not found." });

            user.FullName = dto.FullName.Trim();
            await _context.SaveChangesAsync();

            return Ok(new { fullName = user.FullName });
            
        }

        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChnagePassword(ChangePasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                return BadRequest(new { message = "New password must contain at least 6 characters" });

            var userId = GetUserId();

            var user = await _context.Users.FindAsync(userId);

            if (user == null)
                return NotFound(new { message = "User not found" });

            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                dto.CurrentPassword);

            if (result == PasswordVerificationResult.Failed)
                return BadRequest(new { message = "Current password is incorrect." });

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password changes successfully." });
        }

        private static string HasToken(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgetPassword(
            ForgotPasswordDto dto,
            [FromServices] EmailService emailService)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user != null && user.IsActive)
            {
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

                user.PasswordResetToken = HasToken(token);
                user.PasswordResetExpiry = DateTime.UtcNow.AddMinutes(30);
                await _context.SaveChangesAsync();

                var link = $"{_configuration["App:FrontendUrl"]}/reset-password" +
                           $"?email={Uri.EscapeDataString(email)}&token={token}";

                try
                {
                    await emailService.SendAsync(
                        email,
                        "Reset your Aura Scents password",
                        $"<p>Click the link below to reset your password. It expires in 30 minutes.</p>" +
                        $"<p><a href=\"{link}\">Reset password</a></p>");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Email error: " + ex.Message);
                }
            }

            return Ok(new { message = "If an account exists for this email, a reset link has been sent." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Password must contain at least 6 characters." });

            var email = dto.Email.Trim().ToLowerInvariant();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if(user == null || 
                user.PasswordResetToken == null ||
                user.PasswordResetExpiry == null ||
                user.PasswordResetExpiry < DateTime.UtcNow || 
                user.PasswordResetToken != HasToken(dto.Token))
            {
                return BadRequest(new { message = "This reset link is invalid or has expired." });
            }
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetExpiry = null;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Password reset successfully."});
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(
            LoginDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }
            if (!user.IsActive)
            {
                return Unauthorized(new
                {
                    message = "This account is inActive."
                });
            }

            var passwordVerify = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                dto.Password);

            if(passwordVerify == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            var token = GenerateJwtToken(user);

            Response.Cookies.Append("token", token, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None, Expires = DateTimeOffset.UtcNow.AddMinutes(60)});

            return Ok(new AuthResponseDto
            {
                Token = token,
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
            });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("token");
            return Ok(new
            {
                message = "Logged out"
            });
        }

        private string GenerateJwtToken(User user)
        {
            var Key = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "Jwt key is not configured.");

            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role),
            };

            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Key));

            var credentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

            var expirationMinutes =
                _configuration.GetValue<int>("Jwt:ExpirationMinutes");

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}

