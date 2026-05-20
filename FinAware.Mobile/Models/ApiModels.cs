using Newtonsoft.Json;

namespace FinAware.Mobile.Models;

// Auth
public class LoginResponse
{
    [JsonProperty("token")]
    public string Token { get; set; } = "";
    [JsonProperty("username")]
    public string Username { get; set; } = "";
    [JsonProperty("email")]
    public string Email { get; set; } = "";
}

public class ErrorResponse
{
    [JsonProperty("message")]
    public string Message { get; set; } = "";
}

// Transaction
public class TransactionModel
{
    [JsonProperty("transactionId")]
    public int TransactionId { get; set; }
    [JsonProperty("amount")]
    public decimal Amount { get; set; }
    [JsonProperty("type")]
    public string Type { get; set; } = "";
    [JsonProperty("description")]
    public string Description { get; set; } = "";
    [JsonProperty("date")]
    public DateTime Date { get; set; }
    [JsonProperty("originalAmount")]
    public decimal OriginalAmount { get; set; }
    [JsonProperty("originalCurrency")]
    public string OriginalCurrency { get; set; } = "TRY";
    [JsonProperty("category")]
    public CategoryModel? Category { get; set; }

    public string AmountDisplay => Type == "Income"
        ? $"+₺{Amount:N2}"
        : $"-₺{Amount:N2}";

    public Color AmountColor => Type == "Income"
        ? Color.FromArgb("#4CAF50")
        : Color.FromArgb("#F44336");

    public string Icon => Type == "Income" ? "💰" : "💸";
    public string CategoryIcon => Category?.Icon ?? "📦";
    public string CategoryName => Category?.Name ?? "Diğer";
    public string DateDisplay => Date.ToString("dd MMM yyyy");
}

public class CreateTransactionRequest
{
    [JsonProperty("amount")]
    public decimal Amount { get; set; }
    [JsonProperty("type")]
    public string Type { get; set; } = "";
    [JsonProperty("description")]
    public string Description { get; set; } = "";
    [JsonProperty("date")]
    public DateTime Date { get; set; }
    [JsonProperty("categoryId")]
    public int CategoryId { get; set; }
    [JsonProperty("currency")]
    public string Currency { get; set; } = "TRY";
}

// Category
public class CategoryModel
{
    [JsonProperty("categoryId")]
    public int CategoryId { get; set; }
    [JsonProperty("name")]
    public string Name { get; set; } = "";
    [JsonProperty("type")]
    public string Type { get; set; } = "";
    [JsonProperty("icon")]
    public string Icon { get; set; } = "📁";
    [JsonProperty("color")]
    public string Color { get; set; } = "#4DB6AC";

    public string DisplayName => $"{Icon} {Name}";
}

// Budget
public class BudgetPageModel
{
    [JsonProperty("budgets")]
    public List<BudgetModel> Budgets { get; set; } = new();
    [JsonProperty("totalExpense")]
    public decimal TotalExpense { get; set; }
}

public class BudgetModel
{
    [JsonProperty("budgetId")]
    public int BudgetId { get; set; }
    [JsonProperty("limitAmount")]
    public decimal LimitAmount { get; set; }
    [JsonProperty("spentAmount")]
    public decimal SpentAmount { get; set; }
    [JsonProperty("month")]
    public int Month { get; set; }
    [JsonProperty("year")]
    public int Year { get; set; }
    [JsonProperty("category")]
    public CategoryModel? Category { get; set; }

    public string CategoryName => Category?.Name ?? "Genel";
    public string CategoryIcon => Category?.Icon ?? "📊";
    public double Progress => LimitAmount > 0
        ? (double)(SpentAmount / LimitAmount)
        : 0;
    public string ProgressDisplay => $"₺{SpentAmount:N0} / ₺{LimitAmount:N0}";
    public Color ProgressColor => Progress >= 1
        ? Color.FromArgb("#F44336")
        : Progress >= 0.8
            ? Color.FromArgb("#FF9800")
            : Color.FromArgb("#4CAF50");
}

// Saving
public class SavingModel
{
    [JsonProperty("savingId")]
    public int SavingId { get; set; }
    [JsonProperty("goalName")]
    public string GoalName { get; set; } = "";
    [JsonProperty("targetAmount")]
    public decimal TargetAmount { get; set; }
    [JsonProperty("currentAmount")]
    public decimal CurrentAmount { get; set; }
    [JsonProperty("targetDate")]
    public DateTime? TargetDate { get; set; }
    [JsonProperty("icon")]
    public string Icon { get; set; } = "💰";
    [JsonProperty("color")]
    public string Color { get; set; } = "#4DB6AC";
    [JsonProperty("progress")]
    public decimal Progress { get; set; }

    public string ProgressDisplay => $"₺{CurrentAmount:N0} / ₺{TargetAmount:N0}";
    public double ProgressValue => TargetAmount > 0
        ? (double)(CurrentAmount / TargetAmount)
        : 0;
    public string TargetDateDisplay => TargetDate.HasValue
        ? TargetDate.Value.ToString("dd.MM.yyyy")
        : "Süresiz";
}

// Profile
public class ProfileModel
{
    [JsonProperty("username")]
    public string Username { get; set; } = "";
    [JsonProperty("email")]
    public string Email { get; set; } = "";
    [JsonProperty("emailNotificationsEnabled")]
    public bool EmailNotificationsEnabled { get; set; }
    [JsonProperty("telegramLinked")]
    public bool TelegramLinked { get; set; }
}

// Genel
public class ApiResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string ErrorMessage { get; set; } = "";

    public static ApiResult<T> Ok(T data) => new() { IsSuccess = true, Data = data };
    public static ApiResult<T> Fail(string msg) => new() { IsSuccess = false, ErrorMessage = msg };
}