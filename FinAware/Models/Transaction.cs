namespace FinAware.API.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public int UserId { get; set; }
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Expense";
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime? ReminderDate { get; set; }
        public bool IsReminded { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public decimal OriginalAmount { get; set; } = 0;
        public string OriginalCurrency { get; set; } = "TRY";
        public decimal ExchangeRate { get; set; } = 1;

        
        public User User { get; set; } = null!;
        public Category Category { get; set; } = null!;
    }
}