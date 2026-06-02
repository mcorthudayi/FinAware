using FinAware.API.Data;
using FinAware.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null
        )
    ));


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
builder.Services.AddHostedService<KeepAliveService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    Console.WriteLine("✅ Database migration tamamlandı");
}

Console.WriteLine("🚀 FinAware API Started!");
Console.WriteLine($"📡 Listening on: {(app.Environment.IsProduction() ? "http://0.0.0.0:8080" : "https://localhost:7061")}");
Console.WriteLine("🏓 Keep-alive: Aktif (10 dakikada bir ping)");
Console.WriteLine("🔓 CORS: Enabled");
Console.WriteLine($"🔑 JWT Issuer: {jwtSettings["Issuer"]}");
Console.WriteLine($"🔑 JWT Audience: {jwtSettings["Audience"]}");

app.Run();