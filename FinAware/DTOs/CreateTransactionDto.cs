using System.ComponentModel.DataAnnotations;

namespace FinAware.API.DTOs
{
    public class CreateTransactionDto
    {
        [Required]
        public int CategoryId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public string Type { get; set; } = "Expense"; 

        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        public DateTime? ReminderDate { get; set; }
    }

   
}