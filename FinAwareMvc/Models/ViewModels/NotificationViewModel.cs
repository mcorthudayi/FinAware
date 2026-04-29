namespace FinAware.MVC.Models.ViewModels
{
    public class NotificationViewModel
    {
        public int NotificationId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "Info";
        public string Icon { get; set; } = "🔔";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public string TimeAgo
        {
            get
            {
                var timeSpan = DateTime.Now - CreatedAt;

                if (timeSpan.TotalMinutes < 1)
                    return "Az önce";
                if (timeSpan.TotalMinutes < 60)
                    return $"{(int)timeSpan.TotalMinutes} dakika önce";
                if (timeSpan.TotalHours < 24)
                    return $"{(int)timeSpan.TotalHours} saat önce";
                if (timeSpan.TotalDays < 7)
                    return $"{(int)timeSpan.TotalDays} gün önce";

                return CreatedAt.ToString("dd MMM", new System.Globalization.CultureInfo("tr-TR"));
            }
        }
    }
}