using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectPlanning.Web.Data;
using ProjectPlanning.Web.Models;
using ProjectPlanning.Web.Services;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

namespace ProjectPlanning.Web.Controllers
{
    public class AuthViewController : Controller
    {
        // /AuthView/Register
        public IActionResult Register()
        {
            return View();
        }

        // /AuthView/Login
        public IActionResult Login()
        {
            return View();
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtService _jwtService;

        public AuthController(ApplicationDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        // REGISTER
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            // ✅ Email already used?
            if (await _context.Users.AnyAsync(u => u.Email == user.Email))
                return BadRequest(new { message = "Email already registered." });

            // ✅ Validate password
            if (!IsValidPassword(user.Password))
                return BadRequest(new { message = "Password must be at least 5 characters long and contain at least one number." });

            // ✅ Hash and save
            user.Password = HashPassword(user.Password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User registered successfully." });
        }

        // LOGIN
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !VerifyPassword(request.Password, user.Password))
                return Unauthorized(new { message = "Invalid credentials." });

            var token = _jwtService.GenerateToken(user);
            return Ok(new { token });
        }

        // PROFILE (protected route)
        [HttpGet("profile")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> Profile()
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.Id,
                user.Email,
                user.Name,
                user.Organization,
                user.IsOfferingOng
            });
        }

        // 🔐 Helper methods
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyPassword(string inputPassword, string storedHash)
        {
            var hash = HashPassword(inputPassword);
            return hash == storedHash;
        }

        // ✅ Password validation helper
        private static bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) return false;
            return password.Length >= 5 && password.Any(char.IsDigit);
        }
    }

    // DTO for login
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
