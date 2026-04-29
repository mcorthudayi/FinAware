using System.ComponentModel.DataAnnotations;

namespace FinAware.API.DTOs
{
    public class TransactionDto
    {
        public int TransactionId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }

        [Required]
        public string Type { get; set; } = "Expense";

        public string? CategoryName { get; set; }
        public string? CategoryIcon { get; set; }
    }
}