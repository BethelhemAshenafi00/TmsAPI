using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Api.Filters;
using Asp.Versioning;
using TmsApi.Api.Middlewares;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Services;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Application;



var builder = WebApplication.CreateBuilder(args);

// Controllers
// builder.Services.AddControllers();
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
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader =
    ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version")
    );
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
// Authentication
builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions,
        TrainingAuthHandler>("Training", null);

// Authorization
builder.Services.AddAuthorization();

// 

// ProblemDetails (Exercise 6)
builder.Services.AddProblemDetails();



// Background Worker
// builder.Services.AddScoped<EnrollmentWorker>();
// Student Service

//builder.Services.AddScoped<CourseService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IAssessmentService, AssessmentService>();
builder.Services.AddScoped<ICertificateService, CertificateService>();

// Options Pattern + Validation
builder.Services
    .AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Enrollment Service


// Register TmsDbContext scoped for incoming HTTP request
builder.Services.AddDbContext<TmsDbContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase")));


// Dependency Validation
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});
var app = builder.Build();



// ========================================
// Exercise 6 - Global Error Handling
// ========================================


app.UseMiddleware<RequestLoggingMiddleware>();




app.MapOpenApi();

app.MapScalarApiReference(options =>
{
    options.WithTitle("TMS API Reference")
        .WithTheme(ScalarTheme.DeepSpace)
        .WithDefaultHttpClient(
            ScalarTarget.CSharp,
            ScalarClient.HttpClient);

    options
        .AddDocument("v1", "API Version 1.0")
        .AddDocument("v2", "API Version 2.0");
});

app.UseExceptionHandler();

app.UseStatusCodePages();


// ========================================
// Middleware Pipeline
// ========================================
app.UseHttpsRedirection();
app.UseRouting();


app.UseAuthentication();

app.UseAuthorization();

app.UseMiddleware<V1DeprecationMiddleware>();
// ========================================
// Controllers
// ========================================
app.MapControllers();



// ========================================
// Protected Endpoint (Session 1)
// ========================================
// app.MapGet("/api/assessments/results", () =>
// {
//     return Results.Ok(new
//     {
//         courseCode = "CS-101",
//         studentId = "S-001",
//         letterGrade = "A"
//     });
// })
// .RequireAuthorization();


// ========================================
// Worker Test Endpoint (Session 2)
// ========================================
// app.MapGet("/api/enrollments/worker-smoke",
//     (EnrollmentWorker worker) =>
// {
//     worker.ProcessBatch();

//     return Results.Ok("processed");
// });


// ========================================
// Exercise 6 - ProblemDetails Test Route
// ========================================
// app.MapGet("/api/error", () =>
// {
//     throw new TmsDatabaseException(
//         "Simulated database failure for ProblemDetails testing");
// });



// ========= Student Endpoints ==========//

// app.MapGet("/api/students", async (IStudentService service) =>
// {
//     var students = await service.GetAllAsync();
//     return Results.Ok(students);
// });

// ========================================
// one that returns a single student
// ========================================
// app.MapGet("/api/students/{id}", async (IStudentService service, string id) =>
// {
//     var student = await service.GetByIdAsync(id);
//     return student is not null ? Results.Ok(student) : Results.NotFound();
// });



// ========================================
// one that returns all students 
// ========================================
// app.MapGet("/api/students/all", async (IStudentService service) =>
// {
//     var students = await service.GetAllAsync();
//     return Results.Ok(students);
// });

// ========== Course Endpoints ==========//
// ========================================
// one that returns a single course 
// ========================================
// app.MapGet("/api/courses/{id}", async (ICourseService service, string id) =>
// {
//     var course = await service.GetByIdAsync(id);
//     return course is not null ? Results.Ok(course) : Results.NotFound();
// });

// ========================================
// one that returns all courses
// ========================================
// app.MapGet("/api/courses/all", async (ICourseService service) =>
// {
//     var courses = await service.GetAllAsync();
//     return Results.Ok(courses);
// });

//M5-session ex2 step 2 write an auto-seeder

// using (var scope = app.Services.CreateScope())
// {
//     var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();

//     context.Database.Migrate();

//     if (!context.Students.Any())
//     {
//         var students = new List<Student>
//         {
//             new() { RegistrationNumber = "TMS-2026-0001", Name = "Alice Smith", GPA = 3.8m, IsActive = true },
//             new() { RegistrationNumber = "TMS-2026-0002", Name = "Bob Jones", GPA = 2.9m, IsActive = true },
//             new() { RegistrationNumber = "TMS-2026-0003", Name = "Charlie Brown", GPA = 3.4m, IsActive = false },
//             new() { RegistrationNumber = "TMS-2026-0004", Name = "Diana Prince", GPA = 3.9m, IsActive = true },
//             new() { RegistrationNumber = "TMS-2026-0005", Name = "Evan Wright", GPA = 2.5m, IsActive = true },
//           };

//         context.Students.AddRange(students);

//         var courses = new List<Course>
//         {
//             new() { Code = "CS-101", Title = "Introduction to Computer Science", MaxCapacity = 30 },
//             new() { Code = "CS-201", Title = "Data Structures and Algorithms", MaxCapacity = 25 },
//             new() { Code = "MAT-101", Title = "Calculus I", MaxCapacity = 40 }
//         };

//         context.Courses.AddRange(courses);

//         context.SaveChanges();

//         var enrollments = new List<Enrollment>
//         {
//             new() { StudentId = students[0].Id, CourseId = courses[0].Id, Grade = 4.0m },
//             new() { StudentId = students[0].Id, CourseId = courses[1].Id, Grade = 3.6m },
//             new() { StudentId = students[1].Id, CourseId = courses[0].Id, Grade = 2.8m },
//             new() { StudentId = students[3].Id, CourseId = courses[1].Id, Grade = 3.9m }
//         };

//         context.Enrollments.AddRange(enrollments);
//         context.SaveChanges();
//     }
// }

// Run the seeder automatically during development mode

app.Run();