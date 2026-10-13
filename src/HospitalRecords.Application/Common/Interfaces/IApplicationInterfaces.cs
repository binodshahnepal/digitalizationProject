using HospitalRecords.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HospitalRecords.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AccessGrant> AccessGrants { get; }
    DbSet<Patient> Patients { get; }
    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<Encounter> Encounters { get; }
    DbSet<ClinicalEvent> ClinicalEvents { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentVersion> DocumentVersions { get; }
    DbSet<DocumentOcrResult> DocumentOcrResults { get; }
    DbSet<ClinicalEntity> ClinicalEntities { get; }
    DbSet<DocumentEmbedding> DocumentEmbeddings { get; }
    DbSet<ClinicalAiFeedback> ClinicalAiFeedbacks { get; }
    DbSet<BulkBatchJob> BulkBatchJobs { get; }
    DbSet<BulkBatchItem> BulkBatchItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public record FileValidationResult(
    bool IsValid,
    string? DetectedContentType,
    string? Sha256Checksum,
    string? ErrorMessage
);

public interface IFileSecurityValidator
{
    FileValidationResult Validate(Stream stream, string fileName, long fileSize);
}

public interface IObjectStorageService
{
    Task<string> UploadAsync(Stream stream, string storageKey, string contentType, CancellationToken cancellationToken = default);
    Task<Stream?> DownloadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
}

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Username { get; }
    string? Role { get; }
    string? IpAddress { get; }
    string? RequestId { get; }
}

public interface IAuditService
{
    Task LogAsync(
        string action, 
        string resource, 
        string? resourceId = null, 
        Domain.Enums.AuditOutcome outcome = Domain.Enums.AuditOutcome.Success, 
        string? details = null,
        CancellationToken cancellationToken = default);
}

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, IEnumerable<string> roles);
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
