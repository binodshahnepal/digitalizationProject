using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalRecords.Application.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditService _auditService;

    public AuthService(
        IApplicationDbContext context,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher,
        IAuditService auditService)
    {
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _auditService = auditService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = request.Username.Trim().ToLower();

        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername, cancellationToken);

        if (user == null || !user.IsActive)
        {
            await _auditService.LogAsync(
                action: "AUTH_LOGIN_FAILED",
                resource: "User",
                resourceId: normalizedUsername,
                outcome: AuditOutcome.Denied,
                details: "Login failed: Invalid credentials or inactive account.",
                cancellationToken: cancellationToken);
            return null;
        }

        // Verify password using password hasher
        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            await _auditService.LogAsync(
                action: "AUTH_LOGIN_FAILED",
                resource: "User",
                resourceId: user.Id.ToString(),
                outcome: AuditOutcome.Denied,
                details: "Login failed: Incorrect password.",
                cancellationToken: cancellationToken);
            return null;
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var token = _jwtTokenGenerator.GenerateToken(user, roles);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8); // Standard shift length

        await _auditService.LogAsync(
            action: "AUTH_LOGIN_SUCCESS",
            resource: "User",
            resourceId: user.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Successful login by {user.Username} ({string.Join(", ", roles)})",
            cancellationToken: cancellationToken);

        return new LoginResponse(
            Token: token,
            UserId: user.Id,
            Username: user.Username,
            FullName: user.FullName,
            Department: user.Department,
            Roles: roles,
            ExpiresAt: expiresAt
        );
    }
}
