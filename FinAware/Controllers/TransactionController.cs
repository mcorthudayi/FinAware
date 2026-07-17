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
    public class TransactionController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public TransactionController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var transactions = await _context.Transactions
                    .Where(t => t.UserId == int.Parse(userId))
                    .Include(t => t.Category)
                    .OrderByDescending(t => t.Date)
                    .Select(t => new
                    {
                        t.TransactionId,
                        t.Amount,
                        t.Date,
                        t.Description,
                        t.Type,
                        t.OriginalAmount,
                        t.OriginalCurrency,
                        t.ExchangeRate,
                        Category = new
                        {
                            t.Category.CategoryId,
                            t.Category.Name,
                            t.Category.Icon
                        }
                    })
                    .ToListAsync();

                return Ok(transactions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "İşlemler getirilirken hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTransactionById(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var transaction = await _context.Transactions
                    .Include(t => t.Category)
                    .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == int.Parse(userId));

                if (transaction == null)
                    return NotFound(new { message = "İşlem bulunamadı" });

                return Ok(new
                {
                    transaction.TransactionId,
                    transaction.Amount,
                    transaction.Date,
                    transaction.Description,
                    transaction.Type,
                    transaction.OriginalAmount,
                    transaction.OriginalCurrency,
                    transaction.ExchangeRate,
                    Category = new
                    {
                        transaction.Category.CategoryId,
                        transaction.Category.Name,
                        transaction.Category.Icon
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "İşlem getirilirken hata oluştu", error = ex.Message });
            }
        }

        // GET /api/analysis?month=7&year=2026
        // NOT: mutlak route kullanildigi icin (baslangictaki "/") bu action
        // TransactionController icinde olmasina ragmen /api/analysis adresinden
        // calisir, controller'in "api/Transaction" route prefix'ini gormezden gelir.
        [HttpGet("/api/analysis")]
        public async Task<IActionResult> GetAnalysis([FromQuery] int? month, [FromQuery] int? year)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var targetMonth = month ?? DateTime.Now.Month;
                var targetYear  = year ?? DateTime.Now.Year;

                var transactions = await _context.Transactions
                    .Include(t => t.Category)
                    .Where(t => t.UserId == int.Parse(userId)
                             && t.Date.Month == targetMonth
                             && t.Date.Year == targetYear)
                    .OrderByDescending(t => t.Date)
                    .Select(t => new
                    {
                        t.TransactionId,
                        t.Amount,
                        t.Date,
                        t.Description,
                        t.Type,
                        t.OriginalCurrency,
                        CategoryId = t.CategoryId,
                        categoryName = t.Category != null ? t.Category.Name : null,
                        categoryIcon = t.Category != null ? t.Category.Icon : null,
                    })
                    .ToListAsync();

                var totalIncome   = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
                var totalExpenses = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
                var balance       = totalIncome - totalExpenses;

                var categoryExpenses = transactions
                    .Where(t => t.Type == "Expense" && t.categoryName != null)
                    .GroupBy(t => t.categoryName)
                    .ToDictionary(g => g.Key!, g => g.Sum(t => t.Amount));

                return Ok(new
                {
                    month = targetMonth,
                    year = targetYear,
                    transactions,
                    categoryExpenses,
                    totalIncome,
                    totalExpenses,
                    balance,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Analiz verisi alınamadı", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTransaction([FromBody] TransactionCreateDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var currency = dto.Currency?.ToUpper() ?? "TRY";
                decimal exchangeRate = 1m;
                decimal tryAmount = dto.Amount;

                if (currency != "TRY")
                {
                    var exchangeService = new ExchangeRateService();

                    if (dto.ManualRate.HasValue && dto.ManualRate.Value > 0)
                    {
                        exchangeRate = dto.ManualRate.Value;
                    }
                    else
                    {
                        exchangeRate = await exchangeService.GetRateAsync(currency);
                        if (exchangeRate <= 0)
                            return BadRequest(new { message = $"{currency} için kur bilgisi alınamadı" });
                    }

                    tryAmount = Math.Round(dto.Amount * exchangeRate, 2);
                }

                var transaction = new Transaction
                {
                    Amount = tryAmount,
                    OriginalAmount = dto.Amount,
                    OriginalCurrency = currency,
                    ExchangeRate = exchangeRate,
                    Date = dto.Date,
                    Description = dto.Description ?? string.Empty,
                    Type = dto.Type,
                    CategoryId = dto.CategoryId,
                    UserId = int.Parse(userId),
                    CreatedAt = DateTime.UtcNow
                };

                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "İşlem başarıyla eklendi",
                    transactionId = transaction.TransactionId,
                    tryAmount,
                    exchangeRate,
                    currency
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "İşlem eklenirken hata oluştu", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTransaction(int id, [FromBody] TransactionCreateDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var transaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == int.Parse(userId));

                if (transaction == null)
                    return NotFound(new { message = "İşlem bulunamadı" });

                var currency = dto.Currency?.ToUpper() ?? "TRY";
                decimal exchangeRate = 1m;
                decimal tryAmount = dto.Amount;

                if (currency != "TRY")
                {
                    var exchangeService = new ExchangeRateService();
                    exchangeRate = await exchangeService.GetRateAsync(currency);
                    tryAmount = Math.Round(dto.Amount * exchangeRate, 2);
                }

                transaction.Amount = tryAmount;
                transaction.OriginalAmount = dto.Amount;
                transaction.OriginalCurrency = currency;
                transaction.ExchangeRate = exchangeRate;
                transaction.Date = dto.Date;
                transaction.Description = dto.Description ?? string.Empty;
                transaction.Type = dto.Type;
                transaction.CategoryId = dto.CategoryId;

                await _context.SaveChangesAsync();
                return Ok(new { message = "İşlem başarıyla güncellendi" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "İşlem güncellenirken hata oluştu", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var transaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == int.Parse(userId));

                if (transaction == null)
                    return NotFound(new { message = "İşlem bulunamadı" });

                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();
                return Ok(new { message = "İşlem başarıyla silindi" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "İşlem silinirken hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("monthly-report")]
        public async Task<IActionResult> GetMonthlyReport(
            [FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var reportService = HttpContext.RequestServices
                    .GetRequiredService<MonthlyReportService>();

                var data = await reportService.GetMonthlyReportDataAsync(
                    int.Parse(userId), year, month);

                return Ok(new
                {
                    year,
                    month,
                    income = data.Income,
                    expense = data.Expense,
                    balance = data.Balance,
                    prevIncome = data.PrevIncome,
                    prevExpense = data.PrevExpense,
                    transactionCount = data.TransactionCount,
                    categoryBreakdown = data.CategoryBreakdown.Select(c => new
                    {
                        c.CategoryName,
                        c.Icon,
                        c.Amount,
                        c.Count
                    }),
                    expenseChange = data.PrevExpense > 0
                        ? Math.Round((data.Expense - data.PrevExpense) / data.PrevExpense * 100, 1)
                        : 0
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Rapor getirilirken hata oluştu", error = ex.Message });
            }
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportToExcel(
            [FromQuery] int? month,
            [FromQuery] int? year,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var query = _context.Transactions
                    .Include(t => t.Category)
                    .Where(t => t.UserId == int.Parse(userId));

                string period;

                if (startDate.HasValue && endDate.HasValue)
                {
                    var rangeStart = startDate.Value.Date;
                    var rangeEnd = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(t => t.Date >= rangeStart && t.Date <= rangeEnd);
                    period = $"{startDate.Value:dd.MM.yyyy} – {endDate.Value:dd.MM.yyyy}";
                }
                else if (month.HasValue && year.HasValue)
                {
                    var start = new DateTime(year.Value, month.Value, 1);
                    var end = start.AddMonths(1).AddDays(-1);
                    query = query.Where(t => t.Date >= start && t.Date <= end);
                    period = $"{new DateTime(year.Value, month.Value, 1):MMMM yyyy}";
                }
                else if (year.HasValue)
                {
                    query = query.Where(t => t.Date.Year == year.Value);
                    period = year.Value.ToString();
                }
                else
                {
                    period = "Tüm Zamanlar";
                }

                var transactions = await query
                    .OrderByDescending(t => t.Date)
                    .ToListAsync();

                OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("FinAware");

                using var package = new OfficeOpenXml.ExcelPackage();
                var ws = package.Workbook.Worksheets.Add("İşlemler");

                // Başlık
                var titleRow = ws.Cells["A1:H1"];
                titleRow.Merge = true;
                ws.Cells["A1"].Value = "FinAware – İşlem Raporu";
                ws.Cells["A1"].Style.Font.Size = 16;
                ws.Cells["A1"].Style.Font.Bold = true;
                ws.Cells["A1"].Style.Font.Color.SetColor(System.Drawing.Color.White);
                ws.Cells["A1"].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells["A1"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(26, 175, 163));
                ws.Cells["A1"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                ws.Row(1).Height = 30;

                // Alt başlık
                ws.Cells["A2:H2"].Merge = true;
                ws.Cells["A2"].Value = $"Dönem: {period} | Toplam: {transactions.Count} işlem | Oluşturuldu: {DateTime.Now:dd.MM.yyyy HH:mm}";
                ws.Cells["A2"].Style.Font.Italic = true;
                ws.Cells["A2"].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
                ws.Cells["A2"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                ws.Row(2).Height = 20;

                // Sütun başlıkları
                var headers = new[] { "Tarih", "Tip", "Kategori", "Açıklama", "Tutar (TL)", "Orijinal Tutar", "Para Birimi", "Kur" };
                var headerColor = System.Drawing.Color.FromArgb(13, 122, 113);

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cells[3, i + 1];
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(headerColor);
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    cell.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                }
                ws.Row(3).Height = 22;

                // Veri satırları
                decimal totalIncome = 0, totalExpense = 0;

                for (int i = 0; i < transactions.Count; i++)
                {
                    var t = transactions[i];
                    int row = i + 4;
                    bool isIncome = t.Type == "Income";

                    if (i % 2 == 0)
                    {
                        ws.Cells[row, 1, row, 8].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        ws.Cells[row, 1, row, 8].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 253, 250));
                    }

                    ws.Cells[row, 1].Value = t.Date.ToString("dd.MM.yyyy");
                    ws.Cells[row, 2].Value = isIncome ? "Gelir" : "Gider";
                    ws.Cells[row, 2].Style.Font.Color.SetColor(isIncome
                        ? System.Drawing.Color.FromArgb(16, 185, 129)
                        : System.Drawing.Color.FromArgb(239, 68, 68));
                    ws.Cells[row, 2].Style.Font.Bold = true;
                    ws.Cells[row, 3].Value = t.Category?.Name ?? "Diğer";
                    ws.Cells[row, 4].Value = t.Description;
                    ws.Cells[row, 5].Value = t.Amount;
                    ws.Cells[row, 5].Style.Numberformat.Format = "#,##0.00 ₺";
                    ws.Cells[row, 5].Style.Font.Color.SetColor(isIncome
                        ? System.Drawing.Color.FromArgb(16, 185, 129)
                        : System.Drawing.Color.FromArgb(239, 68, 68));
                    ws.Cells[row, 6].Value = t.OriginalAmount != t.Amount ? t.OriginalAmount : (object)"";
                    ws.Cells[row, 7].Value = t.OriginalCurrency != "TRY" ? t.OriginalCurrency : "";
                    ws.Cells[row, 8].Value = t.ExchangeRate != 1 ? t.ExchangeRate : (object)"";
                    ws.Cells[row, 8].Style.Numberformat.Format = "#,##0.0000";
                    ws.Row(row).Height = 18;

                    if (isIncome) totalIncome += t.Amount;
                    else totalExpense += t.Amount;
                }

                // Özet
                int summaryRow = transactions.Count + 4;
                ws.Cells[summaryRow, 1, summaryRow, 8].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[summaryRow, 1, summaryRow, 8].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 253, 250));
                ws.Cells[summaryRow, 1, summaryRow, 3].Merge = true;
                ws.Cells[summaryRow, 1].Value = "ÖZET";
                ws.Cells[summaryRow, 1].Style.Font.Bold = true;
                ws.Cells[summaryRow, 1].Style.Font.Size = 11;
                ws.Cells[summaryRow, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                ws.Cells[summaryRow, 4].Value = "Toplam Gelir:";
                ws.Cells[summaryRow, 4].Style.Font.Bold = true;
                ws.Cells[summaryRow, 5].Value = totalIncome;
                ws.Cells[summaryRow, 5].Style.Numberformat.Format = "#,##0.00 ₺";
                ws.Cells[summaryRow, 5].Style.Font.Bold = true;
                ws.Cells[summaryRow, 5].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(16, 185, 129));

                ws.Cells[summaryRow + 1, 4].Value = "Toplam Gider:";
                ws.Cells[summaryRow + 1, 4].Style.Font.Bold = true;
                ws.Cells[summaryRow + 1, 5].Value = totalExpense;
                ws.Cells[summaryRow + 1, 5].Style.Numberformat.Format = "#,##0.00 ₺";
                ws.Cells[summaryRow + 1, 5].Style.Font.Bold = true;
                ws.Cells[summaryRow + 1, 5].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(239, 68, 68));

                decimal netBalance = totalIncome - totalExpense;
                ws.Cells[summaryRow + 2, 4].Value = "Net Bakiye:";
                ws.Cells[summaryRow + 2, 4].Style.Font.Bold = true;
                ws.Cells[summaryRow + 2, 5].Value = netBalance;
                ws.Cells[summaryRow + 2, 5].Style.Numberformat.Format = "#,##0.00 ₺";
                ws.Cells[summaryRow + 2, 5].Style.Font.Bold = true;
                ws.Cells[summaryRow + 2, 5].Style.Font.Color.SetColor(netBalance >= 0
                    ? System.Drawing.Color.FromArgb(26, 175, 163)
                    : System.Drawing.Color.FromArgb(239, 68, 68));

                // Sütun genişlikleri
                ws.Column(1).Width = 14;
                ws.Column(2).Width = 10;
                ws.Column(3).Width = 18;
                ws.Column(4).Width = 30;
                ws.Column(5).Width = 16;
                ws.Column(6).Width = 16;
                ws.Column(7).Width = 12;
                ws.Column(8).Width = 14;

                // Border
                var dataRange = ws.Cells[3, 1, summaryRow + 2, 8];
                dataRange.Style.Border.Top.Style =
                dataRange.Style.Border.Bottom.Style =
                dataRange.Style.Border.Left.Style =
                dataRange.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                dataRange.Style.Border.Top.Color.SetColor(System.Drawing.Color.FromArgb(229, 231, 235));
                dataRange.Style.Border.Bottom.Color.SetColor(System.Drawing.Color.FromArgb(229, 231, 235));

                var excelBytes = package.GetAsByteArray();

                string fileName = startDate.HasValue && endDate.HasValue
                    ? $"FinAware_{startDate.Value:yyyyMMdd}_{endDate.Value:yyyyMMdd}.xlsx"
                    : month.HasValue && year.HasValue
                        ? $"FinAware_{year}_{month:D2}.xlsx"
                        : $"FinAware_{DateTime.Now:yyyyMMdd}.xlsx";

                return File(excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ EXPORT HATA: {ex.Message}");
                Console.WriteLine($"❌ STACK: {ex.StackTrace}");
                return StatusCode(500, new { message = "Excel oluşturulamadı", error = ex.Message });
            }
        }

        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("analyze-invoice")]
        public async Task<IActionResult> AnalyzeInvoice([FromForm] IFormFile file)
        {
            try
            {
               
                Console.WriteLine("📸 INVOICE UPLOAD REQUEST");

                if (file == null || file.Length == 0)
                    return BadRequest(new { success = false, message = "Dosya yüklenmedi" });

                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    return BadRequest(new { success = false, message = "Sadece JPG ve PNG dosyaları yüklenebilir" });

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);

                Console.WriteLine($"✅ File saved: {filePath}");

                var invoiceService = new InvoiceService(_configuration);
                var result = await invoiceService.AnalyzeInvoiceAsync(filePath);

                try { System.IO.File.Delete(filePath); } catch { }

                

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        amount = result.Amount,
                        date = result.Date?.ToString("yyyy-MM-dd"),
                        category = result.Category,
                        description = result.Description,
                        confidence = result.Confidence
                    });
                }

                return Ok(new
                {
                    success = false,
                    message = "Fatura okunamadı. Lütfen manuel girin.",
                    error = result.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Analyze invoice exception: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Fatura analizi başarısız", error = ex.Message });
            }
        }
    }

    public class TransactionCreateDto
    {
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public string Type { get; set; } = "Expense";
        public int CategoryId { get; set; }
        public string Currency { get; set; } = "TRY";
        public decimal? ManualRate { get; set; }
    }
}