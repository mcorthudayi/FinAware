using FinAware.API.Data;
using FinAware.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace FinAware.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IyzicoService _iyzico;
        private readonly IConfiguration _configuration;

        public SubscriptionController(
            AppDbContext context,
            IyzicoService iyzico,
            IConfiguration configuration)
        {
            _context = context;
            _iyzico = iyzico;
            _configuration = configuration;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // Mevcut plan bilgisi 
        [HttpGet("my-plan")]
        public async Task<IActionResult> GetMyPlan()
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // Süre dolmuşsa Free'ye düşür
            if (user.SubscriptionPlan != "Free" &&
                user.SubscriptionExpiry.HasValue &&
                user.SubscriptionExpiry < DateTime.Now)
            {
                user.SubscriptionPlan = "Free";
                user.SubscriptionExpiry = null;
                await _context.SaveChangesAsync();
            }

            // Aylık kullanım sıfırlama
            if (user.UsageResetDate.Month != DateTime.Now.Month ||
                user.UsageResetDate.Year != DateTime.Now.Year)
            {
                user.OcrUsageThisMonth = 0;
                user.ArisUsageThisMonth = 0;
                user.UsageResetDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                plan = user.SubscriptionPlan,
                expiry = user.SubscriptionExpiry?.ToString("dd.MM.yyyy"),
                ocrUsage = user.OcrUsageThisMonth,
                arisUsage = user.ArisUsageThisMonth,
                ocrLimit = user.SubscriptionPlan == "Platinum" ? -1 : user.SubscriptionPlan == "Gold" ? 60 : 0,
                arisLimit = user.SubscriptionPlan == "Platinum" ? -1 : user.SubscriptionPlan == "Gold" ? 30 : 0,
                canUseExport = user.SubscriptionPlan != "Free",
                canUseTelegram = user.SubscriptionPlan != "Free",
                canUseOcr = user.SubscriptionPlan != "Free",
                canUseAris = user.SubscriptionPlan != "Free"
            });
        }

        //  Ödeme başlat
        [HttpPost("initialize")]
        public async Task<IActionResult> Initialize([FromBody] InitializeSubscriptionDto dto)
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (dto.Plan != "Gold" && dto.Plan != "Platinum")
                return BadRequest(new { message = "Geçersiz plan." });

            var mvcBase = _configuration["AppSettings__MvcBaseUrl"]
                           ?? _configuration["AppSettings:MvcBaseUrl"]
                           ?? "https://localhost:7023";
            var callbackUrl = $"{mvcBase}/Subscription/Callback";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "85.34.78.112";

            var result = await _iyzico.InitializeCheckoutFormAsync(
                dto.Plan, userId, user.Email, user.Username, callbackUrl, ip);

            if (result.Status != "success")
                return BadRequest(new { message = "Ödeme başlatılamadı." });

            // Token'ı geçici olarak sakla
            user.TelegramLinkToken = $"iyzico_{result.Token}_{dto.Plan}";
            await _context.SaveChangesAsync();

            return Ok(new
            {
                checkoutFormContent = result.CheckoutFormContent,
                token = result.Token
            });
        }

        // Callback (iyzico ödeme sonrası döner)
        [HttpPost("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback([FromBody] CallbackDto dto)
        {
            try
            {
                var result = await _iyzico.RetrieveCheckoutFormAsync(dto.Token);

                if (!result.Success)
                    return Ok(new { success = false, message = "Ödeme başarısız." });

                // ConversationId: finaware_{userId}_{plan}_{timestamp}
                var parts = result.ConversationId.Split('_');
                if (parts.Length < 3 || !int.TryParse(parts[1], out int userId))
                    return BadRequest(new { message = "Geçersiz conversationId." });

                var plan = parts[2]; // Gold veya Platinum
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound();

                user.SubscriptionPlan = plan;
                user.SubscriptionExpiry = DateTime.Now.AddMonths(1);
                user.TelegramLinkToken = null;
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Subscription activated: {user.Email} → {plan}");
                return Ok(new { success = true, plan });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Subscription callback error: {ex.Message}");
                return StatusCode(500, new { message = "Sunucu hatası." });
            }
        }

        // ARİS kullanım kontrolü 
        [HttpPost("check-aris")]
        public async Task<IActionResult> CheckAris()
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            if (user.SubscriptionPlan == "Free")
                return Ok(new { allowed = false, message = "ARİS Gold veya Platinum plana özel." });

            if (user.SubscriptionPlan == "Gold" && user.ArisUsageThisMonth >= 30)
                return Ok(new { allowed = false, message = "Bu ay ARİS limitine ulaştın (30/30). Platinum'a geçerek sınırsız kullanabilirsin." });

            user.ArisUsageThisMonth++;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                allowed = true,
                usage = user.ArisUsageThisMonth,
                limit = user.SubscriptionPlan == "Platinum" ? -1 : 30
            });
        }

        // OCR kullanım kontrolü
        [HttpPost("check-ocr")]
        public async Task<IActionResult> CheckOcr()
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            if (user.SubscriptionPlan == "Free")
                return Ok(new { allowed = false, message = "Fatura analizi Gold veya Platinum plana özel." });

            if (user.SubscriptionPlan == "Gold" && user.OcrUsageThisMonth >= 60)
                return Ok(new { allowed = false, message = "Bu ay OCR limitine ulaştın (60/60). Platinum'a geçerek sınırsız kullanabilirsin." });

            user.OcrUsageThisMonth++;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                allowed = true,
                usage = user.OcrUsageThisMonth,
                limit = user.SubscriptionPlan == "Platinum" ? -1 : 60
            });
        }

        //  İptal
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel()
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            user.SubscriptionPlan = "Free";
            user.SubscriptionExpiry = null;
            await _context.SaveChangesAsync();

            Console.WriteLine($"❌ Subscription cancelled: {user.Email}");
            return Ok(new { message = "Abonelik iptal edildi. Dönem sonuna kadar kullanabilirsin." });
        }
    }

    public class InitializeSubscriptionDto
    {
        public string Plan { get; set; } = "";
    }
    public class CallbackDto
    {
        public string Token { get; set; } = "";
    }
}