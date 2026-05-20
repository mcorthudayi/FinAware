using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinAware.API.Data;
using FinAware.API.Models;
using FinAware.API.Services;

namespace FinAware.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            Console.WriteLine($"📝 REGISTER REQUEST: {dto.Email}");

            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                    return BadRequest(new { message = "Bu e-posta adresi zaten kayıtlı!" });

                if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
                    return BadRequest(new { message = "Bu kullanıcı adı zaten kullanılıyor!" });

                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                var verificationToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

                var user = new User
                {
                    Username = dto.Username,
                    Email = dto.Email,
                    PasswordHash = hashedPassword,
                    IsEmailVerified = true,
                    EmailVerificationToken = verificationToken,
                    EmailVerificationTokenExpiry = DateTime.Now.AddHours(24),
                    CreatedAt = DateTime.Now
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var defaultCategories = new List<Category>
                {
                    new Category { UserId = user.UserId, Name = "Market",    Type = "Expense", Icon = "🛒", Color = "#4DB6AC", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Yemek",     Type = "Expense", Icon = "🍔", Color = "#FF9800", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Ulaşım",    Type = "Expense", Icon = "🚗", Color = "#2196F3", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Faturalar", Type = "Expense", Icon = "🧾", Color = "#9C27B0", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Sağlık",    Type = "Expense", Icon = "💊", Color = "#F44336", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Eğlence",   Type = "Expense", Icon = "🎬", Color = "#E91E63", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Giyim",     Type = "Expense", Icon = "👔", Color = "#795548", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Teknoloji", Type = "Expense", Icon = "💻", Color = "#607D8B", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Eğitim",    Type = "Expense", Icon = "📚", Color = "#3F51B5", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Spor",      Type = "Expense", Icon = "⚽", Color = "#4CAF50", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Maaş",      Type = "Income",  Icon = "💰", Color = "#4CAF50", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Ek Gelir",  Type = "Income",  Icon = "💵", Color = "#8BC34A", CreatedAt = DateTime.Now },
                    new Category { UserId = user.UserId, Name = "Diğer",     Type = "Expense", Icon = "📦", Color = "#9E9E9E", CreatedAt = DateTime.Now },
                };

                _context.Categories.AddRange(defaultCategories);
                await _context.SaveChangesAsync();

                try
                {
                    var mvcBaseUrl = _configuration["AppSettings:MvcBaseUrl"];
                    var verificationLink = $"{mvcBaseUrl}/Auth/VerifyEmail?token={verificationToken}";
                    var emailService = new EmailService(_configuration);
                    await emailService.SendEmailVerificationAsync(user.Email, user.Username, verificationLink);
                    Console.WriteLine($"✅ Verification email sent to: {user.Email}");
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"⚠️ Email send failed: {emailEx.Message}");
                }

                Console.WriteLine($"✅ User registered: {user.Username} (ID: {user.UserId})");

                return Ok(new { message = "Kayıt başarılı! Giriş yapabilirsiniz." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Register error: {ex.Message}");
                return StatusCode(500, new { message = "Sunucu hatası" });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            Console.WriteLine($"🔐 LOGIN REQUEST: {dto.Email}");

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

                if (user == null)
                    return Unauthorized(new { message = "E-posta veya şifre hatalı!" });

                if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                    return Unauthorized(new { message = "E-posta veya şifre hatalı!" });

                if (!user.IsEmailVerified)
                    return Unauthorized(new { message = "E-posta adresiniz doğrulanmamış!", emailNotVerified = true });

                var token = GenerateJwtToken(user);

                Console.WriteLine($"✅ Login successful: {user.Username}");
             

                return Ok(new { token, username = user.Username, email = user.Email });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Login error: {ex.Message}");
                return StatusCode(500, new { message = "Sunucu hatası" });
            }
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EmailVerificationToken == token);

                if (user == null)
                    return BadRequest(new { message = "Geçersiz doğrulama linki." });

                if (user.EmailVerificationTokenExpiry < DateTime.Now)
                    return BadRequest(new { message = "Doğrulama linkinin süresi dolmuş." });

                if (user.IsEmailVerified)
                    return Ok(new { message = "E-posta adresiniz zaten doğrulanmış." });

                user.IsEmailVerified = true;
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenExpiry = null;
                await _context.SaveChangesAsync();

                try
                {
                    var emailService = new EmailService(_configuration);
                    await emailService.SendWelcomeEmailAsync(user.Email, user.Username);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"⚠️ Welcome email failed: {emailEx.Message}");
                }

                Console.WriteLine($"✅ Email verified: {user.Email}");
                return Ok(new { message = "E-posta adresiniz başarıyla doğrulandı! Giriş yapabilirsiniz." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Verify email error: {ex.Message}");
                return StatusCode(500, new { message = "Doğrulama sırasında hata oluştu" });
            }
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

                if (user == null)
                    return BadRequest(new { message = "Bu e-posta ile kayıtlı kullanıcı bulunamadı." });

                if (user.IsEmailVerified)
                    return BadRequest(new { message = "E-posta adresiniz zaten doğrulanmış." });

                var verificationToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                user.EmailVerificationToken = verificationToken;
                user.EmailVerificationTokenExpiry = DateTime.Now.AddHours(24);
                await _context.SaveChangesAsync();

                var mvcBaseUrl = _configuration["AppSettings:MvcBaseUrl"];
                var verificationLink = $"{mvcBaseUrl}/Auth/VerifyEmail?token={verificationToken}";
                var emailService = new EmailService(_configuration);
                await emailService.SendEmailVerificationAsync(user.Email, user.Username, verificationLink);

                return Ok(new { message = "Doğrulama maili tekrar gönderildi." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Resend verification error: {ex.Message}");
                return StatusCode(500, new { message = "Mail gönderilemedi" });
            }
        }

        // Bot için link token üret ve Telegram ile hesap bağlama işlemi
        [HttpPost("generate-link-token")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GenerateLinkToken()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound();

                // Yeni token üret (10 dakika geçerli)
                var linkToken = Guid.NewGuid().ToString("N")[..12].ToUpper();
                user.TelegramLinkToken = linkToken;
                await _context.SaveChangesAsync();

                var botUsername = "FinAwareAsistanBot";
                var deepLink = $"https://t.me/{botUsername}?start={linkToken}";

                Console.WriteLine($"✅ Link token generated for {user.Username}: {linkToken}");

                return Ok(new
                {
                    linkToken,
                    deepLink,
                    botUsername,
                    message = "Link token oluşturuldu"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Generate link token error: {ex.Message}");
                return StatusCode(500, new { message = "Token oluşturulamadı" });
            }
        }

        // Bot'un kullanıcıyı doğrulaması için Telegram'dan gelen istekleri işle
        [HttpPost("bot-link")]
        public async Task<IActionResult> BotLink([FromBody] BotLinkDto dto)
        {
            try
            {
                // Bot secret kontrolü
                var botSecret = _configuration["FinAwareApi:BotSecret"];
                if (dto.BotSecret != botSecret)
                    return Unauthorized(new { message = "Yetkisiz erişim" });

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.TelegramLinkToken == dto.LinkToken);

                if (user == null)
                    return NotFound(new { message = "Geçersiz veya süresi dolmuş token" });

                // Bağlantıyı kaydet
                user.TelegramChatId = dto.TelegramChatId;
                user.TelegramLinkedAt = DateTime.Now;
                user.TelegramLinkToken = null;  
                await _context.SaveChangesAsync();

                // Bot için JWT üret
                var jwtToken = GenerateJwtToken(user);

                Console.WriteLine($"✅ Telegram linked: {user.Username} ↔ ChatId:{dto.TelegramChatId}");

                return Ok(new
                {
                    userId = user.UserId,
                    username = user.Username,
                    email = user.Email,
                    token = jwtToken,
                    message = "Hesap başarıyla bağlandı!"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Bot link error: {ex.Message}");
                return StatusCode(500, new { message = "Bağlantı kurulamadı" });
            }
        }

        // Bot'un chatId ile kullanıcı sorgulaması
        [HttpPost("bot-resolve")]
        public async Task<IActionResult> BotResolve([FromBody] BotResolveDto dto)
        {
            try
            {
                var botSecret = _configuration["FinAwareApi:BotSecret"];
                if (dto.BotSecret != botSecret)
                    return Unauthorized(new { message = "Yetkisiz erişim" });

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.TelegramChatId == dto.TelegramChatId);

                if (user == null)
                    return NotFound(new { message = "Bu Telegram hesabına bağlı kullanıcı yok" });

                var jwtToken = GenerateJwtToken(user);

                return Ok(new
                {
                    userId = user.UserId,
                    username = user.Username,
                    email = user.Email,
                    token = jwtToken
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Bot resolve error: {ex.Message}");
                return StatusCode(500, new { message = "Kullanıcı sorgulanamadı" });
            }
        }

        //  Telegram bağlantısını kopar
        [HttpPost("unlink-telegram")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> UnlinkTelegram()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound();

                user.TelegramChatId = null;
                user.TelegramLinkedAt = null;
                user.TelegramLinkToken = null;
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Telegram unlinked: {user.Username}");
                return Ok(new { message = "Telegram bağlantısı kaldırıldı" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Unlink error: {ex.Message}");
                return StatusCode(500, new { message = "Bağlantı kaldırılamadı" });
            }
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(7),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        // Health check – Keep-alive ve MAUI için
        [HttpGet("/api/health")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new { status = "alive", time = DateTime.Now });
        }
    }

    public class RegisterDto { public string Username { get; set; } = ""; public string Email { get; set; } = ""; public string Password { get; set; } = ""; }
    public class LoginDto { public string Email { get; set; } = ""; public string Password { get; set; } = ""; }
    public class ResendVerificationDto { public string Email { get; set; } = ""; }
    public class BotLinkDto { public string BotSecret { get; set; } = ""; public string LinkToken { get; set; } = ""; public long TelegramChatId { get; set; } }
    public class BotResolveDto { public string BotSecret { get; set; } = ""; public long TelegramChatId { get; set; } }
}