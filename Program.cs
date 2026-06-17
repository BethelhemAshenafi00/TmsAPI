using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Scalar.AspNetCore;
using TmsApi.Data;
using Microsoft.EntityFrameworkCore;



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
 builder.Services.AddOpenApi();
 
// Background Worker
builder.Services.AddSingleton<EnrollmentWorker>();
// Student Service

builder.Services.AddSingleton<IStudentService, StudentService>();

// Course Service
builder.Services.AddSingleton<ICourseService, CourseService>();

// Options Pattern + Validation
builder.Services
    .AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Enrollment Service
builder.Services.AddSingleton<
    IEnrollmentService,
    EnrollmentService>();

// Register TmsDbContext scoped for incoming HTTP request
builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging());

// Dependency Validation
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

var app = builder.Build();



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
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    app.MapScalarApiReference();
}



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
  


  // ========= Student Endpoints ==========//

// ========================================
// one that returns a single student
// ========================================
app.MapGet("/api/students/{id}", async (IStudentService service, string id) =>
{
    var student = await service.GetByIdAsync(id);
    return student is not null ? Results.Ok(student) : Results.NotFound();
});



// ========================================
// one that returns all students 
// ========================================
app.MapGet("/api/students/all", async (IStudentService service) =>
{
    var students = await service.GetAllAsync();
    return Results.Ok(students);
});

// ========== Course Endpoints ==========//
// ========================================
// one that returns a single course 
// ========================================
app.MapGet("/api/courses/{id}", async (ICourseService service, string id) =>
{
    var course = await service.GetByIdAsync(id);
    return course is not null ? Results.Ok(course) : Results.NotFound();
});

// ========================================
// one that returns all courses
// ========================================
app.MapGet("/api/courses/all", async (ICourseService service) =>
{
    var courses = await service.GetAllAsync();
    return Results.Ok(courses);
});



app.Run();