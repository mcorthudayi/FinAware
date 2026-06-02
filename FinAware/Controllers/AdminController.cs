using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinAware.API.Data;
using System.Security.Claims;

namespace FinAware.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.IsEmailVerified,
                    u.EmailNotificationsEnabled,
                    u.CreatedAt,
                    u.TelegramChatId,
                    u.SubscriptionPlan,          
                    u.SubscriptionExpiry,        
                    u.OcrUsageThisMonth,         
                    u.ArisUsageThisMonth,        
                    TransactionCount = _context.Transactions.Count(t => t.UserId == u.UserId),
                    IsFrozen = u.Role == "Frozen"
                })
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("users/{id}/change-plan")]
        public async Task<IActionResult> ChangePlan(int id, [FromBody] ChangePlanDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var validPlans = new[] { "Free", "Gold", "Platinum" };
            if (!validPlans.Contains(dto.Plan))
                return BadRequest(new { message = "Geçersiz plan" });

            user.SubscriptionPlan = dto.Plan;
            user.SubscriptionExpiry = dto.Plan == "Free"
                ? null
                : DateTime.Now.AddMonths(dto.Months);

            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ Plan changed: {user.Email} → {dto.Plan}");
            return Ok(new { message = $"{user.Username} planı {dto.Plan} olarak güncellendi" });
        }

        [HttpGet("subscription-stats")]
        public async Task<IActionResult> GetSubscriptionStats()
        {
            var total = await _context.Users.CountAsync();
            var free = await _context.Users.CountAsync(u => u.SubscriptionPlan == "Free");
            var gold = await _context.Users.CountAsync(u => u.SubscriptionPlan == "Gold");
            var platinum = await _context.Users.CountAsync(u => u.SubscriptionPlan == "Platinum");

            return Ok(new { total, free, gold, platinum });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var verifiedUsers = await _context.Users.CountAsync(u => u.IsEmailVerified);
            var totalTransactions = await _context.Transactions.CountAsync();
            var telegramLinked = await _context.Users.CountAsync(u => u.TelegramChatId != null);
            var totalIncome = await _context.Transactions
                .Where(t => t.Type == "Income").SumAsync(t => t.Amount);
            var totalExpense = await _context.Transactions
                .Where(t => t.Type == "Expense").SumAsync(t => t.Amount);
            var newUsersThisMonth = await _context.Users
                .CountAsync(u => u.CreatedAt.Month == DateTime.Now.Month
                              && u.CreatedAt.Year == DateTime.Now.Year);

            return Ok(new
            {
                totalUsers,
                verifiedUsers,
                totalTransactions,
                telegramLinked,
                totalIncome,
                totalExpense,
                newUsersThisMonth
            });
        }

        [HttpPost("users/{id}/freeze")]
        public async Task<IActionResult> FreezeUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (user.UserId == adminId)
                return BadRequest(new { message = "Kendinizi donduramazsınız" });

            user.Role = "Frozen";
            await _context.SaveChangesAsync();
            Console.WriteLine($"🧊 User frozen: {user.Email}");
            return Ok(new { message = $"{user.Username} donduruldu" });
        }

        [HttpPost("users/{id}/unfreeze")]
        public async Task<IActionResult> UnfreezeUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Role = "User";
            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ User unfrozen: {user.Email}");
            return Ok(new { message = $"{user.Username} aktif edildi" });
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            if (user.UserId == adminId)
                return BadRequest(new { message = "Kendinizi silemezsiniz" });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            Console.WriteLine($"🗑️ User deleted: {user.Email}");
            return Ok(new { message = $"{user.Username} silindi" });
        }

        [HttpPost("users/{id}/make-admin")]
        public async Task<IActionResult> MakeAdmin(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Role = "Admin";
            await _context.SaveChangesAsync();
            return Ok(new { message = $"{user.Username} admin yapıldı" });
        }

        [HttpGet("users/{id}/transactions")]
        public async Task<IActionResult> GetUserTransactions(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var transactions = await _context.Transactions
                .Where(t => t.UserId == id)
                .Include(t => t.Category)
                .OrderByDescending(t => t.Date)
                .Select(t => new
                {
                    t.TransactionId,
                    t.Amount,
                    t.Date,
                    t.Description,
                    t.Type,
                    CategoryName = t.Category.Name
                })
                .ToListAsync();

            return Ok(new { user = new { user.Username, user.Email }, transactions });
        }
    }
    public class ChangePlanDto
    {
        public string Plan { get; set; } = "Free";
        public int Months { get; set; } = 1;
    }
}