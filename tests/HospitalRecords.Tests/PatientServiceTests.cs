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

public class PatientServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly PatientService _patientService;

    public PatientServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _auditServiceMock = new Mock<IAuditService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        _currentUserServiceMock.Setup(s => s.Username).Returns("clerk_ramesh");
        _currentUserServiceMock.Setup(s => s.UserId).Returns(Guid.NewGuid().ToString());
        _currentUserServiceMock.Setup(s => s.Role).Returns("RegistrationClerk");

        _patientService = new PatientService(_context, _auditServiceMock.Object, _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task CreatePatient_WithUniqueMrn_ShouldSucceedAndAssignPersistentId()
    {
        // Arrange
        var request = new CreatePatientRequest(
            Mrn: "BPKMCH-2026-0001",
            NationalId: "123-456-789",
            FirstName: "Sita",
            MiddleName: null,
            LastName: "Thapa",
            DateOfBirth: new DateOnly(1985, 5, 12),
            Gender: Gender.Female,
            PhoneNumber: "9841234567",
            Email: "sita.thapa@example.com",
            District: "Chitwan",
            Municipality: "Bharatpur",
            Ward: "10",
            AddressLine: "Hospital Chowk",
            EmergencyContactName: "Ram Thapa",
            EmergencyContactPhone: "9841234568",
            EmergencyContactRelation: "Spouse",
            IdentityProvenance: "Citizenship Card"
        );

        // Act
        var result = await _patientService.CreatePatientAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Patient.Should().NotBeNull();
        result.Patient!.Id.Should().NotBeEmpty();
        result.Patient.Mrn.Should().Be("BPKMCH-2026-0001");
        result.Patient.FullName.Should().Be("Sita Thapa");
        result.Patient.IdentityProvenance.Should().Be("Citizenship Card");

        // Verify Audit Event logged (NFR-004)
        _auditServiceMock.Verify(a => a.LogAsync(
            "PATIENT_CREATED",
            "Patient",
            result.Patient.Id.ToString(),
            AuditOutcome.Success,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AT01_CreatePatient_WithExistingMrn_ShouldRejectAndEnforceReuse()
    {
        // Acceptance Scenario AT-01:
        // Given existing MRN, when clerk registers repeat attendance, duplicate creation is rejected
        
        // Arrange: Existing patient already registered
        var existingPatient = new Patient
        {
            Mrn = "BPKMCH-2026-0012",
            FirstName = "Hari",
            LastName = "Bahadur",
            DateOfBirth = new DateOnly(1975, 8, 20),
            Gender = Gender.Male,
            PhoneNumber = "9851000111",
            IdentityProvenance = "OPD Desk"
        };
        _context.Patients.Add(existingPatient);
        await _context.SaveChangesAsync();

        // Act: Attempt to register with the same MRN
        var repeatAttendanceRequest = new CreatePatientRequest(
            Mrn: "BPKMCH-2026-0012",
            NationalId: null,
            FirstName: "Hari",
            MiddleName: null,
            LastName: "Bahadur",
            DateOfBirth: new DateOnly(1975, 8, 20),
            Gender: Gender.Male,
            PhoneNumber: "9851000111",
            Email: null,
            District: "Chitwan",
            Municipality: "Bharatpur",
            Ward: "1",
            AddressLine: null,
            EmergencyContactName: null,
            EmergencyContactPhone: null,
            EmergencyContactRelation: null,
            IdentityProvenance: "OPD Desk"
        );

        var result = await _patientService.CreatePatientAsync(repeatAttendanceRequest);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already exists");
        result.ErrorMessage.Should().Contain("AT-01 / FR-PAT-001");

        // Ensure no second patient with this MRN exists in DB
        var totalWithMrn = await _context.Patients.CountAsync(p => p.Mrn == "BPKMCH-2026-0012");
        totalWithMrn.Should().Be(1);

        // Verify audit logged the rejected attempt
        _auditServiceMock.Verify(a => a.LogAsync(
            "PATIENT_CREATION_FAILED",
            "Patient",
            "BPKMCH-2026-0012",
            AuditOutcome.Failed,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DuplicateDetection_MatchingNameAndDOB_ShouldFlagWarningRatherThanSilentMerge()
    {
        // Requirement FR-PAT-002:
        // Search patients by MRN, name, DOB; show possible duplicates rather than silently merging.

        // Arrange: Seed an existing patient
        var existing = new Patient
        {
            Mrn = "BPKMCH-2026-0044",
            FirstName = "Geeta",
            LastName = "Adhikari",
            DateOfBirth = new DateOnly(1990, 3, 15),
            Gender = Gender.Female,
            PhoneNumber = "9800000001"
        };
        _context.Patients.Add(existing);
        await _context.SaveChangesAsync();

        // Act: Attempt to register a patient with a new MRN but matching Name and DOB
        var request = new CreatePatientRequest(
            Mrn: "BPKMCH-2026-0099",
            NationalId: null,
            FirstName: "Geeta",
            MiddleName: null,
            LastName: "Adhikari",
            DateOfBirth: new DateOnly(1990, 3, 15),
            Gender: Gender.Female,
            PhoneNumber: "9800000002",
            Email: null,
            District: "Kaski",
            Municipality: "Pokhara",
            Ward: "5",
            AddressLine: null,
            EmergencyContactName: null,
            EmergencyContactPhone: null,
            EmergencyContactRelation: null,
            IdentityProvenance: "OPD Desk",
            ForceCreateIfDuplicate: false
        );

        var result = await _patientService.CreatePatientAsync(request);

        // Assert: Creation blocked with duplicate candidates returned for clerk review
        result.Success.Should().BeFalse();
        result.PotentialDuplicates.Should().NotBeEmpty();
        result.PotentialDuplicates.First().ExistingPatientId.Should().Be(existing.Id);
        result.PotentialDuplicates.First().Mrn.Should().Be("BPKMCH-2026-0044");
        result.PotentialDuplicates.First().MatchScore.Should().BeGreaterThan(0.8);
        result.PotentialDuplicates.First().MatchReason.Should().Contain("Date of Birth");
    }

    [Fact]
    public async Task UpdatePatient_ShouldMaintainProvenanceAndLogAudit()
    {
        // Arrange
        var patient = new Patient
        {
            Mrn = "BPKMCH-2026-0050",
            FirstName = "Binod",
            LastName = "Chaudhary",
            DateOfBirth = new DateOnly(1982, 1, 1),
            Gender = Gender.Male,
            PhoneNumber = "9841111111",
            IdentityProvenance = "OPD Desk",
            CreatedBy = "clerk_1"
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        var updateRequest = new UpdatePatientRequest(
            NationalId: "CITIZEN-998877",
            FirstName: "Binod",
            MiddleName: "Kumar",
            LastName: "Chaudhary",
            DateOfBirth: new DateOnly(1982, 1, 1),
            Gender: Gender.Male,
            PhoneNumber: "9842222222",
            Email: "binod@example.com",
            District: "Chitwan",
            Municipality: "Bharatpur",
            Ward: "12",
            AddressLine: "Narayangarh",
            EmergencyContactName: "Rita Chaudhary",
            EmergencyContactPhone: "9843333333",
            EmergencyContactRelation: "Wife",
            UpdateReason: "Patient provided official citizenship certificate and updated contact number"
        );

        // Act
        var updated = await _patientService.UpdateAsync(patient.Id, updateRequest);

        // Assert
        updated.Should().NotBeNull();
        updated!.MiddleName.Should().Be("Kumar");
        updated.NationalId.Should().Be("CITIZEN-998877");
        updated.PhoneNumber.Should().Be("9842222222");

        _auditServiceMock.Verify(a => a.LogAsync(
            "PATIENT_UPDATED",
            "Patient",
            patient.Id.ToString(),
            AuditOutcome.Success,
            It.Is<string>(d => d.Contains("citizenship certificate")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
