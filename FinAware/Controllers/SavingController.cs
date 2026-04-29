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
    public class SavingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ExchangeRateService _exchangeRateService;

        public SavingController(AppDbContext context)
        {
            _context = context;
            _exchangeRateService = new ExchangeRateService();
        }

        // GET: api/saving endpointi, kullanıcının tüm birikim hedeflerini getirir
        [HttpGet]
        public async Task<IActionResult> GetSavings()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var savings = await _context.Savings
                    .Where(s => s.UserId == int.Parse(userId))
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new
                    {
                        s.SavingId,
                        s.GoalName,
                        s.TargetAmount,
                        s.CurrentAmount,
                        s.TargetDate,
                        s.Icon,
                        s.Color,
                        Progress = s.TargetAmount > 0 ? (s.CurrentAmount / s.TargetAmount * 100) : 0
                    })
                    .ToListAsync();

                return Ok(savings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Get savings error: {ex.Message}");
                return StatusCode(500, new { message = "Birikimler getirilirken hata oluştu" });
            }
        }

        // GET: api/saving/{id} endpointi, belirli bir birikim hedefini getirir
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSavingById(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var saving = await _context.Savings
                    .FirstOrDefaultAsync(s => s.SavingId == id && s.UserId == int.Parse(userId));

                if (saving == null)
                    return NotFound(new { message = "Birikim bulunamadı" });

                return Ok(saving);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Get saving error: {ex.Message}");
                return StatusCode(500, new { message = "Birikim getirilirken hata oluştu" });
            }
        }

        // GET: api/saving/{id}/details endpointi, belirli bir birikim hedefinin detaylarını getirir (işlemler, kar/zarar vs.)
        [HttpGet("{id}/details")]
        public async Task<IActionResult> GetSavingDetails(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var saving = await _context.Savings
                    .Include(s => s.Transactions)
                    .FirstOrDefaultAsync(s => s.SavingId == id && s.UserId == int.Parse(userId));

                if (saving == null)
                    return NotFound(new { message = "Birikim bulunamadı" });

                var rates = await _exchangeRateService.GetRatesAsync();

                var allTx = saving.Transactions.ToList();
                var deposits = allTx.Where(t => t.TransactionType == "Deposit").ToList();
                var sales = allTx.Where(t => t.TransactionType == "Sale").ToList();

                // Hangi deposit'ler satılmış
                var soldDepositIds = sales
                    .Where(s => s.RelatedDepositId.HasValue)
                    .Select(s => s.RelatedDepositId!.Value)
                    .ToHashSet();

                // ── Aktif (henüz satılmamış) deposit'ler ─────────────────
                var activeDeposits = deposits
                    .Where(d => !soldDepositIds.Contains(d.SavingTransactionId))
                    .OrderByDescending(d => d.Date)
                    .Select(d =>
                    {
                        decimal currentRate = d.Currency == "TRY" ? 1m :
                            rates.TryGetValue(d.Currency, out var r) ? r : d.ExchangeRate;

                        decimal currentValue = d.Currency == "TRY"
                            ? d.Amount
                            : Math.Round(d.OriginalAmount * currentRate, 2);

                        decimal profitLoss = currentValue - d.Amount;
                        double profitLossPercent = d.Amount > 0
                            ? (double)(profitLoss / d.Amount * 100)
                            : 0;

                        return new
                        {
                            d.SavingTransactionId,
                            d.Amount,
                            d.OriginalAmount,
                            d.Currency,
                            d.ExchangeRate,
                            d.Date,
                            d.Note,
                            CurrentRate = currentRate,
                            CurrentValue = currentValue,
                            ProfitLoss = profitLoss,
                            ProfitLossPercent = Math.Round(profitLossPercent, 2),
                            IsProfit = profitLoss >= 0
                        };
                    }).ToList();

                // ── Satışlar (her sale, ilgili deposit'in alış kuruyla) ──
                var saleDetails = sales
                    .OrderByDescending(s => s.Date)
                    .Select(s =>
                    {
                        var relatedDeposit = deposits.FirstOrDefault(d => d.SavingTransactionId == s.RelatedDepositId);
                        decimal buyAmount = relatedDeposit?.Amount ?? 0;
                        decimal buyRate = relatedDeposit?.ExchangeRate ?? 1;
                        decimal realizedPL = s.Amount - buyAmount;
                        double realizedPercent = buyAmount > 0
                            ? (double)(realizedPL / buyAmount * 100)
                            : 0;

                        return new
                        {
                            SaleTransactionId = s.SavingTransactionId,
                            s.RelatedDepositId,
                            SaleAmount = s.Amount,
                            s.OriginalAmount,
                            s.Currency,
                            SaleRate = s.ExchangeRate,
                            BuyAmount = buyAmount,
                            BuyRate = buyRate,
                            s.Date,
                            s.Note,
                            RealizedProfitLoss = realizedPL,
                            RealizedPercent = Math.Round(realizedPercent, 2),
                            IsProfit = realizedPL >= 0,
                            DepositExists = relatedDeposit != null
                        };
                    }).ToList();

                // ── Aktif özet (yansımamış kar/zarar) ─────────────────────
                decimal activePaid = activeDeposits.Sum(d => d.Amount);
                decimal activeCurrentValue = activeDeposits.Sum(d => d.CurrentValue);
                decimal activeProfitLoss = activeCurrentValue - activePaid;
                double activePercent = activePaid > 0
                    ? (double)(activeProfitLoss / activePaid * 100)
                    : 0;

                // ── Gerçekleşen kar/zarar ─────────────────────────────────
                decimal totalRealizedPL = saleDetails.Sum(s => s.RealizedProfitLoss);
                decimal totalSold = saleDetails.Sum(s => s.SaleAmount);
                int saleCount = saleDetails.Count;

                return Ok(new
                {
                    saving.SavingId,
                    saving.GoalName,
                    saving.TargetAmount,
                    saving.CurrentAmount,
                    saving.TargetDate,
                    saving.Icon,
                    saving.Color,
                    Progress = saving.TargetAmount > 0
                        ? Math.Round(saving.CurrentAmount / saving.TargetAmount * 100, 1)
                        : 0,
                    ActiveDeposits = activeDeposits,
                    Sales = saleDetails,
                    ActiveSummary = new
                    {
                        TotalPaid = activePaid,
                        TotalCurrentValue = activeCurrentValue,
                        TotalProfitLoss = activeProfitLoss,
                        TotalProfitLossPercent = Math.Round(activePercent, 2),
                        IsProfit = activeProfitLoss >= 0
                    },
                    SaleSummary = new
                    {
                        SaleCount = saleCount,
                        TotalSoldAmount = totalSold,
                        TotalRealizedProfitLoss = totalRealizedPL,
                        IsProfit = totalRealizedPL >= 0
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Get saving details error: {ex.Message}");
                return StatusCode(500, new { message = "Birikim detayları getirilirken hata oluştu" });
            }
        }

        // GET: api/saving/rates endpointi, desteklenen para birimleri ve güncel kurlarını getirir
        [HttpGet("rates")]
        public async Task<IActionResult> GetRates()
        {
            try
            {
                var rates = await _exchangeRateService.GetRatesAsync();
                var currencies = _exchangeRateService.GetSupportedCurrencies();

                var result = currencies.Select(c => new
                {
                    c.Code,
                    c.Name,
                    c.Icon,
                    c.IsDefault,
                    Rate = rates.TryGetValue(c.Code, out var rate) ? rate : 0
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Get rates error: {ex.Message}");
                return StatusCode(500, new { message = "Kurlar getirilirken hata oluştu" });
            }
        }

        // POST: api/saving endpointi, yeni bir birikim hedefi oluşturur
        [HttpPost]
        public async Task<IActionResult> CreateSaving([FromBody] SavingDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                if (string.IsNullOrWhiteSpace(dto.GoalName))
                    return BadRequest(new { message = "Hedef adı boş olamaz" });

                if (dto.TargetAmount <= 0)
                    return BadRequest(new { message = "Hedef tutar 0'dan büyük olmalıdır" });

                var saving = new Saving
                {
                    UserId = int.Parse(userId),
                    GoalName = dto.GoalName,
                    TargetAmount = dto.TargetAmount,
                    CurrentAmount = 0,
                    TargetDate = dto.TargetDate,
                    Icon = dto.Icon ?? "💰",
                    Color = dto.Color ?? "#4DB6AC",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Savings.Add(saving);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Saving created: {saving.GoalName}");
                return Ok(new { message = "Birikim hedefi başarıyla eklendi!", savingId = saving.SavingId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Create saving error: {ex.Message}");
                return StatusCode(500, new { message = "Birikim eklenirken hata oluştu", error = ex.Message });
            }
        }

        // PUT: api/saving/{id} endpointi, mevcut bir birikim hedefini günceller
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSaving(int id, [FromBody] SavingDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var saving = await _context.Savings
                    .FirstOrDefaultAsync(s => s.SavingId == id && s.UserId == int.Parse(userId));

                if (saving == null)
                    return NotFound(new { message = "Birikim bulunamadı" });

                saving.GoalName = dto.GoalName;
                saving.TargetAmount = dto.TargetAmount;
                saving.TargetDate = dto.TargetDate;
                saving.Icon = dto.Icon ?? saving.Icon;
                saving.Color = dto.Color ?? saving.Color;

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Saving updated: {saving.GoalName}");
                return Ok(new { message = "Birikim başarıyla güncellendi!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Update saving error: {ex.Message}");
                return StatusCode(500, new { message = "Birikim güncellenirken hata oluştu" });
            }
        }

        // POST: api/saving/{id}/add endpointi, belirli bir birikim hedefine tutar ekler (farklı para birimleri ve manuel kur desteği ile)
        [HttpPost("{id}/add")]
        public async Task<IActionResult> AddToSaving(int id, [FromBody] AddAmountDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var saving = await _context.Savings
                    .FirstOrDefaultAsync(s => s.SavingId == id && s.UserId == int.Parse(userId));

                if (saving == null)
                    return NotFound(new { message = "Birikim bulunamadı" });

                var currency = dto.Currency?.ToUpper() ?? "TRY";
                decimal exchangeRate = 1m;
                decimal tryAmount = dto.Amount;

                if (currency != "TRY")
                {
                    if (dto.ManualRate.HasValue && dto.ManualRate.Value > 0)
                    {
                        exchangeRate = dto.ManualRate.Value;
                    }
                    else
                    {
                        exchangeRate = await _exchangeRateService.GetRateAsync(currency);
                        if (exchangeRate <= 0)
                            return BadRequest(new { message = $"{currency} için kur bilgisi alınamadı" });
                    }

                    tryAmount = Math.Round(dto.Amount * exchangeRate, 2);
                }

                var savingTransaction = new SavingTransaction
                {
                    SavingId = id,
                    Amount = tryAmount,
                    OriginalAmount = dto.Amount,
                    Currency = currency,
                    ExchangeRate = exchangeRate,
                    Date = dto.Date ?? DateTime.Now,
                    Note = dto.Note,
                    TransactionType = "Deposit",
                    RelatedDepositId = null
                };

                _context.SavingTransactions.Add(savingTransaction);
                saving.CurrentAmount += tryAmount;
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Added {dto.Amount} {currency} = {tryAmount} TRY to {saving.GoalName}");

                return Ok(new
                {
                    message = "Tutar başarıyla eklendi!",
                    currentAmount = saving.CurrentAmount,
                    addedTry = tryAmount,
                    exchangeRate,
                    currency
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Add to saving error: {ex.Message}");
                return StatusCode(500, new { message = "Tutar eklenirken hata oluştu" });
            }
        }

        // POST: api/saving/{savingId}/sell/{depositTxId} endpointi, belirli bir yatırma işlemini satar
        [HttpPost("{savingId}/sell/{depositTxId}")]
        public async Task<IActionResult> SellDeposit(int savingId, int depositTxId, [FromBody] SellDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var saving = await _context.Savings
                    .Include(s => s.Transactions)
                    .FirstOrDefaultAsync(s => s.SavingId == savingId && s.UserId == int.Parse(userId));

                if (saving == null)
                    return NotFound(new { message = "Birikim bulunamadı" });

                var deposit = saving.Transactions
                    .FirstOrDefault(t => t.SavingTransactionId == depositTxId
                                      && t.TransactionType == "Deposit");

                if (deposit == null)
                    return NotFound(new { message = "Yatırma işlemi bulunamadı" });

                // Zaten satılmış mı kontrol et
                bool alreadySold = saving.Transactions.Any(t => t.TransactionType == "Sale"
                                                            && t.RelatedDepositId == depositTxId);
                if (alreadySold)
                    return BadRequest(new { message = "Bu işlem zaten satılmış" });

                if (dto.SaleRate <= 0)
                    return BadRequest(new { message = "Geçerli bir satış kuru giriniz" });

                // TRY ise kur 1, değilse manuel kur kullan
                decimal saleRate = deposit.Currency == "TRY" ? 1m : dto.SaleRate;
                decimal saleAmountTry = Math.Round(deposit.OriginalAmount * saleRate, 2);

                var saleTransaction = new SavingTransaction
                {
                    SavingId = savingId,
                    Amount = saleAmountTry,
                    OriginalAmount = deposit.OriginalAmount,
                    Currency = deposit.Currency,
                    ExchangeRate = saleRate,
                    Date = DateTime.Now,
                    Note = dto.Note,
                    TransactionType = "Sale",
                    RelatedDepositId = depositTxId
                };

                _context.SavingTransactions.Add(saleTransaction);

                // Birikim toplamından alış değerini düş (alış üzerinden tutuyoruz)
                saving.CurrentAmount -= deposit.Amount;
                if (saving.CurrentAmount < 0) saving.CurrentAmount = 0;

                await _context.SaveChangesAsync();

                decimal realizedPL = saleAmountTry - deposit.Amount;

                Console.WriteLine($"✅ Sold deposit {depositTxId}: sale={saleAmountTry}, buy={deposit.Amount}, P/L={realizedPL}");

                return Ok(new
                {
                    message = "Satış başarıyla yapıldı!",
                    saleTransactionId = saleTransaction.SavingTransactionId,
                    saleAmount = saleAmountTry,
                    buyAmount = deposit.Amount,
                    realizedProfitLoss = realizedPL,
                    currentAmount = saving.CurrentAmount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Sell deposit error: {ex.Message}");
                return StatusCode(500, new { message = "Satış sırasında hata oluştu" });
            }
        }

        // DELETE: api/saving/sale/{saleTxId} endpointi, belirli bir satış işlemini geri alır (satış kaydını siler ve ilgili deposit'in alış değerini CurrentAmount'a geri ekler)
        [HttpDelete("sale/{saleTxId}")]
        public async Task<IActionResult> ReverseSale(int saleTxId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var saleTx = await _context.SavingTransactions
                    .Include(st => st.Saving)
                    .FirstOrDefaultAsync(st => st.SavingTransactionId == saleTxId
                                            && st.TransactionType == "Sale"
                                            && st.Saving!.UserId == int.Parse(userId));

                if (saleTx == null)
                    return NotFound(new { message = "Satış işlemi bulunamadı" });

                // İlgili deposit'in alış değerini bul ve CurrentAmount'a geri ekle
                var relatedDeposit = await _context.SavingTransactions
                    .FirstOrDefaultAsync(t => t.SavingTransactionId == saleTx.RelatedDepositId);

                decimal restoreAmount = relatedDeposit?.Amount ?? 0;
                saleTx.Saving!.CurrentAmount += restoreAmount;

                _context.SavingTransactions.Remove(saleTx);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Sale reversed: {saleTxId}, restored {restoreAmount} TRY");

                return Ok(new
                {
                    message = "Satış başarıyla geri alındı!",
                    currentAmount = saleTx.Saving.CurrentAmount,
                    restoredAmount = restoreAmount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Reverse sale error: {ex.Message}");
                return StatusCode(500, new { message = "Satış geri alınırken hata oluştu" });
            }
        }

        // PUT: api/saving/transaction/{transactionId}
        [HttpPut("transaction/{transactionId}")]
        public async Task<IActionResult> UpdateSavingTransaction(int transactionId, [FromBody] EditDepositDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var transaction = await _context.SavingTransactions
                    .Include(st => st.Saving)
                    .FirstOrDefaultAsync(st => st.SavingTransactionId == transactionId
                                            && st.Saving!.UserId == int.Parse(userId));

                if (transaction == null)
                    return NotFound(new { message = "İşlem bulunamadı" });

                if (transaction.TransactionType != "Deposit")
                    return BadRequest(new { message = "Sadece yatırma işlemleri düzenlenebilir" });

                // Satılmış deposit düzenlenemez
                bool isSold = await _context.SavingTransactions
                    .AnyAsync(t => t.TransactionType == "Sale"
                                && t.RelatedDepositId == transactionId);

                if (isSold)
                    return BadRequest(new { message = "Satılmış işlem düzenlenemez. Önce satışı geri alın." });

                // CurrentAmount'u güncelle (eski değeri çıkar, yeni değeri ekle)
                decimal oldAmount = transaction.Amount;

                var currency = dto.Currency?.ToUpper() ?? transaction.Currency;
                decimal exchangeRate = 1m;
                decimal tryAmount = dto.Amount;

                if (currency != "TRY")
                {
                    if (dto.ManualRate.HasValue && dto.ManualRate.Value > 0)
                    {
                        exchangeRate = dto.ManualRate.Value;
                    }
                    else
                    {
                        exchangeRate = await _exchangeRateService.GetRateAsync(currency);
                        if (exchangeRate <= 0)
                            return BadRequest(new { message = $"{currency} için kur bilgisi alınamadı" });
                    }
                    tryAmount = Math.Round(dto.Amount * exchangeRate, 2);
                }

                // Saving CurrentAmount'ı güncelle
                transaction.Saving!.CurrentAmount -= oldAmount;
                transaction.Saving.CurrentAmount += tryAmount;
                if (transaction.Saving.CurrentAmount < 0)
                    transaction.Saving.CurrentAmount = 0;

                // Transaction'ı güncelle
                transaction.Amount = tryAmount;
                transaction.OriginalAmount = dto.Amount;
                transaction.Currency = currency;
                transaction.ExchangeRate = exchangeRate;
                transaction.Date = dto.Date ?? transaction.Date;
                transaction.Note = dto.Note;

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Deposit updated: {transactionId}");
                return Ok(new
                {
                    message = "İşlem başarıyla güncellendi!",
                    newAmount = tryAmount,
                    currentAmount = transaction.Saving.CurrentAmount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Update deposit error: {ex.Message}");
                return StatusCode(500, new { message = "İşlem güncellenirken hata oluştu" });
            }
        }

        // DELETE: api/saving/transaction/{transactionId} (sadece Deposit silmek için) endpointi, belirli bir işlemi siler (yalnızca henüz satılmamış deposit'ler için geçerli, satış işlemi silinemez)
        [HttpDelete("transaction/{transactionId}")]
        public async Task<IActionResult> DeleteSavingTransaction(int transactionId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var transaction = await _context.SavingTransactions
                    .Include(st => st.Saving)
                    .FirstOrDefaultAsync(st => st.SavingTransactionId == transactionId
                                            && st.Saving!.UserId == int.Parse(userId));

                if (transaction == null)
                    return NotFound(new { message = "İşlem bulunamadı" });

                // Sadece Deposit'leri bu endpoint silebilir
                if (transaction.TransactionType != "Deposit")
                    return BadRequest(new { message = "Satış işlemini silmek için 'Geri Al' butonunu kullanın." });

                // Satılmış deposit silinemez (Seçenek A)
                bool isSold = await _context.SavingTransactions
                    .AnyAsync(t => t.TransactionType == "Sale" && t.RelatedDepositId == transactionId);

                if (isSold)
                    return BadRequest(new { message = "Bu yatırma işlemi satılmış. Önce ilgili satışı geri alın." });

                // CurrentAmount'tan düş
                transaction.Saving!.CurrentAmount -= transaction.Amount;
                if (transaction.Saving.CurrentAmount < 0)
                    transaction.Saving.CurrentAmount = 0;

                _context.SavingTransactions.Remove(transaction);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Deposit deleted: {transactionId}");
                return Ok(new { message = "İşlem başarıyla silindi!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Delete saving transaction error: {ex.Message}");
                return StatusCode(500, new { message = "İşlem silinirken hata oluştu" });
            }
        }

        // DELETE: api/saving/{id} endpointi, belirli bir birikim hedefini ve tüm ilişkili işlemlerini siler
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSaving(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "Kullanıcı bulunamadı" });

                var saving = await _context.Savings
                    .FirstOrDefaultAsync(s => s.SavingId == id && s.UserId == int.Parse(userId));

                if (saving == null)
                    return NotFound(new { message = "Birikim bulunamadı" });

                _context.Savings.Remove(saving);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Saving deleted: {saving.GoalName}");
                return Ok(new { message = "Birikim başarıyla silindi!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Delete saving error: {ex.Message}");
                return StatusCode(500, new { message = "Birikim silinirken hata oluştu" });
            }
        }
    }

    public class SavingDto
    {
        public string GoalName { get; set; } = "";
        public decimal TargetAmount { get; set; }
        public DateTime? TargetDate { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
    }

    public class AddAmountDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TRY";
        public DateTime? Date { get; set; }
        public string? Note { get; set; }
        public decimal? ManualRate { get; set; }
    }
    public class EditDepositDto
    {
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public decimal? ManualRate { get; set; }
        public DateTime? Date { get; set; }
        public string? Note { get; set; }
    }
    public class SellDto
    {
        public decimal SaleRate { get; set; }
        public string? Note { get; set; }
    }
}