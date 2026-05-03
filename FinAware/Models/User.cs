using System.ComponentModel.DataAnnotations;

namespace FinAware.API.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public string? ProfilePhoto { get; set; } = "";
        public string Role { get; set; } = "User";
        public bool IsEmailVerified { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }
        public bool EmailNotificationsEnabled { get; set; } = true;
        public long? TelegramChatId { get; set; } = null;
        public string? TelegramLinkToken { get; set; } = null;
        public DateTime? TelegramLinkedAt { get; set; } = null;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Category> Categories { get; set; } = new List<Category>();
        public ICollection<Notification>? Notifications { get; set; }
        public ICollection<Saving>? Savings { get; set; }
    }
}