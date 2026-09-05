using Asp.Versioning;
using FluentValidation;
using MediatR;

using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TmsApi.Infrastructure.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using TmsApi.Infrastructure.Identity;

using Scalar.AspNetCore;

using System.Threading.Channels;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

using TmsApi.Api.ExceptionHandlers;
using TmsApi.Api.Filters;
using TmsApi.Api.Hubs;
using TmsApi.Api.Middlewares;
using TmsApi.Api.Notifications;
using TmsApi.Api.RateLimiting;
using TmsApi.Api.Authorization;

using TmsApi.Application;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Hubs;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Notifications;
using TmsApi.Application.Transcripts;

using TmsApi.Domain.Entities;

using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Transcripts;
using TmsApi.Infrastructure.Workers;


// =====================================================
// CREATE BUILDER
// =====================================================

var builder = WebApplication.CreateBuilder(args);


// =====================================================
// API VERSIONING + OPENAPI
// =====================================================

builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = description =>
        description.GroupName == "v1";
});

builder.Services.AddOpenApi("v2", options =>
{
    options.ShouldInclude = description =>
        description.GroupName == "v2";
});

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);

        options.AssumeDefaultVersionWhenUnspecified = true;

        options.ReportApiVersions = true;

        options.ApiVersionReader =
            ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version")
            );
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });


// =====================================================
// RATE LIMITING
// =====================================================

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(
            httpContext =>
            {
                var (partitionKey, tier) =
                    ApiKeyResolver.Resolve(httpContext);

                return tier switch
                {
                    ApiKeyTier.Paid =>
                        RateLimitPartition.GetTokenBucketLimiter(
                            partitionKey: $"paid:{partitionKey}",
                            factory: _ =>
                                new TokenBucketRateLimiterOptions
                                {
                                    TokenLimit = 200,
                                    TokensPerPeriod = 200,
                                    ReplenishmentPeriod =
                                        TimeSpan.FromSeconds(10),
                                    QueueLimit = 0,
                                    AutoReplenishment = true
                                }),

                    ApiKeyTier.Free =>
                        RateLimitPartition.GetTokenBucketLimiter(
                            partitionKey: $"free:{partitionKey}",
                            factory: _ =>
                                new TokenBucketRateLimiterOptions
                                {
                                    TokenLimit = 30,
                                    TokensPerPeriod = 10,
                                    ReplenishmentPeriod =
                                        TimeSpan.FromSeconds(10),
                                    QueueLimit = 0,
                                    AutoReplenishment = true
                                }),

                    _ =>
                        RateLimitPartition.GetTokenBucketLimiter(
                            partitionKey: $"anon:{partitionKey}",
                            factory: _ =>
                                new TokenBucketRateLimiterOptions
                                {
                                    TokenLimit = 10,
                                    TokensPerPeriod = 5,
                                    ReplenishmentPeriod =
                                        TimeSpan.FromSeconds(10),
                                    QueueLimit = 0,
                                    AutoReplenishment = true
                                })
                };
            });

    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, ct) =>
    {
        var retryAfter = "10";

        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out var ts))
        {
            retryAfter =
                ((int)ts.TotalSeconds).ToString();
        }

        context.HttpContext.Response.Headers.RetryAfter =
            retryAfter;

        context.HttpContext.Response.ContentType =
            "application/problem+json";

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "Rate limit exceeded",
                Detail =
                    $"Too many requests. Retry after {retryAfter} seconds.",
                Status =
                    StatusCodes.Status429TooManyRequests,
                Type =
                    "https://tms.local/errors/rate_limit_exceeded"
            },
            ct);
    };

    options.AddConcurrencyLimiter("transcripts", opt =>
    {
        opt.PermitLimit = 5;
        opt.QueueLimit = 20;
        opt.QueueProcessingOrder =
            QueueProcessingOrder.OldestFirst;
    });

    options.AddTokenBucketLimiter("search", opt =>
    {
        opt.TokenLimit = 10;
        opt.TokensPerPeriod = 5;
        opt.ReplenishmentPeriod =
            TimeSpan.FromSeconds(10);
        opt.QueueLimit = 2;
    });

    // =================================================
    // MODULE 11 - AUTH LOGIN RATE LIMITER
    // =================================================

    options.AddFixedWindowLimiter("AuthLimiter", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});


// =====================================================
// DATABASE
// =====================================================

builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration
            .GetConnectionString("TmsDatabase")
    ));


// =====================================================
// IDENTITY
// =====================================================

builder.Services
    .AddIdentityCore<TmsUser>(options =>
    {
        // Password policy
        options.Password.RequiredLength = 12;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;

        // Lockout policy
        options.Lockout.MaxFailedAccessAttempts = 5;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<TmsDbContext>();


// =====================================================
// JWT AUTHENTICATION
// =====================================================

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (builder.Environment.IsDevelopment())
    {
        // In development: auto-generate a stable key and persist it to user-secrets
        // so the same key survives restarts. A new random key on every restart
        // would invalidate all issued tokens.
        jwtKey = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));

        var userSecretsId = "d1755de8-f931-4cf4-9a55-c8915ed957c2";
        var secretsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "UserSecrets", userSecretsId, "secrets.json");

        Directory.CreateDirectory(Path.GetDirectoryName(secretsPath)!);

        var existing = File.Exists(secretsPath)
            ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                await File.ReadAllTextAsync(secretsPath))
              ?? new Dictionary<string, object>()
            : new Dictionary<string, object>();

        existing["Jwt:Key"] = jwtKey;

        await File.WriteAllTextAsync(
            secretsPath,
            System.Text.Json.JsonSerializer.Serialize(
                existing,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine(
            "[Auth] Jwt:Key was not configured. A stable key has been generated " +
            "and saved to user-secrets. It will be reused on subsequent runs.");
    }
    else
    {
        throw new InvalidOperationException(
            "JWT configuration is missing: Jwt:Key. " +
            "Set it via an environment variable or a secrets manager.");
    }
}
var jwtIssuer =
    builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT configuration is missing: Jwt:Issuer");

var jwtAudience =
    builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT configuration is missing: Jwt:Audience");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Keep long-form ClaimTypes (e.g. ClaimTypes.Role) mapped correctly
        // when reading tokens back. .NET 8+ defaults this to false, which
        // breaks [Authorize(Roles = "...")] when roles were added via ClaimTypes.Role.
        options.MapInboundClaims = true;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                // Explicitly tell the validator which claim holds the role,
                // so ClaimsPrincipal.IsInRole / [Authorize(Roles = "...")] work correctly.
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
    });


// =====================================================
// AUTHORIZATION
// =====================================================

builder.Services.AddAuthorizationBuilder()

    // Resource-based course authorization
    .AddPolicy("CanEditCourse", policy =>
    {
        policy.Requirements.Add(
            new CourseInstructorRequirement());
    });


// Register custom authorization handler
builder.Services.AddSingleton<IAuthorizationHandler, CourseInstructorHandler>();


// =====================================================
// APPLICATION SERVICES
// =====================================================

builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IAssessmentService, AssessmentService>();
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

builder.Services.AddScoped<TokenService>();


// =====================================================
// TRANSCRIPT SERVICES
// =====================================================

builder.Services.AddSingleton<
    ITranscriptStatusStore,
    InMemoryTranscriptStatusStore
>();

builder.Services.AddSingleton(
    Channel.CreateBounded<TranscriptRequest>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        })
);

builder.Services.AddSingleton<
    ITranscriptNotificationService,
    SignalRTranscriptNotificationService
>();


// =====================================================
// OPENAPI / SWAGGER
// =====================================================

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter an access token obtained from POST /api/auth/login.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document, null),
            new List<string>()
        }
    });
});


// =====================================================
// MEDIATR - CQRS
// =====================================================

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(EnrollStudentHandler).Assembly
    )
);


// =====================================================
// HYBRID CACHE
// =====================================================

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions =
        new HybridCacheEntryOptions
        {
            Expiration =
                TimeSpan.FromMinutes(10),

            LocalCacheExpiration =
                TimeSpan.FromMinutes(2)
        };
});


// =====================================================
// FLUENT VALIDATION
// =====================================================

builder.Services.AddValidatorsFromAssembly(
    typeof(EnrollStudentValidator).Assembly
);


// =====================================================
// CORS
// =====================================================

var allowedOrigins =
    builder.Configuration
        .GetSection("AllowedOrigins")
        .Get<string[]>()
        ?? new[]
        {
            "http://localhost:4200"
        };

builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(
                TimeSpan.FromMinutes(10)
            );
    });
});


// =====================================================
// MEDIATR PIPELINE BEHAVIORS
// =====================================================

builder.Services.AddTransient(
    typeof(IPipelineBehavior<,>),
    typeof(LoggingBehavior<,>)
);

builder.Services.AddTransient(
    typeof(IPipelineBehavior<,>),
    typeof(ValidationBehavior<,>)
);


// =====================================================
// GLOBAL EXCEPTION HANDLING
// =====================================================

builder.Services.AddExceptionHandler<
    GlobalExceptionHandler
>();

builder.Services.AddProblemDetails();


// =====================================================
// CONTROLLERS
// =====================================================

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});


// =====================================================
// ANTIFORGERY / XSRF
// =====================================================

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});


// =====================================================
// PAYMENT OPTIONS
// =====================================================

builder.Services
    .AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();


// =====================================================
// DEPENDENCY VALIDATION
// =====================================================

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});


// =====================================================
// SIGNALR
// =====================================================

builder.Services.AddSignalR();


// =====================================================
// BUILD APPLICATION
// =====================================================

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var userManager =
        scope.ServiceProvider
            .GetRequiredService<UserManager<TmsUser>>();

    var roleManager =
        scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

    await IdentitySeeder.SeedAsync(
        userManager,
        roleManager);
}

await EnsureBootstrapAdminAsync(app.Services, builder.Configuration);


// =====================================================
// STATUS CODE PAGES
// =====================================================

// Return JSON problem details for 4xx/5xx instead of the default HTML page.
// This prevents the browser "Access Denied" page on 401/403 for API clients.
app.UseStatusCodePages(async ctx =>
{
    ctx.HttpContext.Response.ContentType = "application/problem+json";
    var status = ctx.HttpContext.Response.StatusCode;
    await ctx.HttpContext.Response.WriteAsJsonAsync(new
    {
        type   = $"https://httpstatuses.io/{status}",
        title  = status switch
        {
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            _   => "Error"
        },
        status
    });
});


// =====================================================
// GLOBAL EXCEPTION HANDLER
// =====================================================

app.UseExceptionHandler();


// =====================================================
// HTTPS
// =====================================================

app.UseHttpsRedirection();


// =====================================================
// SECURITY HEADERS
// MODULE 11 - EXERCISE 7
// =====================================================

app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "X-Content-Type-Options",
        "nosniff");

    context.Response.Headers.Append(
        "X-Frame-Options",
        "DENY");

    context.Response.Headers.Append(
        "Referrer-Policy",
        "strict-origin-when-cross-origin");

    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline';");

    await next();
});


// =====================================================
// OPENAPI
// =====================================================

app.MapOpenApi();


// =====================================================
// SCALAR API DOCUMENTATION
// =====================================================

app.MapScalarApiReference(options =>
{
    options
        .WithTitle("TMS API Reference")
        .WithTheme(ScalarTheme.DeepSpace)
        .WithDefaultHttpClient(
            ScalarTarget.CSharp,
            ScalarClient.HttpClient
        );

    options
        .AddDocument("v1", "API Version 1.0")
        .AddDocument("v2", "API Version 2.0");
});


// =====================================================
// SWAGGER
// =====================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// =====================================================
// ROUTING
// =====================================================

app.UseRouting();


// =====================================================
// CORS
// =====================================================

app.UseCors("Angular");


// =====================================================
// RATE LIMITING
// =====================================================

app.UseRateLimiter();


// =====================================================
// REQUEST LOGGING
// =====================================================

app.UseMiddleware<RequestLoggingMiddleware>();


// =====================================================
// AUTHENTICATION
// =====================================================

app.UseAuthentication();


// =====================================================
// AUTHORIZATION
// =====================================================

app.UseAuthorization();


// =====================================================
// XSRF TOKEN COOKIE
// =====================================================

app.Use(async (context, next) =>
{
    if (
        context.User.Identity?.IsAuthenticated == true ||
        context.Request.Cookies.ContainsKey("tms_auth")
    )
    {
        var antiforgery =
            context.RequestServices
                .GetRequiredService<IAntiforgery>();

        var tokens =
            antiforgery.GetAndStoreTokens(context);

        context.Response.Cookies.Append(
            "XSRF-TOKEN",
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,

                Secure =
                    !app.Environment.IsDevelopment(),

                SameSite =
                    SameSiteMode.Strict
            }
        );
    }

    await next(context);
});


// =====================================================
// API VERSION 1 DEPRECATION
// =====================================================

app.UseMiddleware<V1DeprecationMiddleware>();


// =====================================================
// SIGNALR HUB
// =====================================================

app.MapHub<TmsHub>("/hubs/tms")
   .RequireCors("Angular");


// =====================================================
// CONTROLLERS
// =====================================================

app.MapControllers();


// =====================================================
// RUN APPLICATION
// =====================================================

app.Run();

static async Task EnsureBootstrapAdminAsync(
    IServiceProvider services,
    IConfiguration configuration)
{
    if (!configuration.GetValue<bool>("BootstrapAdmin:Enabled"))
        return;

    var email = configuration["BootstrapAdmin:Email"];
    var password = configuration["BootstrapAdmin:Password"];
    var firstName = configuration["BootstrapAdmin:FirstName"];
    var lastName = configuration["BootstrapAdmin:LastName"];

    if (string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(password) ||
        string.IsNullOrWhiteSpace(firstName) ||
        string.IsNullOrWhiteSpace(lastName))
    {
        throw new InvalidOperationException(
            "BootstrapAdmin is enabled but its email, password, first name, or last name is missing.");
    }

    using var scope = services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TmsUser>>();

    const string adminRole = "Admin";
    if (!await roleManager.RoleExistsAsync(adminRole))
    {
        var createRoleResult = await roleManager.CreateAsync(new IdentityRole(adminRole));
        if (!createRoleResult.Succeeded)
            throw new InvalidOperationException("Unable to create the BootstrapAdmin role.");
    }

    var admin = await userManager.FindByEmailAsync(email);
    if (admin is null)
    {
        admin = new TmsUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };

        var createUserResult = await userManager.CreateAsync(admin, password);
        if (!createUserResult.Succeeded)
            throw new InvalidOperationException("Unable to create the BootstrapAdmin user.");
    }

    if (!await userManager.IsInRoleAsync(admin, adminRole))
    {
        var addRoleResult = await userManager.AddToRoleAsync(admin, adminRole);
        if (!addRoleResult.Succeeded)
            throw new InvalidOperationException("Unable to assign the BootstrapAdmin role.");
    }
}
