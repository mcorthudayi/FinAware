using FinAware.API.Data;
using FinAware.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FinAware.API.Services
{
    public class MonthlyReportService
    {
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MonthlyReportService> _logger;

        public MonthlyReportService(
            AppDbContext context,
            EmailService emailService,
            IConfiguration configuration,
            ILogger<MonthlyReportService> logger)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendMonthlyReportsAsync(int year, int month)
        {
            var users = await _context.Users
                .Where(u => u.EmailNotificationsEnabled && u.IsEmailVerified)
                .ToListAsync();

            _logger.LogInformation($"📊 Aylık rapor gönderiliyor: {users.Count} kullanıcı, {year}/{month}");

            foreach (var user in users)
            {
                try
                {
                    await SendReportToUserAsync(user, year, month);
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"❌ Rapor hatası ({user.Email}): {ex.Message}");
                }
            }
        }

        public async Task<MonthlyReportData> GetMonthlyReportDataAsync(int userId, int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

            var prevStart = startDate.AddMonths(-1);
            var prevEnd = startDate.AddDays(-1);
            var prevTransactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= prevStart && t.Date <= prevEnd)
                .ToListAsync();

            decimal income = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal expense = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            decimal balance = income - expense;

            decimal prevIncome = prevTransactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal prevExpense = prevTransactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);

            var categoryBreakdown = transactions
                .Where(t => t.Type == "Expense")
                .GroupBy(t => new { t.Category?.Name, t.Category?.Icon })
                .Select(g => new CategoryReport
                {
                    CategoryName = g.Key.Name ?? "Diğer",
                    Icon = g.Key.Icon ?? "💳",
                    Amount = g.Sum(t => t.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            var prevCategoryBreakdown = prevTransactions
                .Where(t => t.Type == "Expense")
                .GroupBy(t => t.Category?.Name ?? "Diğer")
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

            var firmBreakdown = transactions
                .Where(t => t.Type == "Expense" && !string.IsNullOrWhiteSpace(t.Description))
                .GroupBy(t => t.Description!.Trim())
                .Select(g => new FirmReport
                {
                    Name = g.Key,
                    Total = g.Sum(t => t.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(f => f.Total)
                .Take(5)
                .ToList();

            return new MonthlyReportData
            {
                Year = year,
                Month = month,
                Income = income,
                Expense = expense,
                Balance = balance,
                PrevIncome = prevIncome,
                PrevExpense = prevExpense,
                TransactionCount = transactions.Count,
                CategoryBreakdown = categoryBreakdown,
                PrevCategoryBreakdown = prevCategoryBreakdown,
                FirmBreakdown = firmBreakdown
            };
        }

        private async Task SendReportToUserAsync(User user, int year, int month)
        {
            var data = await GetMonthlyReportDataAsync(user.UserId, year, month);

            if (data.TransactionCount == 0)
            {
                _logger.LogInformation($"⏭️ {user.Email} - İşlem yok, atlandı");
                return;
            }

            var advice = await GenerateAdviceAsync(data, user.Username);
            var subject = $"📊 {MonthName(month)} {year} Aylık Finansal Raporunuz – FinAware";
            var body = BuildEmailHtml(user.Username, data, advice, year, month);

            await _emailService.SendEmailAsync(user.Email, subject, body);
            _logger.LogInformation($"✅ Rapor gönderildi: {user.Email}");
        }

        private async Task<string> GenerateAdviceAsync(MonthlyReportData data, string username)
        {
            try
            {
                var apiKey = _configuration["OpenAI:ApiKey"];
                if (string.IsNullOrEmpty(apiKey)) return "";

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var topCategories = string.Join(", ",
                    data.CategoryBreakdown.Take(3).Select(c => $"{c.Icon}{c.CategoryName}: ₺{c.Amount:N2}"));

                var topFirms = data.FirmBreakdown.Any()
                    ? string.Join(", ", data.FirmBreakdown.Take(3).Select(f => $"{f.Name}: ₺{f.Total:N2}"))
                    : "";

                var expenseChange = data.PrevExpense > 0
                    ? ((data.Expense - data.PrevExpense) / data.PrevExpense * 100) : 0;

                var prompt = $@"Sen FinAware kişisel finans asistanısın. Kullanıcı: {username}.
{MonthName(data.Month)} {data.Year} raporu:
- Gelir: ₺{data.Income:N2}
- Gider: ₺{data.Expense:N2}
- Net bakiye: ₺{data.Balance:N2}
- Geçen aya göre gider değişimi: %{expenseChange:N1}
- En yüksek harcama kategorileri: {topCategories}
{(string.IsNullOrEmpty(topFirms) ? "" : $"- En çok harcama yapılan firmalar: {topFirms}")}

Kısa, samimi ve motive edici 3-4 cümle tavsiye yaz. Türkçe. Rakamları tekrar sayma.";

                var payload = new
                {
                    model = "gpt-4o",
                    max_tokens = 200,
                    messages = new[] { new { role = "user", content = prompt } }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var doc = System.Text.Json.JsonDocument.Parse(result);
                    return doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"GPT tavsiye hatası: {ex.Message}");
            }
            return "";
        }

        private string BuildEmailHtml(
            string username, MonthlyReportData data,
            string advice, int year, int month)
        {
            var balanceColor = data.Balance >= 0 ? "#10b981" : "#ef4444";
            var balanceSign = data.Balance >= 0 ? "+" : "";
            var expenseChange = data.PrevExpense > 0
                ? ((data.Expense - data.PrevExpense) / data.PrevExpense * 100) : 0;
            var expChangeColor = expenseChange <= 0 ? "#10b981" : "#ef4444";
            var expChangeSign = expenseChange >= 0 ? "+" : "";

            var categoryRows = string.Join("", data.CategoryBreakdown.Take(6).Select(c =>
            {
                var pct = data.Expense > 0 ? (c.Amount / data.Expense * 100) : 0;
                var prevAmt = data.PrevCategoryBreakdown.GetValueOrDefault(c.CategoryName, 0);
                var diff = prevAmt > 0
                    ? $"<span style='color:{(c.Amount <= prevAmt ? "#10b981" : "#ef4444")};font-size:12px;'>" +
                      $"{(c.Amount <= prevAmt ? "▼" : "▲")} Geçen ay: ₺{prevAmt:N2}</span>"
                    : "<span style='color:#9ca3af;font-size:12px;'>İlk kez</span>";

                return $@"
                <tr style='border-bottom:1px solid #f3f4f6;'>
                    <td style='padding:10px 8px;font-size:15px;'>{c.Icon}</td>
                    <td style='padding:10px 8px;'>
                        <div style='font-weight:600;color:#111827;'>{c.CategoryName}</div>
                        <div>{diff}</div>
                    </td>
                    <td style='padding:10px 8px;text-align:right;'>
                        <div style='font-weight:700;color:#ef4444;'>₺{c.Amount:N2}</div>
                        <div style='font-size:11px;color:#9ca3af;'>{c.Count} işlem · %{pct:N0}</div>
                    </td>
                </tr>";
            }));

            var firmSection = "";
            if (data.FirmBreakdown.Any())
            {
                var firmRows = string.Join("", data.FirmBreakdown.Select(f => $@"
                <tr style='border-bottom:1px solid #f3f4f6;'>
                    <td style='padding:9px 8px;font-weight:600;color:#111827;'>{f.Name}</td>
                    <td style='padding:9px 8px;color:#6b7280;font-size:12px;text-align:center;'>{f.Count} işlem</td>
                    <td style='padding:9px 8px;text-align:right;font-weight:700;color:#ef4444;'>₺{f.Total:N2}</td>
                </tr>"));

                firmSection = $@"
        <div style='margin-bottom:24px;'>
            <div style='font-weight:700;color:#111827;margin-bottom:12px;font-size:16px;'>
                🏢 Firma / Açıklama Bazlı Harcamalar
            </div>
            <table style='width:100%;border-collapse:collapse;'>
                <tr style='background:#f9fafb;'>
                    <th style='padding:8px;text-align:left;font-size:12px;color:#6b7280;'>Firma / Açıklama</th>
                    <th style='padding:8px;text-align:center;font-size:12px;color:#6b7280;'>İşlem</th>
                    <th style='padding:8px;text-align:right;font-size:12px;color:#6b7280;'>Toplam</th>
                </tr>
                {firmRows}
            </table>
        </div>";
            }

            var adviceSection = !string.IsNullOrEmpty(advice) ? $@"
        <div style='background:#f0fdf4;border-left:4px solid #1AAFA3;
                    border-radius:8px;padding:16px 20px;margin:24px 0;'>
            <div style='font-weight:700;color:#065f46;margin-bottom:6px;'>
                🤖 FinAware AI Tavsiyesi
            </div>
            <div style='color:#374151;font-size:14px;line-height:1.6;'>{advice}</div>
        </div>" : "";

            return $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f4f7f9;font-family:Segoe UI,sans-serif;'>
<div style='max-width:600px;margin:32px auto;background:white;border-radius:16px;
            overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);'>

    <div style='background:linear-gradient(135deg,#1AAFA3 0%,#0D7A71 100%);
                padding:32px 24px;text-align:center;'>
        <div style='font-size:32px;margin-bottom:8px;'>📊</div>
        <div style='color:white;font-size:22px;font-weight:700;'>Aylık Finansal Rapor</div>
        <div style='color:rgba(255,255,255,0.8);font-size:15px;margin-top:4px;'>
            {MonthName(month)} {year}
        </div>
    </div>

    <div style='padding:28px 24px;'>
        <p style='color:#374151;font-size:15px;margin-bottom:24px;'>
            Merhaba <strong>{username}</strong> 👋<br>
            {MonthName(month)} ayına ait finansal özet raporu aşağıda.
        </p>

        <div style='display:flex;gap:12px;margin-bottom:24px;'>
            <div style='flex:1;background:#ecfdf5;border-radius:12px;padding:16px;text-align:center;'>
                <div style='font-size:12px;color:#6b7280;margin-bottom:4px;'>Toplam Gelir</div>
                <div style='font-size:22px;font-weight:700;color:#10b981;'>₺{data.Income:N2}</div>
            </div>
            <div style='flex:1;background:#fef2f2;border-radius:12px;padding:16px;text-align:center;'>
                <div style='font-size:12px;color:#6b7280;margin-bottom:4px;'>Toplam Gider</div>
                <div style='font-size:22px;font-weight:700;color:#ef4444;'>₺{data.Expense:N2}</div>
            </div>
            <div style='flex:1;background:#f0fdf4;border-radius:12px;padding:16px;text-align:center;'>
                <div style='font-size:12px;color:#6b7280;margin-bottom:4px;'>Net Bakiye</div>
                <div style='font-size:22px;font-weight:700;color:{balanceColor};'>
                    {balanceSign}₺{data.Balance:N2}
                </div>
            </div>
        </div>

        <div style='background:#f9fafb;border-radius:12px;padding:16px;margin-bottom:24px;'>
            <div style='font-weight:600;color:#374151;margin-bottom:10px;'>📈 Geçen Ay Karşılaştırma</div>
            <div style='display:flex;justify-content:space-between;margin-bottom:6px;'>
                <span style='color:#6b7280;'>Gider değişimi</span>
                <span style='font-weight:700;color:{expChangeColor};'>
                    {expChangeSign}%{expenseChange:N1}
                </span>
            </div>
            <div style='display:flex;justify-content:space-between;'>
                <span style='color:#6b7280;'>Geçen ay gider</span>
                <span style='font-weight:600;color:#374151;'>₺{data.PrevExpense:N2}</span>
            </div>
        </div>

        {(data.CategoryBreakdown.Any() ? $@"
        <div style='margin-bottom:24px;'>
            <div style='font-weight:700;color:#111827;margin-bottom:12px;font-size:16px;'>
                🏷️ Kategori Bazlı Harcamalar
            </div>
            <table style='width:100%;border-collapse:collapse;'>
                {categoryRows}
            </table>
        </div>" : "")}

        {firmSection}

        {adviceSection}

        <div style='text-align:center;padding-top:16px;border-top:1px solid #f3f4f6;'>
            <p style='color:#9ca3af;font-size:12px;'>
                Bu rapor FinAware tarafından otomatik oluşturulmuştur.<br>
                E-posta bildirimlerini kapatmak için profil sayfanızı ziyaret edin.
            </p>
        </div>
    </div>
</div>
</body>
</html>";
        }

        private string MonthName(int month)
        {
            var names = new[]
            {
                "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
                "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
            };
            return names[month];
        }
    }

    public class MonthlyReportData
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal Balance { get; set; }
        public decimal PrevIncome { get; set; }
        public decimal PrevExpense { get; set; }
        public int TransactionCount { get; set; }
        public List<CategoryReport> CategoryBreakdown { get; set; } = new();
        public Dictionary<string, decimal> PrevCategoryBreakdown { get; set; } = new();
        public List<FirmReport> FirmBreakdown { get; set; } = new();
    }

    public class CategoryReport
    {
        public string CategoryName { get; set; } = "";
        public string Icon { get; set; } = "💳";
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    public class FirmReport
    {
        public string Name { get; set; } = "";
        public decimal Total { get; set; }
        public int Count { get; set; }
    }
}