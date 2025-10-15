using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ClothesShopAPI.Data;
using ClothesShopAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Get database connection string
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("No database connection string found.");
}

// Convert URI format to Npgsql connection string format
string finalConnectionString;
try
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');
    var username = userInfo[0];
    var password = userInfo.Length > 1 ? userInfo[1] : "";
    var host = uri.Host;
    var database = uri.AbsolutePath.TrimStart('/');

    finalConnectionString = $"Host={host};Database={database};Username={username};Password={password};SSL Mode=Prefer;Trust Server Certificate=true";
}
catch (Exception)
{
    finalConnectionString = connectionString; // Fallback to original
}

// Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(finalConnectionString);
});

// Add JWT service
builder.Services.AddScoped<IJwtService, JwtService>();

// Add Stripe service
builder.Services.AddScoped<ClothesShopAPI.Services.IStripeService, ClothesShopAPI.Services.StripeService>();

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:SecretKey"] ?? "YourVeryLongSecretKeyThatIsAtLeast32Characters!";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ClothesShopAPI",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ClothesShopAPI",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000"
            )
            .SetIsOriginAllowed(origin =>
            {
                // Allow any Vercel app domain
                return origin.Contains("vercel.app") ||
                       origin.StartsWith("http://localhost") ||
                       origin.StartsWith("https://localhost");
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        });
});

// Override appsettings with environment variables for Stripe if they exist
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY")))
{
    builder.Configuration["Stripe:SecretKey"] = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
}
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY")))
{
    builder.Configuration["Stripe:PublishableKey"] = Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY");
}
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET")))
{
    builder.Configuration["Stripe:WebhookSecret"] = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");
}

var app = builder.Build();

// Ensure database is created
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.EnsureCreatedAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Database initialization error: {ex.Message}");
}

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECommerce API V1");
    c.RoutePrefix = "swagger";
});

// Middleware pipeline
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Configure port (only if PORT environment variable is set for deployment)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    var urls = $"http://0.0.0.0:{port}";
    app.Urls.Add(urls);
}

app.Run();
