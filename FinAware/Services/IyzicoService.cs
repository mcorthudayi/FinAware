using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FinAware.API.Services
{
    public class IyzicoService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        private string ApiKey => _configuration["Iyzico__ApiKey"]!;
        private string SecretKey => _configuration["Iyzico__SecretKey"]!;
        private string BaseUrl => _configuration["Iyzico__BaseUrl"]
                                 ?? "https://sandbox-api.iyzipay.com";

        public IyzicoService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        }

        private string GenerateAuthorizationHeader(string randomKey, string requestBody)
        {
            var hashStr = ApiKey + randomKey + SecretKey + requestBody;
            var hash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(hashStr)));
            return $"IYZWSv2 apiKey:{ApiKey}&randomKey:{randomKey}&signature:{hash}";
        }

        private string RandomKey() => Guid.NewGuid().ToString("N")[..16];

        private async Task<JsonElement> PostAsync(string path, object body)
        {
            var json = JsonSerializer.Serialize(body);
            var randomKey = RandomKey();
            var auth = GenerateAuthorizationHeader(randomKey, json);

            var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", auth);
            request.Headers.Add("x-iyzi-rnd", randomKey);

            var response = await _httpClient.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"📡 Iyzico [{path}]: {text}");
            return JsonSerializer.Deserialize<JsonElement>(text);
        }

        public async Task<IyzicoCheckoutResult> InitializeCheckoutFormAsync(
            string plan, int userId, string email, string username,
            string callbackUrl, string ip)
        {
            var price = plan == "Gold" ? "75.00" : "200.00";
            var planName = plan == "Gold" ? "FinAware Gold" : "FinAware Platinum";
            var conversationId = $"finaware_{userId}_{plan}_{DateTime.Now:yyyyMMddHHmmss}";

            var body = new
            {
                locale = "tr",
                conversationId,
                price,
                paidPrice = price,
                currency = "TRY",
                basketId = conversationId,
                paymentGroup = "SUBSCRIPTION",
                callbackUrl,
                enabledInstallments = new[] { 1, 2, 3, 6, 9, 12 },
                buyer = new
                {
                    id = userId.ToString(),
                    name = username.Split(' ')[0],
                    surname = username.Split(' ').Length > 1 ? username.Split(' ')[1] : username,
                    email,
                    identityNumber = "11111111111",
                    registrationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    lastLoginDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    registrationAddress = "Türkiye",
                    city = "Istanbul",
                    country = "Turkey",
                    ip
                },
                shippingAddress = new
                {
                    contactName = username,
                    city = "Istanbul",
                    country = "Turkey",
                    address = "Türkiye"
                },
                billingAddress = new
                {
                    contactName = username,
                    city = "Istanbul",
                    country = "Turkey",
                    address = "Türkiye"
                },
                basketItems = new[]
                {
                    new
                    {
                        id        = plan,
                        name      = planName,
                        category1 = "Yazılım",
                        itemType  = "VIRTUAL",
                        price
                    }
                }
            };

            var result = await PostAsync(
                "/payment/iyzipos/checkoutform/initialize/auth/ecom", body);

            return new IyzicoCheckoutResult
            {
                Status = result.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "",
                CheckoutFormContent = result.TryGetProperty("checkoutFormContent", out var c) ? c.GetString() ?? "" : "",
                Token = result.TryGetProperty("token", out var t) ? t.GetString() ?? "" : "",
                ConversationId = conversationId,
                Plan = plan
            };
        }

        public async Task<IyzicoPaymentResult> RetrieveCheckoutFormAsync(string token)
        {
            var body = new { locale = "tr", token };
            var result = await PostAsync(
                "/payment/iyzipos/checkoutform/auth/ecom/detail", body);

            var status = result.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            var payStatus = result.TryGetProperty("paymentStatus", out var ps) ? ps.GetString() ?? "" : "";
            var convId = result.TryGetProperty("conversationId", out var c) ? c.GetString() ?? "" : "";

            return new IyzicoPaymentResult
            {
                Success = status == "success" && payStatus == "SUCCESS",
                ConversationId = convId,
                PaymentId = result.TryGetProperty("paymentId", out var pid) ? pid.GetString() ?? "" : "",
                Status = status
            };
        }

        public async Task<bool> RefundAsync(string paymentTransactionId, decimal price)
        {
            var body = new
            {
                locale = "tr",
                conversationId = Guid.NewGuid().ToString("N")[..16],
                paymentTransactionId,
                price = price.ToString("F2"),
                currency = "TRY",
                ip = "85.34.78.112"
            };

            var result = await PostAsync("/payment/refund", body);
            var status = result.TryGetProperty("status", out var s) ? s.GetString() : "";
            return status == "success";
        }
    }

    public class IyzicoCheckoutResult
    {
        public string Status { get; set; } = "";
        public string CheckoutFormContent { get; set; } = "";
        public string Token { get; set; } = "";
        public string ConversationId { get; set; } = "";
        public string Plan { get; set; } = "";
    }

    public class IyzicoPaymentResult
    {
        public bool Success { get; set; }
        public string ConversationId { get; set; } = "";
        public string PaymentId { get; set; } = "";
        public string Status { get; set; } = "";
    }
}