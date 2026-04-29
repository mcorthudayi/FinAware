using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinAware.API.Data;
using System.Security.Claims;

namespace FinAware.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/user/profile endpoint'i kullanıcı profil bilgilerini dönderir   
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var user = await _context.Users.FindAsync(int.Parse(userId));
                if (user == null)
                    return NotFound(new { message = "Kullanıcı bulunamadı" });

                return Ok(new
                {
                    username = user.Username,
                    email = user.Email,
                    profilePhoto = user.ProfilePhoto,
                    emailNotificationsEnabled = user.EmailNotificationsEnabled,
                    telegramLinked = user.TelegramChatId.HasValue,
                    telegramLinkedAt = user.TelegramLinkedAt?.ToString("dd.MM.yyyy HH:mm")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Get profile error: {ex.Message}");
                return StatusCode(500, new { message = "Sunucu hatası" });
            }
        }

        // POST: api/user/upload-profile-photo endpoint'i kullanıcıların profil fotoğrafı yüklemesini sağlar
        [HttpPost("upload-profile-photo")]
        [RequestSizeLimit(5_242_880)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadProfilePhoto(IFormFile file)
        {
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("📸 API: PROFILE PHOTO UPLOAD REQUEST");

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "Dosya seçilmedi" });

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    return BadRequest(new { message = "Sadece JPG, PNG veya WEBP!" });

                if (file.Length > 5 * 1024 * 1024)
                    return BadRequest(new { message = "Maksimum 5MB!" });

                var tempFolder = Path.Combine(Path.GetTempPath(), "FinAware", "profiles");
                if (!Directory.Exists(tempFolder))
                    Directory.CreateDirectory(tempFolder);

                var fileExtension = Path.GetExtension(file.FileName);
                var fileName = $"{userId}_{DateTime.Now.Ticks}{fileExtension}";
                var filePath = Path.Combine(tempFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var user = await _context.Users.FindAsync(int.Parse(userId));
                if (user == null)
                    return NotFound(new { message = "Kullanıcı bulunamadı" });

                if (!string.IsNullOrEmpty(user.ProfilePhoto))
                {
                    var oldPath = Path.Combine(tempFolder, Path.GetFileName(user.ProfilePhoto));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                user.ProfilePhoto = fileName;
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Photo uploaded: {fileName}");
                Console.WriteLine("═══════════════════════════════════════");

                return Ok(new
                {
                    message = "Başarılı!",
                    photoUrl = $"/api/user/photo/{fileName}"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: {ex.Message}");
                Console.WriteLine("═══════════════════════════════════════");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // GET: api/user/photo/{filename} endpoint'i kullanıcıların profil fotoğrafını görüntülemesini sağlar
        [HttpGet("photo/{filename}")]
        [AllowAnonymous]
        public IActionResult GetPhoto(string filename)
        {
            try
            {
                var tempFolder = Path.Combine(Path.GetTempPath(), "FinAware", "profiles");
                var filePath = Path.Combine(tempFolder, filename);

                if (!System.IO.File.Exists(filePath))
                    return NotFound();

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var extension = Path.GetExtension(filename).ToLower();
                var contentType = extension switch
                {
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "application/octet-stream"
                };

                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Get photo error: {ex.Message}");
                return NotFound();
            }
        }

        // PUT: api/user/update-profile endpoint'i kullanıcıların profil bilgilerini güncellemesini sağlar
        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var user = await _context.Users.FindAsync(int.Parse(userId));
                if (user == null)
                    return NotFound(new { message = "Kullanıcı bulunamadı" });

                if (dto.Email != user.Email)
                {
                    var emailExists = await _context.Users.AnyAsync(u => u.Email == dto.Email && u.UserId != user.UserId);
                    if (emailExists)
                        return BadRequest(new { message = "E-posta kullanılıyor" });
                    user.Email = dto.Email;
                }

                if (dto.Username != user.Username)
                {
                    var usernameExists = await _context.Users.AnyAsync(u => u.Username == dto.Username && u.UserId != user.UserId);
                    if (usernameExists)
                        return BadRequest(new { message = "Kullanıcı adı kullanılıyor" });
                    user.Username = dto.Username;
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Güncellendi!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Update error: {ex.Message}");
                return StatusCode(500, new { message = "Hata" });
            }
        }

        // POST: api/user/change-password endpoint'i kullanıcıların şifrelerini değiştirmesini sağlar
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var user = await _context.Users.FindAsync(int.Parse(userId));
                if (user == null)
                    return NotFound(new { message = "Kullanıcı bulunamadı" });

                if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                    return BadRequest(new { message = "Mevcut şifre yanlış" });

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Şifre değiştirildi!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Password error: {ex.Message}");
                return StatusCode(500, new { message = "Hata" });
            }
        }

        // POST: api/user/toggle-email-notifications endpoint'i kullanıcıların e-posta bildirimlerini açıp kapatmasını sağlar
        [HttpPost("toggle-email-notifications")]
        public async Task<IActionResult> ToggleEmailNotifications()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var user = await _context.Users.FindAsync(int.Parse(userId));
                if (user == null)
                    return NotFound(new { message = "Kullanıcı bulunamadı" });

                user.EmailNotificationsEnabled = !user.EmailNotificationsEnabled;
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Email notifications {(user.EmailNotificationsEnabled ? "enabled" : "disabled")}: {user.Username}");

                return Ok(new
                {
                    message = user.EmailNotificationsEnabled ? "E-posta bildirimleri açıldı" : "E-posta bildirimleri kapatıldı",
                    emailNotificationsEnabled = user.EmailNotificationsEnabled
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Toggle notifications error: {ex.Message}");
                return StatusCode(500, new { message = "Bir hata oluştu" });
            }
        }

        // DELETE: api/user/delete-account endpoint'i kullanıcıların hesaplarını silmesini sağlar
        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var user = await _context.Users.FindAsync(int.Parse(userId));
                if (user == null)
                    return NotFound(new { message = "Kullanıcı bulunamadı" });

                if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                    return BadRequest(new { message = "Şifre hatalı. Hesap silinmedi." });

                //  1. Budgets (Category'den önce silinmeli - FK_Budgets_Categories)
                var budgets = _context.Budgets.Where(b => b.UserId == user.UserId);
                _context.Budgets.RemoveRange(budgets);

                //  2. SavingTransactions (Savings'den önce silinmeli - FK cascade)
                var savingIds = await _context.Savings
                    .Where(s => s.UserId == user.UserId)
                    .Select(s => s.SavingId)
                    .ToListAsync();
                var savingTransactions = _context.SavingTransactions
                    .Where(st => savingIds.Contains(st.SavingId));
                _context.SavingTransactions.RemoveRange(savingTransactions);

                //  3. Transactions
                var transactions = _context.Transactions.Where(t => t.UserId == user.UserId);
                _context.Transactions.RemoveRange(transactions);

                //  4. Savings
                var savings = _context.Savings.Where(s => s.UserId == user.UserId);
                _context.Savings.RemoveRange(savings);

                //  5. Notifications
                var notifications = _context.Notifications.Where(n => n.UserId == user.UserId);
                _context.Notifications.RemoveRange(notifications);

                //  6. Categories (Budget'tan sonra)
                var categories = _context.Categories.Where(c => c.UserId == user.UserId);
                _context.Categories.RemoveRange(categories);

                //  7. User
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Account deleted: {user.Username}");
                return Ok(new { message = "Hesap başarıyla silindi." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Delete account error: {ex.Message}");
                return StatusCode(500, new { message = "Hesap silinirken hata oluştu" });
            }
        }
    }

    public class UpdateProfileDto
    {
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class DeleteAccountDto
    {
        public string Password { get; set; } = "";
    }
}