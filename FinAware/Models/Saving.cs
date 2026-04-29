namespace FinAware.API.Models
{
    public class Saving
    {
        public int SavingId { get; set; }
        public int UserId { get; set; }
        public string GoalName { get; set; } = "";
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public DateTime? TargetDate { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public DateTime CreatedAt { get; set; }
        public User? User { get; set; }
        public ICollection<SavingTransaction> Transactions { get; set; } = new List<SavingTransaction>();
    }

    public class SavingTransaction
    {
        public int SavingTransactionId { get; set; }
        public int SavingId { get; set; }
        public decimal Amount { get; set; }
        public decimal OriginalAmount { get; set; }
        public string Currency { get; set; } = "TRY"; 
        public decimal ExchangeRate { get; set; } = 1; 

        public DateTime Date { get; set; } = DateTime.Now;
        public string? Note { get; set; }
        public string TransactionType { get; set; } = "Deposit";
        public int? RelatedDepositId { get; set; }

        
        public Saving? Saving { get; set; }
    }
}