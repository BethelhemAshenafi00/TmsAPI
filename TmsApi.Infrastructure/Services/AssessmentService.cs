using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;

namespace TmsApi.Infrastructure.Services;
public class AssessmentService : IAssessmentService
{
    private readonly TmsDbContext _context;

    public AssessmentService(TmsDbContext context)
    {
        _context = context;
    }

    // =========================
    // GET BY ID (DTO OUTPUT)
    // =========================
    public async Task<AssessmentResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _context.Assessments
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AssessmentResponseDto(
                a.Id,
                a.Title,
                a.MaxScore,
                a.Weight,
                a.CourseId))
            .FirstOrDefaultAsync(ct);
    }

    // =========================
    // CREATE (DTO INPUT → DTO OUTPUT)
    // =========================
    public async Task<AssessmentResponseDto> CreateAsync(CreateAssessmentRequest request, CancellationToken ct)
    {
        var assessment = new Assessment
        {
            Title = request.Title,
            MaxScore = request.MaxScore,
            Weight = request.Weight,
            CourseId = request.CourseId
        };

        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(assessment.Id, ct))!;
    }

    // =========================
    // LIST ALL ASSESSMENTS (DTO OUTPUT)
    // =========================
    public async Task<List<AssessmentResponseDto>> ListAsync(CancellationToken ct)
    {
        return await _context.Assessments
            .AsNoTracking()
            .Select(a => new AssessmentResponseDto(
                a.Id,
                a.Title,
                a.MaxScore,
                a.Weight,
                a.CourseId))
            .ToListAsync(ct);
    }
}