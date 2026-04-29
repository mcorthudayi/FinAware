using System.ComponentModel.DataAnnotations;

namespace FinAware.MVC.Models.ViewModels
{
    public class TransactionViewModel
    {
        public int TransactionId { get; set; }

        [Required(ErrorMessage = "Kategori gereklidir")]
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryIcon { get; set; } = string.Empty;

        public CategoryInfo? Category { get; set; }

        [Required(ErrorMessage = "Tutar gereklidir")]
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TRY";
        public decimal? ManualRate { get; set; }
        public decimal? OriginalAmount { get; set; }
        public string? OriginalCurrency { get; set; } = "TRY";
        public decimal? ExchangeRate { get; set; }

        [Required(ErrorMessage = "Tip gereklidir")]
        public string Type { get; set; } = "Expense";

        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tarih gereklidir")]
        public DateTime Date { get; set; } = DateTime.Now;

        public DateTime? ReminderDate { get; set; }
    }
    public class CategoryInfo
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}