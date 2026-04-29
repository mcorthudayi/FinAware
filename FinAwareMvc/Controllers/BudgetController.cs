using FinAware.MVC.Models.ViewModels;
using FinAware.MVC.Services;
using FinAware.MVC.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FinAware.MVC.Controllers
{
    [AuthorizeFilter]
    public class BudgetController : Controller
    {
        private readonly IApiService _apiService;

        public BudgetController(IApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index(int? month, int? year)
        {
            var targetMonth = month ?? DateTime.Now.Month;
            var targetYear = year ?? DateTime.Now.Year;

            var model = await _apiService.GetBudgetsAsync(targetMonth, targetYear);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int? categoryId, decimal limitAmount, int month, int year)
        {
            try
            {
                if (limitAmount <= 0)
                {
                    TempData["Error"] = "Limit tutarı 0'dan büyük olmalıdır.";
                    return RedirectToAction("Index", new { month, year });
                }

                var success = await _apiService.CreateBudgetAsync(categoryId, limitAmount, month, year);

                if (success)
                    TempData["Success"] = "Bütçe başarıyla oluşturuldu!";
                else
                    TempData["Error"] = "Bütçe oluşturulamadı.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Budget create error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu.";
            }

            return RedirectToAction("Index", new { month, year });
        }

   
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int month, int year)
        {
            try
            {
                var success = await _apiService.DeleteBudgetAsync(id);

                if (success)
                    TempData["Success"] = "Bütçe silindi!";
                else
                    TempData["Error"] = "Bütçe silinemedi.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Budget delete error: {ex.Message}");
                TempData["Error"] = "Bir hata oluştu.";
            }

            return RedirectToAction("Index", new { month, year });
        }
    }
}