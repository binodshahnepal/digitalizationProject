using FluentAssertions;
using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Application.Services;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using HospitalRecords.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace HospitalRecords.Tests;

public class AuthServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IJwtTokenGenerator> _jwtMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _jwtMock = new Mock<IJwtTokenGenerator>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _auditServiceMock = new Mock<IAuditService>();

        _authService = new AuthService(_context, _jwtMock.Object, _passwordHasherMock.Object, _auditServiceMock.Object);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnTokenAndRoles()
    {
        // Arrange
        var role = new Role { RoleType = UserRoleType.TreatingClinician, Name = "TreatingClinician" };
        var user = new User
        {
            Username = "dr_koirala",
            Email = "koirala@bpkmch.org.np",
            FullName = "Dr. Sunita Koirala",
            PasswordHash = "hashed_secret",
            Department = "Medical Oncology",
            IsActive = true
        };
        user.UserRoles.Add(new UserRole { User = user, Role = role });

        _context.Roles.Add(role);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _passwordHasherMock.Setup(p => p.VerifyPassword("Doctor@123", "hashed_secret")).Returns(true);
        _jwtMock.Setup(j => j.GenerateToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>())).Returns("mock.jwt.token");

        // Act
        var result = await _authService.LoginAsync(new LoginRequest("dr_koirala", "Doctor@123"));

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be("mock.jwt.token");
        result.Username.Should().Be("dr_koirala");
        result.Roles.Should().Contain("TreatingClinician");

        _auditServiceMock.Verify(a => a.LogAsync(
            "AUTH_LOGIN_SUCCESS",
            "User",
            user.Id.ToString(),
            AuditOutcome.Success,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldFailAndLogAudit()
    {
        // Arrange
        var user = new User
        {
            Username = "clerk_anil",
            Email = "anil@bpkmch.org.np",
            PasswordHash = "hashed_secret",
            IsActive = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _passwordHasherMock.Setup(p => p.VerifyPassword("WrongPassword", "hashed_secret")).Returns(false);

        // Act
        var result = await _authService.LoginAsync(new LoginRequest("clerk_anil", "WrongPassword"));

        // Assert
        result.Should().BeNull();

        _auditServiceMock.Verify(a => a.LogAsync(
            "AUTH_LOGIN_FAILED",
            "User",
            user.Id.ToString(),
            AuditOutcome.Denied,
            It.Is<string>(d => d.Contains("Incorrect password")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
