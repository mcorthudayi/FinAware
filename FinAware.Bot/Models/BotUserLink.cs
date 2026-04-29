namespace FinAware.Bot.Models
{
    public class BotUserLink
    {
        public int Id { get; set; }
        public long TelegramChatId { get; set; }
        public string TelegramUsername { get; set; } = "";
        public int FinAwareUserId { get; set; }
        public string FinAwareUsername { get; set; } = "";
        public string JwtToken { get; set; } = "";
        public DateTime LinkedAt { get; set; } = DateTime.Now;
        public DateTime TokenExpiresAt { get; set; }
        public string? PendingInvoiceData { get; set; } = null;
    }
}