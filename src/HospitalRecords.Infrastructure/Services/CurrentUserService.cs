using System.Security.Claims;
using HospitalRecords.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HospitalRecords.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Username => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name) 
        ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name 
        ?? "Anonymous";

    public string? Role => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    public string? RequestId => _httpContextAccessor.HttpContext?.TraceIdentifier;
}
