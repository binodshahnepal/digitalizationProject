using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HospitalRecords.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IApplicationDbContext context, 
        ICurrentUserService currentUserService,
        ILogger<AuditService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task LogAsync(
        string action, 
        string resource, 
        string? resourceId = null, 
        AuditOutcome outcome = AuditOutcome.Success, 
        string? details = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditEvent = new AuditEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                ActorId = _currentUserService.UserId ?? "Anonymous",
                ActorUsername = _currentUserService.Username ?? "Anonymous",
                ActorRole = _currentUserService.Role ?? "None",
                Action = action,
                Resource = resource,
                ResourceId = resourceId,
                Outcome = outcome,
                IpAddress = _currentUserService.IpAddress,
                RequestId = _currentUserService.RequestId,
                Details = details
            };

            _context.AuditEvents.Add(auditEvent);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("AUDIT [{Outcome}]: {Action} on {Resource}/{ResourceId} by {User}", 
                outcome, action, resource, resourceId ?? "N/A", auditEvent.ActorUsername);
        }
        catch (Exception ex)
        {
            // Fallback: Ensure audit failure is logged to system log without killing primary request
            _logger.LogError(ex, "Failed to persist audit event for action {Action} on {Resource}", action, resource);
        }
    }
}
