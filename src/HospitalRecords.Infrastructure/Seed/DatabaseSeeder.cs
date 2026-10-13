using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using HospitalRecords.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HospitalRecords.Infrastructure.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, ILogger logger)
    {
        try
        {
            if (await context.Roles.AnyAsync())
            {
                return; // Already seeded
            }

            logger.LogInformation("Seeding default hospital roles and pilot accounts...");

            // 1. Seed Roles based on SRS Section 2.1
            var roles = new List<Role>
            {
                new() { RoleType = UserRoleType.RegistrationClerk, Name = "RegistrationClerk", Description = "Create/find patient; create encounter; upload/index documents" },
                new() { RoleType = UserRoleType.TreatingClinician, Name = "TreatingClinician", Description = "View authorized patient timeline, records, cited AI summaries" },
                new() { RoleType = UserRoleType.Nurse, Name = "Nurse", Description = "Access assigned encounters and approved documents" },
                new() { RoleType = UserRoleType.MedicalRecordsSupervisor, Name = "MedicalRecordsSupervisor", Description = "Reconcile duplicates, correct metadata, approve indexing" },
                new() { RoleType = UserRoleType.HospitalAdmin, Name = "HospitalAdmin", Description = "Aggregate operational reporting; configuration" },
                new() { RoleType = UserRoleType.ITAdmin, Name = "ITAdmin", Description = "Infrastructure and accounts; no default access to clinical content" },
                new() { RoleType = UserRoleType.ClinicalAiReviewer, Name = "ClinicalAiReviewer", Description = "Validate extraction and AI answer quality using approved datasets" },
                new() { RoleType = UserRoleType.Auditor, Name = "Auditor", Description = "Review access events, incidents, retention" }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();

            // 2. Seed Default Pilot Users (BCrypt hashed password: "Password@123")
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password@123");

            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@bpkmch.org.np",
                FullName = "Hospital Administrator",
                PasswordHash = passwordHash,
                Department = "Administration",
                IsActive = true
            };

            var clerkUser = new User
            {
                Username = "clerk",
                Email = "clerk@bpkmch.org.np",
                FullName = "Ramesh Sharma",
                PasswordHash = passwordHash,
                Department = "Medical Records / Registration",
                IsActive = true
            };

            var doctorUser = new User
            {
                Username = "doctor",
                Email = "doctor@bpkmch.org.np",
                FullName = "Dr. Sunita Koirala",
                PasswordHash = passwordHash,
                Department = "Medical Oncology",
                IsActive = true
            };

            var supervisorUser = new User
            {
                Username = "supervisor",
                Email = "supervisor@bpkmch.org.np",
                FullName = "Anil Shrestha",
                PasswordHash = passwordHash,
                Department = "Medical Records Supervision",
                IsActive = true
            };

            await context.Users.AddRangeAsync(adminUser, clerkUser, doctorUser, supervisorUser);
            await context.SaveChangesAsync();

            // 3. Assign Roles
            var adminRole = roles.First(r => r.Name == "HospitalAdmin");
            var itRole = roles.First(r => r.Name == "ITAdmin");
            var clerkRole = roles.First(r => r.Name == "RegistrationClerk");
            var doctorRole = roles.First(r => r.Name == "TreatingClinician");
            var supervisorRole = roles.First(r => r.Name == "MedicalRecordsSupervisor");

            var userRoles = new List<UserRole>
            {
                new() { UserId = adminUser.Id, RoleId = adminRole.Id },
                new() { UserId = adminUser.Id, RoleId = itRole.Id },
                new() { UserId = clerkUser.Id, RoleId = clerkRole.Id },
                new() { UserId = doctorUser.Id, RoleId = doctorRole.Id },
                new() { UserId = supervisorUser.Id, RoleId = supervisorRole.Id }
            };

            await context.UserRoles.AddRangeAsync(userRoles);
            await context.SaveChangesAsync();

            logger.LogInformation("Database seeded successfully with roles and default pilot accounts.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }
}
