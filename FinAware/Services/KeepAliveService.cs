namespace FinAware.API.Services
{
    public class KeepAliveService : BackgroundService
    {
        private readonly ILogger<KeepAliveService> _logger;
        private readonly IConfiguration _configuration;

        public KeepAliveService(ILogger<KeepAliveService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🏓 Keep-alive servisi başlatıldı");
            await Task.Delay(30000, stoppingToken);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var baseUrl = _configuration["AppSettings:ApiBaseUrl"]
                                  ?? "https://finaware-uq2x.onrender.com";
                    await client.GetAsync($"{baseUrl}/api/health", stoppingToken);
                    _logger.LogInformation("🏓 Keep-alive ping gönderildi");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Keep-alive ping başarısız: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}