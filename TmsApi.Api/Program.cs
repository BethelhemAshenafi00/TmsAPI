using Asp.Versioning;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

using TmsApi.Api.Notifications;
using TmsApi.Application.Notifications;
using TmsApi.Api.ExceptionHandlers;
using TmsApi.Api.Filters;
using TmsApi.Api.Middlewares;

using System.Threading.Channels;
using TmsApi.Infrastructure.Workers;
using TmsApi.Api.Hubs;

using Microsoft.AspNetCore.SignalR;
using TmsApi.Application.Hubs;


using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Api.RateLimiting;

using TmsApi.Application;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Transcripts;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;

using TmsApi.Domain.Entities;

using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Transcripts;
using TmsApi.Infrastructure.Services;

using Microsoft.Extensions.Caching.Hybrid;

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


//======================================================
// Configure the global tier-aware limiter
//======================================================

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
                                    TokensPerPeriod = 100,
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
            retryAfter = ((int)ts.TotalSeconds).ToString();
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
                Status = StatusCodes.Status429TooManyRequests,
                Type = "https://tms.local/errors/rate_limit_exceeded"
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
    .AddScheme<AuthenticationSchemeOptions,
        TrainingAuthHandler>("Training", null);


// =====================================================
// AUTHORIZATION
// =====================================================

builder.Services.AddAuthorization();


// =====================================================
// DATABASE
// =====================================================

builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("TmsDatabase")
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
    InMemoryTranscriptStatusStore>();
builder.Services.AddSingleton(
    Channel.CreateBounded<TranscriptRequest>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        }));

builder.Services.AddSingleton<ITranscriptNotificationService, SignalRTranscriptNotificationService>();
// =====================================================
// MEDIATR - CQRS
// =====================================================

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(EnrollStudentHandler).Assembly
    ));


//======================================================
// HYBRID CATCH
//======================================================

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };
});



// =====================================================
// FLUENT VALIDATION
// =====================================================

builder.Services.AddValidatorsFromAssembly(
    typeof(EnrollStudentValidator).Assembly
);




//=======================================================
//  Handle CORS
//=======================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
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
// =====================================================

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddProblemDetails();


// =====================================================
// CONTROLLERS
// =====================================================

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
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


builder.Services.AddSignalR();

var app = builder.Build();

app.MapHub<TmsHub>("/hubs/tms");

// =====================================================
// GLOBAL EXCEPTION HANDLER
// MUST BE BEFORE CONTROLLERS
// =====================================================

app.UseExceptionHandler();


// =====================================================
// REQUEST PIPELINE
// =====================================================

app.UseHttpsRedirection();

app.UseRouting();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();

app.UseAuthorization();


// =====================================================
// API VERSION 1 DEPRECATION
// Must be before MapControllers()
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
            ScalarClient.HttpClient);

    options
        .AddDocument("v1", "API Version 1.0")
        .AddDocument("v2", "API Version 2.0");
});

// app.MapHealthChecks("/health/live")
//     .DisableRateLimiting();

// app.MapHealthChecks("/health/ready")
//     .DisableRateLimiting();


// ====================================================
// Allow Angular
// ====================================================

app.UseCors("Angular");

// =====================================================
// STATUS CODE PAGES
// =====================================================

app.UseStatusCodePages();

//=======================================================
// RATE LIMITING
//=======================================================

app.UseRateLimiter();


// =====================================================
// CONTROLLERS
// =====================================================

app.MapControllers();


// =====================================================
// RUN APPLICATION
// =====================================================

app.Run();