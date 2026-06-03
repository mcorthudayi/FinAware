using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;

namespace FinAware.API.Services
{
    public class IyzicoService
    {
        private readonly Options _options;

        public IyzicoService(IConfiguration configuration)
        {
            _options = new Options
            {
                ApiKey = configuration["Iyzico__ApiKey"]!,
                SecretKey = configuration["Iyzico__SecretKey"]!,
                BaseUrl = configuration["Iyzico__BaseUrl"]
                            ?? "https://sandbox-api.iyzipay.com"
            };
        }

        public async Task<IyzicoCheckoutResult> InitializeCheckoutFormAsync(
            string plan, int userId, string email, string username,
            string callbackUrl, string ip)
        {
            var price = plan == "Gold" ? "75" : "200";
            var planName = plan == "Gold" ? "FinAware Gold" : "FinAware Platinum";
            var conversationId = $"finaware_{userId}_{plan}_{DateTime.Now:yyyyMMddHHmmss}";

            var firstName = username.Split(' ')[0];
            var lastName = username.Split(' ').Length > 1 ? username.Split(' ')[1] : username;

            var request = new CreateCheckoutFormInitializeRequest
            {
                Locale = Locale.TR.ToString(),
                ConversationId = conversationId,
                Price = price,
                PaidPrice = price,
                Currency = Currency.TRY.ToString(),
                BasketId = conversationId,
                PaymentGroup = PaymentGroup.PRODUCT.ToString(),
                CallbackUrl = callbackUrl,
                EnabledInstallments = new List<int> { 1, 2, 3, 6, 9, 12 },

                Buyer = new Buyer
                {
                    Id = userId.ToString(),
                    Name = firstName,
                    Surname = lastName,
                    Email = email,
                    IdentityNumber = "74300864791",
                    RegistrationAddress = "Istanbul",
                    City = "Istanbul",
                    Country = "Turkey",
                    Ip = string.IsNullOrEmpty(ip) ? "85.34.78.112" : ip
                },

                ShippingAddress = new Address
                {
                    ContactName = $"{firstName} {lastName}",
                    City = "Istanbul",
                    Country = "Turkey",
                    Description = "Istanbul"
                },

                BillingAddress = new Address
                {
                    ContactName = $"{firstName} {lastName}",
                    City = "Istanbul",
                    Country = "Turkey",
                    Description = "Istanbul"
                },

                BasketItems = new List<BasketItem>
                {
                    new BasketItem
                    {
                        Id        = plan,
                        Name      = planName,
                        Category1 = "Software",
                        ItemType  = BasketItemType.VIRTUAL.ToString(),
                        Price     = price
                    }
                }
            };

            var result = await Task.Run(() =>
                CheckoutFormInitialize.Create(request, _options));

            Console.WriteLine($"📡 Iyzico Init: status={result.Status} error={result.ErrorMessage} errorCode={result.ErrorCode}");

            return new IyzicoCheckoutResult
            {
                Status = result.Status ?? "",
                CheckoutFormContent = result.CheckoutFormContent ?? "",
                Token = result.Token ?? "",
                ConversationId = conversationId,
                Plan = plan
            };
        }

        public async Task<IyzicoPaymentResult> RetrieveCheckoutFormAsync(string token)
        {
            var request = new RetrieveCheckoutFormRequest
            {
                Locale = Locale.TR.ToString(),
                Token = token
            };

            var result = await Task.Run(() =>
                CheckoutForm.Retrieve(request, _options));

            Console.WriteLine($"📡 Iyzico Retrieve: status={result.Status} payStatus={result.PaymentStatus}");

            return new IyzicoPaymentResult
            {
                Success = result.Status == "success" && result.PaymentStatus == "SUCCESS",
                ConversationId = result.ConversationId ?? "",
                PaymentId = result.PaymentId ?? "",
                Status = result.Status ?? ""
            };
        }

        public async Task<bool> RefundAsync(string paymentTransactionId, decimal price)
        {
            var request = new CreateRefundRequest
            {
                Locale = Locale.TR.ToString(),
                ConversationId = Guid.NewGuid().ToString("N")[..16],
                PaymentTransactionId = paymentTransactionId,
                Price = price.ToString("F2"),
                Currency = Currency.TRY.ToString(),
                Ip = "85.34.78.112"
            };

            var result = await Task.Run(() => Refund.Create(request, _options));
            return result.Status == "success";
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