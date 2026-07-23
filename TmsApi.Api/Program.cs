using Asp.Versioning;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

using TmsApi.Api.ExceptionHandlers;
using TmsApi.Api.Filters;
using TmsApi.Api.Middlewares;

using TmsApi.Application;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;

using TmsApi.Domain.Entities;

using TmsApi.Infrastructure.Persistence;
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


var app = builder.Build();


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


// =====================================================
// STATUS CODE PAGES
// =====================================================

app.UseStatusCodePages();


// =====================================================
// CONTROLLERS
// =====================================================

app.MapControllers();


// =====================================================
// RUN APPLICATION
// =====================================================

app.Run();