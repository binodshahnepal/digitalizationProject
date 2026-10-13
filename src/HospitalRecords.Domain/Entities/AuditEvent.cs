using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Domain.Entities;

public class AuditEvent
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string ActorId { get; set; } = string.Empty;
    public string ActorUsername { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public AuditOutcome Outcome { get; set; } = AuditOutcome.Success;
    public string? IpAddress { get; set; }
    public string? RequestId { get; set; }
    public string? Details { get; set; }
}
