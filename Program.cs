using System.Text;

// rate limiting 
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using mysystem_bff.Services.Helpers;

// auth tokens
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using MySqlConnector;

// services
using mysystem_bff.Services.Interfaces;
using mysystem_bff.Services.Services;

// serilog logging
using Serilog;

// ======================================================================

// configure serilog

var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "log-.txt");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 62,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}"
    )
    .CreateLogger();

// ======================================================================

// configure builder

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Configuration.AddJsonFile("secrets.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers();

builder.Services.AddScoped<MySqlConnection>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    return new MySqlConnection(connectionString);
});

// ======================================================================

// authentication / authorisation

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new Exception("JWT key is missing.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            )
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdministratorOnly", policy =>
    {
        policy.RequireRole("Administrator");
    });

    options.AddPolicy("AdministrationAccess", policy =>
    {
        policy.RequireRole("Administrator", "Staff");
    });
});

// ======================================================================

// local data services

builder.Services.AddScoped<IAdminUserReadService, AdminUserReadService>();
builder.Services.AddScoped<IAdminUserCreateService, AdminUserCreateService>();
builder.Services.AddScoped<IAdminUserUpdateService, AdminUserUpdateService>();
builder.Services.AddScoped<IAdminUserStatusService, AdminUserStatusService>();
builder.Services.AddScoped<IAdminUserPasswordService, AdminUserPasswordService>();
builder.Services.AddScoped<IAdminRoleService, AdminRoleService>();
builder.Services.AddScoped<IPortalAccessService, PortalAccessService>();
builder.Services.AddScoped<IPasswordUpdateService, PasswordUpdateService>();
builder.Services.AddScoped<IPortalEmailService, GraphEmailService>();

// usage tracking / logging services

builder.Services.AddSingleton<EndpointUsageTracker>();
builder.Services.AddHostedService<EndpointUsageWriterService>();

// mmapi data services

builder.Services.AddHttpClient<IMiddlewareSitesService, MiddlewareSitesService>();
builder.Services.AddHttpClient<IMiddlewareCallsService, MiddlewareCallsService>();
builder.Services.AddHttpClient<IMiddlewareSiteSystemsService, MiddlewareSiteSystemsService>();
builder.Services.AddHttpClient<IMiddlewareReferenceService, MiddlewareReferenceService>();
builder.Services.AddHttpClient<IMiddlewareSmsService, MiddlewareSmsService>();
builder.Services.AddHttpClient<IMiddlewareCallActionsService, MiddlewareCallActionsService>();

// frontend dashboard services

builder.Services.AddScoped<
    ICallsDashboardService,
    CallsDashboardService>();

builder.Services.AddScoped<
    IMaintenanceDashboardService,
    MaintenanceDashboardService>();


builder.Services.AddScoped<
    ISlaDashboardService, 
    SlaDashboardService>();

builder.Services.AddScoped<
    IDashboardDataService,
    DashboardDataService>();

// mmapi authentication service

builder.Services.AddHttpClient<IMiddlewareAuthService, MiddlewareAuthService>();

// ======================================================================

// CORS Policies - allow web traffic

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://localhost:5174",
                "https://mysystem.thekirbygroup.co.uk",
                "https://mysystem.info"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ======================================================================

// rate limiting

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    // global api protection

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context =>
        {
            var partitionKey = RateLimitHelpers.GetRateLimitPartitionKey(context);

            return RateLimitPartition
                .GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 180,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
        });

    // login

    options.AddPolicy(
        "Login",
        context =>
        {
            var partitionKey =
                RateLimitHelpers.GetRateLimitPartitionKey(context);

            return RateLimitPartition
                .GetFixedWindowLimiter(
                    partitionKey,
                    _ =>
                        new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 8,
                            Window =
                                TimeSpan.FromMinutes(5),

                            QueueLimit = 0,

                            AutoReplenishment = true
                        });
        });

    // security operations / email codes

    options.AddPolicy(
        "Security",
        context =>
        {
            var partitionKey =
                RateLimitHelpers.GetRateLimitPartitionKey(context);

            return RateLimitPartition
                .GetFixedWindowLimiter(
                    partitionKey,
                    _ =>
                        new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window =
                                TimeSpan.FromMinutes(10),

                            QueueLimit = 0,

                            AutoReplenishment = true
                        });
        });

    // expensive dashboard operations

    options.AddPolicy(
        "ExpensiveDashboard",
        context =>
        {
            var partitionKey =
                RateLimitHelpers.GetRateLimitPartitionKey(context);

            return RateLimitPartition
                .GetConcurrencyLimiter(
                    partitionKey,
                    _ =>
                        new ConcurrencyLimiterOptions
                        {
                            PermitLimit = 1,
                            QueueLimit = 0,
                            QueueProcessingOrder =
                                QueueProcessingOrder
                                    .OldestFirst
                        });
        });

    // 429 rate limited response

    options.OnRejected =
        async (context, cancellationToken) =>
        {
            context.HttpContext
                .Response.StatusCode =
                StatusCodes.Status429TooManyRequests;

            context.HttpContext
                .Response.ContentType =
                "application/json";

            await context.HttpContext
                .Response.WriteAsJsonAsync(
                    new
                    {
                        message =
                            "Too many requests. Please wait and try again."
                    },
                    cancellationToken);
        };
});

// ======================================================================

// build project

try
{
    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseCors("FrontendCors");
    app.UseAuthentication();
    app.UseRateLimiter();

    // track user endpoint usage
    app.Use(async (context, next) => 
    {
        await next();

        var tracker =
            context.RequestServices
                .GetRequiredService<EndpointUsageTracker>();

        tracker.Record(context);
    });

    app.UseAuthorization();
    app.MapControllers();

    app.Run();
} 
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

