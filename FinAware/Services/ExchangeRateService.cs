using System.Xml.Linq;

namespace FinAware.API.Services
{
    public class ExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private static Dictionary<string, decimal> _cachedRates = new();
        private static DateTime _lastFetch = DateTime.MinValue;
        private const int CacheMinutes = 30;

        public ExchangeRateService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        
        public async Task<Dictionary<string, decimal>> GetRatesAsync()
        {
            if (_cachedRates.Any() && (DateTime.Now - _lastFetch).TotalMinutes < CacheMinutes)
            {
                Console.WriteLine("📊 Using cached rates");
                return _cachedRates;
            }

            try
            {
                Console.WriteLine("📡 Fetching rates from TCMB...");
                var xml = await _httpClient.GetStringAsync("https://www.tcmb.gov.tr/kurlar/today.xml");

                var doc = XDocument.Parse(xml);
                var rates = new Dictionary<string, decimal>();

                
                rates["TRY"] = 1m;

                foreach (var currency in doc.Descendants("Currency"))
                {
                    var code = currency.Attribute("CurrencyCode")?.Value;
                    var forexSelling = currency.Element("ForexSelling")?.Value;
                    var unit = currency.Element("Unit")?.Value;

                    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(forexSelling))
                        continue;

                    if (decimal.TryParse(forexSelling.Replace(",", "."),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out decimal rate) && rate > 0)
                    {
                        
                        if (int.TryParse(unit, out int unitCount) && unitCount > 1)
                            rate = rate / unitCount;

                        rates[code] = rate;
                    }
                }

                
                await AddMetalRatesAsync(rates);

                _cachedRates = rates;
                _lastFetch = DateTime.Now;

                Console.WriteLine($"✅ Fetched {rates.Count} rates from TCMB");
                return rates;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ TCMB fetch error: {ex.Message}");

                
                if (_cachedRates.Any())
                    return _cachedRates;

                
                return GetFallbackRates();
            }
        }
        private async Task AddMetalRatesAsync(Dictionary<string, decimal> rates)
        {
            try
            {
                if (rates.ContainsKey("USD"))
                {
                    
                    decimal goldOzUsd = 2300m; 

                    try
                    {
                        var goldResponse = await _httpClient.GetStringAsync(
                            "https://api.metals.live/v1/spot/gold");
                        var goldData = System.Text.Json.JsonDocument.Parse(goldResponse);

                        if (goldData.RootElement.TryGetProperty("price", out var priceEl))
                            goldOzUsd = priceEl.GetDecimal();
                    }
                    catch
                    {
                        Console.WriteLine("⚠️ Gold API failed, using fallback price");
                    }

                    
                    rates["XAU"] = Math.Round((goldOzUsd / 31.1035m) * rates["USD"], 2);
                    decimal silverOzUsd = 27m;
                    rates["XAG"] = Math.Round((silverOzUsd / 31.1035m) * rates["USD"], 2);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Metal rates error: {ex.Message}");

                
                if (rates.ContainsKey("USD"))
                {
                    rates["XAU"] = Math.Round((2300m / 31.1035m) * rates["USD"], 2);
                    rates["XAG"] = Math.Round((27m / 31.1035m) * rates["USD"], 2);
                }
            }
        }

        
        public async Task<decimal> GetRateAsync(string currency)
        {
            if (currency == "TRY") return 1m;

            var rates = await GetRatesAsync();
            return rates.TryGetValue(currency.ToUpper(), out var rate) ? rate : 0;
        }

        
        public async Task<decimal> ConvertTryToCurrencyAsync(decimal tryAmount, string currency)
        {
            var rate = await GetRateAsync(currency);
            if (rate <= 0) return 0;
            return Math.Round(tryAmount / rate, 4);
        }

        
        public async Task<decimal> ConvertToTryAsync(decimal amount, string currency)
        {
            var rate = await GetRateAsync(currency);
            return Math.Round(amount * rate, 2);
        }

        
        public List<CurrencyInfo> GetSupportedCurrencies()
        {
            return new List<CurrencyInfo>
            {
                new CurrencyInfo { Code = "TRY", Name = "Türk Lirası", Icon = "₺", IsDefault = true },
                new CurrencyInfo { Code = "USD", Name = "Amerikan Doları", Icon = "$" },
                new CurrencyInfo { Code = "EUR", Name = "Euro", Icon = "€" },
                new CurrencyInfo { Code = "GBP", Name = "İngiliz Sterlini", Icon = "£" },
                new CurrencyInfo { Code = "CHF", Name = "İsviçre Frangı", Icon = "Fr" },
                new CurrencyInfo { Code = "JPY", Name = "Japon Yeni", Icon = "¥" },
                new CurrencyInfo { Code = "SAR", Name = "Suudi Riyali", Icon = "﷼" },
                new CurrencyInfo { Code = "AED", Name = "BAE Dirhemi", Icon = "د.إ" },
                new CurrencyInfo { Code = "XAU", Name = "Altın (gram)", Icon = "🥇" },
                new CurrencyInfo { Code = "XAG", Name = "Gümüş (gram)", Icon = "🥈" },
            };
        }

        private Dictionary<string, decimal> GetFallbackRates()
        {
            return new Dictionary<string, decimal>
            {
                ["TRY"] = 1m,
                ["USD"] = 32.50m,
                ["EUR"] = 35.20m,
                ["GBP"] = 41.30m,
                ["CHF"] = 36.80m,
                ["JPY"] = 0.22m,
                ["SAR"] = 8.67m,
                ["AED"] = 8.85m,
                ["XAU"] = 2100m,
                ["XAG"] = 27m,
            };
        }
    }

    public class CurrencyInfo
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public bool IsDefault { get; set; } = false;
    }
}