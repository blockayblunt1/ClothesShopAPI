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

Console.WriteLine($"Database connection source: {(Environment.GetEnvironmentVariable("DATABASE_URL") != null ? "Environment Variable" : "Configuration")}");
Console.WriteLine($"Connection string present: {!string.IsNullOrEmpty(connectionString)}");

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

    finalConnectionString = $"Host={host};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;Command Timeout=30;Timeout=30;";
}
catch (Exception)
{
    finalConnectionString = connectionString; // Fallback to original
}

// Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(finalConnectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(30);
    });
    
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
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

// Add startup logging
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("Application starting up...");
Console.WriteLine("Application starting up...");

// Run database migrations
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    Console.WriteLine("Starting database migration...");
    logger.LogInformation("Starting database migration...");
    
    // Ensure database exists and run migrations
    await context.Database.MigrateAsync();
    
    Console.WriteLine("Database migration completed successfully.");
    logger.LogInformation("Database migration completed successfully.");
    
    // Test database connection and verify tables
    var canConnect = await context.Database.CanConnectAsync();
    Console.WriteLine($"Database connection test: {(canConnect ? "SUCCESS" : "FAILED")}");
    
    if (canConnect)
    {
        // Verify users table exists by querying it
        var userCount = await context.Users.CountAsync();
        Console.WriteLine($"Users table verified. Current user count: {userCount}");
        logger.LogInformation("Users table verified. Current user count: {UserCount}", userCount);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Database migration error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    
    // Don't throw - let the app start but log the error
    var logger = app.Services.GetService<ILogger<Program>>();
    logger?.LogError(ex, "Failed to run database migrations: {ErrorMessage}", ex.Message);
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECommerce API V1");
    c.RoutePrefix = "swagger";
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Database initialization endpoint (for manual trigger)
app.MapPost("/init-db", async (IServiceProvider services) =>
{
    try
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Manual database initialization started");
        
        // Check if we can connect
        var canConnect = await context.Database.CanConnectAsync();
        if (!canConnect)
        {
            return Results.BadRequest(new { error = "Cannot connect to database" });
        }
        
        // Run migrations
        await context.Database.MigrateAsync();
        
        // Verify tables exist by checking if we can query users
        var userCount = await context.Users.CountAsync();
        
        logger.LogInformation("Database initialization completed successfully. User count: {UserCount}", userCount);
        
        return Results.Ok(new { 
            status = "success", 
            message = "Database initialized successfully",
            userCount = userCount,
            timestamp = DateTime.UtcNow 
        });
    }
    catch (Exception ex)
    {
        var logger = services.GetService<ILogger<Program>>();
        logger?.LogError(ex, "Database initialization failed");
        
        return Results.BadRequest(new { 
            error = "Database initialization failed", 
            message = ex.Message,
            timestamp = DateTime.UtcNow 
        });
    }
});

// Error handling endpoint
app.Map("/error", () => Results.Problem("An error occurred processing your request."));

// Middleware pipeline
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Configure port (only if PORT environment variable is set for deployment)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    var urls = $"http://0.0.0.0:{port}";
    app.Urls.Add(urls);
}

app.Run();
