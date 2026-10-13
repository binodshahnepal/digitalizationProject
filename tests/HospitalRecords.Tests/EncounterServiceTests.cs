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

public class EncounterServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly EncounterService _encounterService;

    public EncounterServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _auditServiceMock = new Mock<IAuditService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(s => s.Username).Returns("doctor_sunita");

        _encounterService = new EncounterService(_context, _auditServiceMock.Object, _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task CreateEncounter_ShouldGenerateSequentialEncounterNumber_AndLinkToPatient()
    {
        // Requirement FR-VIS-001:
        // Create zero-to-many encounters for a patient with unique encounter number
        
        // Arrange
        var patient = new Patient
        {
            Mrn = "BPKMCH-2026-0777",
            FirstName = "Nabin",
            LastName = "Pandey",
            DateOfBirth = new DateOnly(1972, 9, 14)
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        var request = new CreateEncounterRequest(
            PatientId: patient.Id,
            Type: EncounterType.Inpatient,
            Facility: "BPKMCH Main Campus",
            Department: "Surgical Oncology",
            StartDate: DateTimeOffset.UtcNow,
            AttendingClinicianName: "Dr. Koirala",
            ReasonForVisit: "Scheduled resection surgery"
        );

        // Act
        var encounter = await _encounterService.CreateEncounterAsync(request);

        // Assert
        encounter.Should().NotBeNull();
        encounter.PatientId.Should().Be(patient.Id);
        encounter.EncounterNumber.Should().StartWith($"ENC-{DateTime.UtcNow.Year}-");
        encounter.Status.Should().Be(EncounterStatus.Active);
        encounter.Department.Should().Be("Surgical Oncology");

        _auditServiceMock.Verify(a => a.LogAsync(
            "ENCOUNTER_CREATED",
            "Encounter",
            encounter.Id.ToString(),
            AuditOutcome.Success,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateClinicalEvent_ShouldAttachToEncounter_AndRecordAuthor()
    {
        // Requirement FR-VIS-002:
        // Allow one admission to contain multiple clinical events
        
        // Arrange
        var patient = new Patient { Mrn = "BPKMCH-2026-0778", FirstName = "Dilip", LastName = "Rai" };
        var encounter = new Encounter
        {
            PatientId = patient.Id,
            EncounterNumber = "ENC-2026-00099",
            Type = EncounterType.Inpatient
        };
        _context.Patients.Add(patient);
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync();

        var request = new CreateClinicalEventRequest(
            EncounterId: encounter.Id,
            EventType: "Post-Op Wound Check",
            EventTime: DateTimeOffset.UtcNow,
            Author: "Dr. Sunita Koirala",
            Department: "Surgical Oncology",
            Description: "Wound clean, surgical staples intact, no sign of infection."
        );

        // Act
        var created = await _encounterService.CreateClinicalEventAsync(request);

        // Assert
        created.Should().NotBeNull();
        created.EncounterId.Should().Be(encounter.Id);
        created.EventType.Should().Be("Post-Op Wound Check");
        created.Author.Should().Be("Dr. Sunita Koirala");

        var eventsInDb = await _context.ClinicalEvents.Where(ce => ce.EncounterId == encounter.Id).ToListAsync();
        eventsInDb.Should().HaveCount(1);
    }
}
