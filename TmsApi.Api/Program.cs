using Asp.Versioning;
using FluentValidation;
using MediatR;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

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

using TmsApi.Application;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Hubs;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Notifications;
using TmsApi.Application.Transcripts;

using TmsApi.Domain.Entities;

using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
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
});


// =====================================================
// AUTHENTICATION
// =====================================================

builder.Services
    .AddAuthentication("Training")
    .AddScheme<
        AuthenticationSchemeOptions,
        TrainingAuthHandler
    >(
        "Training",
        null
    );


// =====================================================
// AUTHORIZATION
// =====================================================

builder.Services.AddAuthorization();


// =====================================================
// DATABASE
// =====================================================

builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration
            .GetConnectionString("TmsDatabase")
    ));


// =====================================================
// APPLICATION SERVICES
// =====================================================

builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IAssessmentService, AssessmentService>();
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();


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
// IMPORTANT: ORDER MATTERS
// Logging FIRST -> Validation SECOND
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
// RFC 7807 PROBLEMD DETAILS
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
// OPTIONS PATTERN + VALIDATION
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


// =====================================================
// STATUS CODE PAGES
// Converts empty 4xx/5xx responses into ProblemDetails
// =====================================================

app.UseStatusCodePages();


// =====================================================
// GLOBAL EXCEPTION HANDLER
// =====================================================

app.UseExceptionHandler();


// =====================================================
// HTTPS
// =====================================================

app.UseHttpsRedirection();


// =====================================================
// ROUTING
// =====================================================

app.UseRouting();


// =====================================================
// CORS
// =====================================================

app.UseCors("Angular");


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
// SIGNALR HUB
// IMPORTANT: CORS MUST MATCH "Angular"
// =====================================================

app.MapHub<TmsHub>("/hubs/tms")
   .RequireCors("Angular");


// =====================================================
// RATE LIMITING
// =====================================================

app.UseRateLimiter();


// =====================================================
// CONTROLLERS
// =====================================================

app.MapControllers();


// =====================================================
// RUN APPLICATION
// =====================================================

app.Run();