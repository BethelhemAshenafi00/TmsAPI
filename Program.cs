using Microsoft.AspNetCore.Authentication;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Authentication
builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions,
        TrainingAuthHandler>("Training", null);

// Authorization
builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();

// ProblemDetails (Exercise 6)
builder.Services.AddProblemDetails();

// OpenAPI (Exercise 7)
/* builder.Services.AddOpenApi();
 */
// Background Worker
builder.Services.AddSingleton<EnrollmentWorker>();

// Options Pattern + Validation
builder.Services
    .AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Enrollment Service
builder.Services.AddScoped<
    IEnrollmentService,
    EnrollmentService>();

// Dependency Validation
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

var app = builder.Build();


// ========================================
// Exercise 7 - Development Environment
// ========================================
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}


// ========================================
// Exercise 6 - Global Error Handling
// ========================================
app.UseExceptionHandler();

app.UseStatusCodePages();


// ========================================
// Middleware Pipeline
// ========================================
app.UseRouting();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();


// ========================================
// Controllers
// ========================================
app.MapControllers();


// ========================================
// Protected Endpoint (Session 1)
// ========================================
app.MapGet("/api/assessments/results", () =>
{
    return Results.Ok(new
    {
        courseCode = "CS-101",
        studentId = "S-001",
        letterGrade = "A"
    });
})
.RequireAuthorization();


// ========================================
// Worker Test Endpoint (Session 2)
// ========================================
app.MapGet("/api/enrollments/worker-smoke",
    (EnrollmentWorker worker) =>
{
    worker.ProcessBatch();

    return Results.Ok("processed");
});


// ========================================
// Exercise 6 - ProblemDetails Test Route
// ========================================
app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException(
        "Simulated database failure for ProblemDetails testing");
});


app.Run();