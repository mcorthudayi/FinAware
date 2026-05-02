namespace FinAware.API.Services
{
    public class MonthlyReportHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MonthlyReportHostedService> _logger;

        public MonthlyReportHostedService(
            IServiceProvider serviceProvider,
            ILogger<MonthlyReportHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("📅 Aylık rapor servisi başlatıldı");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRun = GetNextRunTime(now);
                var delay = nextRun - now;

                _logger.LogInformation($"⏰ Sonraki rapor: {nextRun:dd.MM.yyyy HH:mm}");
                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    // Önceki ay için rapor gönder
                    var reportMonth = now.Month == 1 ? 12 : now.Month - 1;
                    var reportYear = now.Month == 1 ? now.Year - 1 : now.Year;

                    await SendReportsAsync(reportYear, reportMonth);
                }
            }
        }

        private DateTime GetNextRunTime(DateTime now)
        {
            // Her ayın 1'i saat 09:00
            var nextMonth = new DateTime(now.Year, now.Month, 1, 9, 0, 0).AddMonths(1);
            if (now < new DateTime(now.Year, now.Month, 1, 9, 0, 0))
                return new DateTime(now.Year, now.Month, 1, 9, 0, 0);
            return nextMonth;
        }

        private async Task SendReportsAsync(int year, int month)
        {
            try
            {
                _logger.LogInformation($"📊 Aylık raporlar gönderiliyor: {year}/{month}");
                using var scope = _serviceProvider.CreateScope();
                var reportService = scope.ServiceProvider.GetRequiredService<MonthlyReportService>();
                await reportService.SendMonthlyReportsAsync(year, month);
                _logger.LogInformation("✅ Aylık raporlar tamamlandı");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Aylık rapor hatası: {ex.Message}");
            }
        }
    }
}