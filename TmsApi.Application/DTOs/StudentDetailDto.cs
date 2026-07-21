namespace TmsApi.Application.DTOs;

    public record StudentDetailDto
    {
        public int Id { get; init; }
        public required string RegistrationNumber { get; init; }
        public required string Name { get; init; }
        public decimal GPA { get; init; }   
        public bool IsActive { get; init; }
        public uint Version { get; init; }
        public required IReadOnlyList<LinkDto> Links { get; init; }
    }