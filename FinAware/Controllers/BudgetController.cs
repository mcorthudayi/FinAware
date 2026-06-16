using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinAware.API.Data;
using FinAware.API.Models;
using FinAware.API.Services;
using System.Security.Claims;

namespace FinAware.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BudgetController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public BudgetController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // GET: api/budget?month=4&year=2026 endpoint'i belirtilen ay ve yıl için bütçeleri ve harcamaları getirir
        [HttpGet]
        public async Task<IActionResult> GetBudgets([FromQuery] int? month, [FromQuery] int? year)
        {
            var userId = GetUserId();
            var targetMonth = month ?? DateTime.Now.Month;
            var targetYear = year ?? DateTime.Now.Year;

            var budgets = await _context.Budgets
                .Where(b => b.UserId == userId && b.Month == targetMonth && b.Year == targetYear)
                .Include(b => b.Category)
                .ToListAsync();

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId &&
                            t.Type == "Expense" &&
                            t.Date.Month == targetMonth &&
                            t.Date.Year == targetYear)
                .ToListAsync();

            var totalExpense = transactions.Sum(t => t.Amount);

            var result = budgets.Select(b =>
            {
                decimal spent = b.CategoryId.HasValue
                    ? transactions.Where(t => t.CategoryId == b.CategoryId).Sum(t => t.Amount)
                    : totalExpense;

                var percentage = b.LimitAmount > 0 ? (spent / b.LimitAmount * 100) : 0;

                return new
                {
                    b.BudgetId,
                    b.LimitAmount,
                    b.Month,
                    b.Year,
                    b.CategoryId,
                    CategoryName = b.Category?.Name ?? "Genel Bütçe",
                    CategoryIcon = b.Category?.Icon ?? "💰",
                    Spent = spent,
                    Remaining = b.LimitAmount - spent,
                    Percentage = Math.Round(percentage, 1),
                    IsOver = spent > b.LimitAmount,
                    IsWarning = percentage >= 80 && percentage < 100,
                    IsInfo = percentage >= 50 && percentage < 80
                };
            }).ToList();

            return Ok(new
            {
                budgets = result,
                totalExpense,
                month = targetMonth,
                year = targetYear
            });
        }

        // POST: api/budget endpoint hem yeni bütçe oluşturur hem de var olanı günceller
        [HttpPost]
        public async Task<IActionResult> CreateBudget([FromBody] BudgetDto dto)
        {
            var userId = GetUserId();

            var existing = await _context.Budgets
                .FirstOrDefaultAsync(b => b.UserId == userId &&
                                          b.Month == dto.Month &&
                                          b.Year == dto.Year &&
                                          b.CategoryId == dto.CategoryId);

            if (existing != null)
            {
                existing.LimitAmount = dto.LimitAmount;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Bütçe güncellendi!", budgetId = existing.BudgetId });
            }

            var budget = new Budget
            {
                UserId = userId,
                CategoryId = dto.CategoryId,
                LimitAmount = dto.LimitAmount,
                Month = dto.Month,
                Year = dto.Year,
                CreatedAt = DateTime.Now
            };

            _context.Budgets.Add(budget);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Bütçe oluşturuldu!", budgetId = budget.BudgetId });
        }

        // DELETE: api/budget/5 endpoint bütçeyi siler
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            var userId = GetUserId();

            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.BudgetId == id && b.UserId == userId);

            if (budget == null)
                return NotFound(new { message = "Bütçe bulunamadı" });

            _context.Budgets.Remove(budget);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Bütçe silindi" });
        }

        // POST: api/budget/check endpoint'i aylık bütçe durumunu kontrol eder ve bildirimler oluşturur
        [HttpPost("check")]
        public async Task<IActionResult> CheckBudget()
        {
            var userId = GetUserId();
            var now = DateTime.Now;

            var budgets = await _context.Budgets
                .Where(b => b.UserId == userId && b.Month == now.Month && b.Year == now.Year)
                .Include(b => b.Category)
                .ToListAsync();

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId &&
                            t.Type == "Expense" &&
                            t.Date.Month == now.Month &&
                            t.Date.Year == now.Year)
                .ToListAsync();

            var totalExpense = transactions.Sum(t => t.Amount);
            var warnings = new List<object>();
            var user = await _context.Users.FindAsync(userId);

            Console.WriteLine($"📧 Email enabled: {user?.EmailNotificationsEnabled}, Verified: {user?.IsEmailVerified}");

            foreach (var budget in budgets)
            {
                decimal spent = budget.CategoryId.HasValue
                    ? transactions.Where(t => t.CategoryId == budget.CategoryId).Sum(t => t.Amount)
                    : totalExpense;

                var percentage = budget.LimitAmount > 0 ? (spent / budget.LimitAmount * 100) : 0;
                var categoryName = budget.Category?.Name ?? "Genel Bütçe";

                Console.WriteLine($"📊 Budget check: {categoryName} | %{Math.Round(percentage, 1)} | Spent: {spent} / Limit: {budget.LimitAmount}");

                // %50 bilgi bildirimi
                if (percentage >= 50 && percentage < 80)
                {
                    var title = $"📊 Bütçe Bilgisi - {categoryName}";
                    var message = $"{categoryName} bütçenizin yarısını kullandınız (%{Math.Round(percentage, 0)}). Kalan: ₺{(budget.LimitAmount - spent):N2}";

                    var existingNotif = await _context.Notifications
                        .AnyAsync(n => n.UserId == userId &&
                                       n.Title == title &&
                                       n.CreatedAt.Month == now.Month &&
                                       n.CreatedAt.Year == now.Year);

                    if (!existingNotif)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = userId,
                            Title = title,
                            Message = message,
                            Type = "Info",
                            Icon = "📊",
                            CreatedAt = DateTime.Now
                        });

                        Console.WriteLine($"🔔 %50 site bildirimi oluşturuldu: {categoryName}");

                        if (user != null && user.IsEmailVerified && user.EmailNotificationsEnabled)
                        {
                            try
                            {
                                var emailService = new EmailService(_configuration);
                                await emailService.SendBudgetWarningAsync(
                                    user.Email, user.Username, categoryName,
                                    percentage, budget.LimitAmount - spent);
                                Console.WriteLine($"✅ %50 email gönderildi: {user.Email}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ %50 email HATA: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⏭️ Email atlandı — enabled:{user?.EmailNotificationsEnabled}, verified:{user?.IsEmailVerified}");
                        }
                    }

                    warnings.Add(new { type = "info", categoryName, percentage, remaining = budget.LimitAmount - spent });
                }

                //  %80 uyarı bildirimi
                if (percentage >= 80 && percentage < 100)
                {
                    var title = $"⚠️ Bütçe Uyarısı - {categoryName}";
                    var message = $"{categoryName} bütçenizin %{Math.Round(percentage, 0)}'ini kullandınız. Kalan: ₺{(budget.LimitAmount - spent):N2}";

                    var existingNotif = await _context.Notifications
                        .AnyAsync(n => n.UserId == userId &&
                                       n.Title == title &&
                                       n.CreatedAt.Month == now.Month &&
                                       n.CreatedAt.Year == now.Year);

                    if (!existingNotif)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = userId,
                            Title = title,
                            Message = message,
                            Type = "Warning",
                            Icon = "⚠️",
                            CreatedAt = DateTime.Now
                        });

                        Console.WriteLine($"🔔 %80 site bildirimi oluşturuldu: {categoryName}");

                        if (user != null && user.IsEmailVerified && user.EmailNotificationsEnabled)
                        {
                            try
                            {
                                var emailService = new EmailService(_configuration);
                                await emailService.SendBudgetWarningAsync(
                                    user.Email, user.Username, categoryName,
                                    percentage, budget.LimitAmount - spent);
                                Console.WriteLine($"✅ %80 email gönderildi: {user.Email}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ %80 email HATA: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⏭️ Email atlandı — enabled:{user?.EmailNotificationsEnabled}, verified:{user?.IsEmailVerified}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"ℹ️ %80 bildirimi zaten mevcut: {categoryName}");
                    }

                    warnings.Add(new { type = "warning", categoryName, percentage, remaining = budget.LimitAmount - spent });
                }

                // %100 aşım bildirimi
                if (percentage >= 100)
                {
                    var title = $"🚨 Bütçe Aşıldı - {categoryName}";
                    var message = $"{categoryName} bütçenizi aştınız! Limit: ₺{budget.LimitAmount:N2}, Harcama: ₺{spent:N2}";

                    var existingNotif = await _context.Notifications
                        .AnyAsync(n => n.UserId == userId &&
                                       n.Title == title &&
                                       n.CreatedAt.Month == now.Month &&
                                       n.CreatedAt.Year == now.Year);

                    if (!existingNotif)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = userId,
                            Title = title,
                            Message = message,
                            Type = "Danger",
                            Icon = "🚨",
                            CreatedAt = DateTime.Now
                        });

                        Console.WriteLine($"🔔 %100 site bildirimi oluşturuldu: {categoryName}");

                        if (user != null && user.IsEmailVerified && user.EmailNotificationsEnabled)
                        {
                            try
                            {
                                var emailService = new EmailService(_configuration);
                                await emailService.SendBudgetExceededAsync(
                                    user.Email, user.Username, categoryName,
                                    budget.LimitAmount, spent);
                                Console.WriteLine($"✅ %100 email gönderildi: {user.Email}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ %100 email HATA: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⏭️ Email atlandı — enabled:{user?.EmailNotificationsEnabled}, verified:{user?.IsEmailVerified}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"ℹ️ %100 bildirimi zaten mevcut: {categoryName}");
                    }

                    warnings.Add(new { type = "exceeded", categoryName, percentage, spent, limit = budget.LimitAmount });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { warnings });
        }
    }

    public class BudgetDto
    {
        public int? CategoryId { get; set; }
        public decimal LimitAmount { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
    }
}