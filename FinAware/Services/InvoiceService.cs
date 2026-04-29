using System.Text;
using System.Text.Json;

namespace FinAware.API.Services
{
    public class InvoiceService
    {
        private readonly IConfiguration _configuration;

        public InvoiceService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<InvoiceAnalysisResult> AnalyzeInvoiceAsync(string imagePath)
        {
            var result = new InvoiceAnalysisResult();

            try
            {
                Console.WriteLine($"📸 Analyzing with GPT-4o Vision: {imagePath}");

                var imageBytes = await File.ReadAllBytesAsync(imagePath);
                var base64Image = Convert.ToBase64String(imageBytes);
                var extension = Path.GetExtension(imagePath).ToLower();
                var mediaType = extension switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };

                var apiKey = _configuration["OpenAI:ApiKey"]!;

                var requestBody = new
                {
                    model = "gpt-4o",
                    max_tokens = 500,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new
                                {
                                    type = "text",
                                    text = @"Bu bir fatura, fiş veya dekont görüntüsüdür. Aşağıdaki bilgileri JSON formatında çıkar:
{
  ""amount"": 0.00,
  ""date"": ""YYYY-MM-DD"",
  ""category"": ""Market|Yemek|Ulaşım|Faturalar|Sağlık|Eğlence|Giyim|Teknoloji|Eğitim|Spor|Diğer"",
  ""description"": ""kısa açıklama""
}
Kurallar:
- amount: Toplam/KDV dahil tutar (sayı, TL cinsinden, sadece rakam)
- date: Fatura tarihi YYYY-MM-DD formatında (bulunamazsa bugünün tarihi)
- category: Listeden en uygun kategori
- description: Firma adı veya kısa açıklama (max 100 karakter)
- Sadece JSON döndür, başka hiçbir şey yazma"
                                },
                                new
                                {
                                    type = "image_url",
                                    image_url = new
                                    {
                                        url = $"data:{mediaType};base64,{base64Image}"
                                    }
                                }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseText = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"📡 GPT Response: {responseText}");

                if (!response.IsSuccessStatusCode)
                {
                    result.Success = false;
                    result.ErrorMessage = $"OpenAI API hatası: {response.StatusCode}";
                    return result;
                }

                var responseJson = JsonDocument.Parse(responseText);
                var messageContent = responseJson
                    .RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                Console.WriteLine($"📄 GPT Content: {messageContent}");

                var cleanJson = messageContent!
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                var invoiceData = JsonDocument.Parse(cleanJson);
                var root = invoiceData.RootElement;

                if (root.TryGetProperty("amount", out var amountEl))
                    result.Amount = amountEl.GetDecimal();

                if (root.TryGetProperty("date", out var dateEl))
                {
                    if (DateTime.TryParse(dateEl.GetString(), out var date))
                        result.Date = date;
                    else
                        result.Date = DateTime.Now;
                }

                if (root.TryGetProperty("category", out var categoryEl))
                    result.Category = categoryEl.GetString() ?? "Diğer";

                if (root.TryGetProperty("description", out var descEl))
                    result.Description = descEl.GetString() ?? "";

                result.Success = true;
                result.Confidence = 95;

                Console.WriteLine($"✅ Amount: {result.Amount}, Date: {result.Date}, Category: {result.Category}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GPT Vision Error: {ex.Message}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
    }

    public class InvoiceAnalysisResult
    {
        public bool Success { get; set; }
        public decimal Amount { get; set; }
        public DateTime? Date { get; set; }
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public int Confidence { get; set; }
        public string ErrorMessage { get; set; } = "";
    }
}