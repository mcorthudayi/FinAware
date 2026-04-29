using FinAware.MVC.Models.ViewModels;

namespace FinAware.MVC.Services
{
    public interface IApiService
    {
        // Auth
        Task<LoginResponse?> LoginAsync(string email, string password);
        Task<bool> RegisterAsync(string username, string email, string password);

        // Transactions
        Task<List<TransactionViewModel>> GetTransactionsAsync();
        Task<bool> CreateTransactionAsync(TransactionViewModel transaction);
        Task<bool> DeleteTransactionAsync(int id);

        // Categories
        Task<List<CategoryViewModel>> GetCategoriesAsync();
        Task<bool> CreateCategoryAsync(CategoryViewModel category);
        Task<bool> DeleteCategoryAsync(int id);

        // Dashboard
        Task<DashboardViewModel> GetDashboardSummaryAsync();

        // Change Password
        Task<bool> ChangePasswordAsync(string currentPassword, string newPassword);

        // Notifications
        Task<List<NotificationViewModel>> GetNotificationsAsync();
        Task<int> GetUnreadCountAsync();
        Task<bool> MarkAsReadAsync(int notificationId);
        Task<bool> MarkAllAsReadAsync();
        Task<bool> DeleteNotificationAsync(int notificationId);

        // Budget
        Task<BudgetPageViewModel> GetBudgetsAsync(int month, int year);
        Task<bool> CreateBudgetAsync(int? categoryId, decimal limitAmount, int month, int year);
        Task<bool> DeleteBudgetAsync(int id);
        Task CheckBudgetAsync();
    }
}