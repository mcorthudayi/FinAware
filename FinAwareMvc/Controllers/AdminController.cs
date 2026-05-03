using Microsoft.AspNetCore.Mvc;
using FinAware.MVC.Services;
using FinAwareMvc.Filters;

namespace FinAwareMvc.Controllers
{
    [AdminAuthorize]
    public class AdminController : Controller
    {
        private readonly IApiService _apiService;

        public AdminController(IApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Stats = await _apiService.GetAdminStatsAsync();
            ViewBag.Users = await _apiService.GetAdminUsersAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> FreezeUser(int id)
        {
            await _apiService.AdminFreezeUserAsync(id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UnfreezeUser(int id)
        {
            await _apiService.AdminUnfreezeUserAsync(id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            await _apiService.AdminDeleteUserAsync(id);
            return RedirectToAction("Index");
        }
    }
}