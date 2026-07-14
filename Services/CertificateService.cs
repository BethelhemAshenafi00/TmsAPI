using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Dtos;
using TmsApi.Entities;
using TmsApi.Services;

namespace TmsApi.Services;
public class CertificateService : ICertificateService
{
    private readonly TmsDbContext _context;
    public CertificateService(TmsDbContext context)
    {
        _context = context;
    }
    // Get by id
    public async Task<CertificateResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _context.Certificates
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CertificateResponseDto(
                c.Id,
                c.SerialNumber,
                c.IssuedAt,
                c.StudentId,
                c.CourseId))
            .FirstOrDefaultAsync(ct);
    }
    // Create
    public async Task<CertificateResponseDto> CreateAsync(CreateCertificateRequest request, CancellationToken ct)
    {
        var certificate = new Certificate
        {
            SerialNumber = request.SerialNumber,
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            IssuedAt = request.IssuedAt
        };

        _context.Certificates.Add(certificate);
        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(certificate.Id, ct))!;
    }
    // List all
    public async Task<List<CertificateResponseDto>> ListAsync(CancellationToken ct)
    {
        return await _context.Certificates
            .AsNoTracking()
            .Select(c => new CertificateResponseDto(
                c.Id,
                c.SerialNumber,
                c.IssuedAt,
                c.StudentId,
                c.CourseId))
            .ToListAsync(ct);
    }   
}