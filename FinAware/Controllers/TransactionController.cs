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

        // GET: api/transaction endpointi, kullanıcının tüm işlemlerini getirir
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

        // GET: api/transaction/{id} endpointi, kullanıcının belirli bir işlemini getirir
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

        // POST: api/transaction endpointi, yeni bir işlem oluşturur
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

        // PUT: api/transaction/{id} endpointi, kullanıcının belirli bir işlemini günceller
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
                transaction.Description = dto.Description;
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

        // DELETE: api/transaction/{id} endpointi, kullanıcının belirli bir işlemini siler
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

        // POST: api/transaction/analyze-invoice endpointi, kullanıcının yüklediği fatura görselini analiz eder ve işlem bilgilerini döner
        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("analyze-invoice")]
        public async Task<IActionResult> AnalyzeInvoice([FromForm] IFormFile file)
        {
            try
            {
                Console.WriteLine("═══════════════════════════════════════");
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

                Console.WriteLine("═══════════════════════════════════════");

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