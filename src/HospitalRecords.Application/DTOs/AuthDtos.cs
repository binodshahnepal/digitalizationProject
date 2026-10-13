namespace HospitalRecords.Application.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string Token,
    Guid UserId,
    string Username,
    string FullName,
    string Department,
    List<string> Roles,
    DateTimeOffset ExpiresAt
);

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string Department,
    bool IsActive,
    List<string> Roles
);
