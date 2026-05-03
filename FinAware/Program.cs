using FinAware.API.Data;
using FinAware.API.Models;
using FinAware.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        var mvcUrl = builder.Configuration["AppSettings:MvcBaseUrl"]
                     ?? "https://localhost:7023";

        policy.WithOrigins(
                    mvcUrl,
                    "https://localhost:7023",
                    "http://localhost:5285",
                    "https://finawaremvc-production.up.railway.app"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

if (string.IsNullOrEmpty(secretKey))
{
    throw new Exception("JWT SecretKey is not configured in appsettings.json!");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });
builder.Services.AddHttpClient();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ExchangeRateService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<MonthlyReportService>();
builder.Services.AddHostedService<MonthlyReportHostedService>();


var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


Console.WriteLine("🚀 FinAware API Started!");
Console.WriteLine("📡 Listening on: https://localhost:7061");
Console.WriteLine("📋 Swagger: https://localhost:7061/swagger");
Console.WriteLine("🔓 CORS: Enabled (AllowAll for testing)");
Console.WriteLine($"🔑 JWT Issuer: {jwtSettings["Issuer"]}");
Console.WriteLine($"🔑 JWT Audience: {jwtSettings["Audience"]}");



using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        
        dbContext.Database.EnsureCreated();

       
        if (!dbContext.Categories.Any())
        {
           
            Console.WriteLine("🏷️ SEEDING DEFAULT CATEGORIES...");

            var defaultCategories = new List<Category>
            {
                new Category { Name = "Market", Icon = "🛒", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Yemek", Icon = "🍔", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Ulaşım", Icon = "🚗", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Faturalar", Icon = "🧾", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Sağlık", Icon = "💊", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Eğlence", Icon = "🎬", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Giyim", Icon = "👔", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Teknoloji", Icon = "💻", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Ev", Icon = "🏠", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Eğitim", Icon = "📚", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Spor", Icon = "⚽", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Diğer", Icon = "📦", CreatedAt = DateTime.UtcNow }
            };

            dbContext.Categories.AddRange(defaultCategories);
            dbContext.SaveChanges();

            Console.WriteLine($"✅ {defaultCategories.Count} default categories added!");
        }
        else
        {
            Console.WriteLine($"✅ Categories already exist: {dbContext.Categories.Count()} categories found");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Category seeding error: {ex.Message}");
    }
}

app.Run();