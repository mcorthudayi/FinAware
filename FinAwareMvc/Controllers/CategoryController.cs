using Microsoft.AspNetCore.Mvc;
using FinAware.MVC.Models.ViewModels;
using FinAware.MVC.Services;
using FinAware.MVC.Filters;

namespace FinAware.MVC.Controllers
{
    [AuthorizeFilter]
    public class CategoryController : Controller
    {
        private readonly IApiService _apiService;

        public CategoryController(IApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var categories = await _apiService.GetCategoriesAsync();
                return View(categories);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Kategoriler yüklenemedi: {ex.Message}";
                return View(new List<CategoryViewModel>());
            }
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryViewModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    TempData["Error"] = "Kategori adı gereklidir";
                    return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.Type))
                {
                    TempData["Error"] = "Lütfen kategori tipini seçin";
                    return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.Icon))
                    model.Icon = "📁";

                if (string.IsNullOrWhiteSpace(model.Color))
                    model.Color = "#4DB6AC";

                var success = await _apiService.CreateCategoryAsync(model);

                if (success)
                {
                    TempData["Success"] = "Kategori başarıyla eklendi!";
                    return RedirectToAction("Index");
                }

                TempData["Error"] = "Kategori eklenemedi. Lütfen tekrar deneyin.";
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
                return View(model);
            }
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var success = await _apiService.DeleteCategoryAsync(id);

                if (success)
                {
                    TempData["Success"] = "Kategori başarıyla silindi!";
                }
                else
                {
                    TempData["Error"] = "Kategori silinemedi! Bu kategoriye ait işlemler olabilir. Önce işlemleri silin veya başka bir kategoriye taşıyın.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}