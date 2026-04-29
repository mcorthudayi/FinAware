using System.ComponentModel.DataAnnotations;

namespace FinAware.MVC.Models.ViewModels
{
    public class CategoryViewModel
    {
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Kategori adı giriniz")]
        [StringLength(50, ErrorMessage = "Kategori adı en fazla 50 karakter olabilir")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tip seçiniz")]
        public string Type { get; set; } = "Expense";

        [StringLength(10)]
        public string? Icon { get; set; } = "📁";

        [StringLength(10)]
        public string? Color { get; set; } = "#4DB6AC";
    }
}