using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HospitalRecords.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<ClinicalEvent> ClinicalEvents => Set<ClinicalEvent>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentOcrResult> DocumentOcrResults => Set<DocumentOcrResult>();
    public DbSet<ClinicalEntity> ClinicalEntities => Set<ClinicalEntity>();
    public DbSet<DocumentEmbedding> DocumentEmbeddings => Set<DocumentEmbedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PostgreSQL Vector extension (Will be enabled in Sprint 5 with pgvector Docker service)
        // modelBuilder.HasPostgresExtension("vector");

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Username).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        });

        // Role Configuration
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Name).IsUnique();
            entity.Property(r => r.Name).HasMaxLength(100).IsRequired();
        });

        // UserRole Many-to-Many
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AccessGrant Configuration
        modelBuilder.Entity<AccessGrant>(entity =>
        {
            entity.HasKey(ag => ag.Id);
            entity.HasIndex(ag => new { ag.UserId, ag.PatientId });

            entity.HasOne(ag => ag.User)
                .WithMany(u => u.AccessGrants)
                .HasForeignKey(ag => ag.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Patient Configuration (FR-PAT-001, FR-PAT-002)
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(p => p.Id);

            // Mandatory Unique Index on Medical Record Number (MRN)
            entity.HasIndex(p => p.Mrn).IsUnique();

            // Search and Duplicate detection indexes
            entity.HasIndex(p => p.NationalId);
            entity.HasIndex(p => p.PhoneNumber);
            entity.HasIndex(p => p.DateOfBirth);
            entity.HasIndex(p => new { p.FirstName, p.LastName, p.DateOfBirth });

            entity.Property(p => p.Mrn).HasMaxLength(50).IsRequired();
            entity.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(p => p.MiddleName).HasMaxLength(100);
            entity.Property(p => p.LastName).HasMaxLength(100).IsRequired();
            entity.Property(p => p.PhoneNumber).HasMaxLength(25);
            entity.Property(p => p.NationalId).HasMaxLength(50);
            entity.Property(p => p.District).HasMaxLength(100);
            entity.Property(p => p.Municipality).HasMaxLength(100);
            entity.Property(p => p.IdentityProvenance).HasMaxLength(100).IsRequired();
        });

        // AuditEvent Configuration (Append-only, indexed for fast audit queries)
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.Timestamp);
            entity.HasIndex(a => a.ActorId);
            entity.HasIndex(a => new { a.Resource, a.ResourceId });
            entity.Property(a => a.ActorUsername).HasMaxLength(100).IsRequired();
            entity.Property(a => a.Action).HasMaxLength(100).IsRequired();
            entity.Property(a => a.Resource).HasMaxLength(100).IsRequired();
            entity.Property(a => a.ResourceId).HasMaxLength(200);
        });

        // Encounter Configuration (FR-VIS-001)
        modelBuilder.Entity<Encounter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EncounterNumber).IsUnique();
            entity.HasIndex(e => e.PatientId);
            entity.HasIndex(e => e.StartDate);

            entity.Property(e => e.EncounterNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Facility).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Department).HasMaxLength(150).IsRequired();

            entity.HasOne(e => e.Patient)
                .WithMany()
                .HasForeignKey(e => e.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ClinicalEvent Configuration (FR-VIS-002)
        modelBuilder.Entity<ClinicalEvent>(entity =>
        {
            entity.HasKey(ce => ce.Id);
            entity.HasIndex(ce => ce.EncounterId);
            entity.HasIndex(ce => ce.EventTime);

            entity.Property(ce => ce.EventType).HasMaxLength(100).IsRequired();
            entity.Property(ce => ce.Author).HasMaxLength(100).IsRequired();

            entity.HasOne(ce => ce.Encounter)
                .WithMany(e => e.ClinicalEvents)
                .HasForeignKey(ce => ce.EncounterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Document Configuration (FR-DOC-001, FR-DOC-003, FR-DOC-004)
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.HasIndex(d => d.PatientId);
            entity.HasIndex(d => d.EncounterId);
            entity.HasIndex(d => d.ClinicalDate);
            entity.HasIndex(d => d.Status);

            entity.Property(d => d.Language).HasMaxLength(20).IsRequired();

            entity.HasOne(d => d.Patient)
                .WithMany()
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Encounter)
                .WithMany(e => e.Documents)
                .HasForeignKey(d => d.EncounterId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.ClinicalEvent)
                .WithMany(ce => ce.Documents)
                .HasForeignKey(d => d.ClinicalEventId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // DocumentVersion Configuration (FR-DOC-004)
        modelBuilder.Entity<DocumentVersion>(entity =>
        {
            entity.HasKey(dv => dv.Id);
            entity.HasIndex(dv => new { dv.DocumentId, dv.VersionNumber }).IsUnique();
            entity.HasIndex(dv => dv.Sha256Checksum);

            entity.Property(dv => dv.Sha256Checksum).HasMaxLength(64).IsRequired();
            entity.Property(dv => dv.StorageKey).HasMaxLength(500).IsRequired();
            entity.Property(dv => dv.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(dv => dv.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(dv => dv.ReasonForVersion).HasMaxLength(500).IsRequired();

            entity.HasOne(dv => dv.Document)
                .WithMany(d => d.Versions)
                .HasForeignKey(dv => dv.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DocumentOcrResult Configuration (FR-OCR-001, FR-OCR-002, FR-CLS-001)
        modelBuilder.Entity<DocumentOcrResult>(entity =>
        {
            entity.HasKey(ocr => ocr.Id);
            entity.HasIndex(ocr => ocr.DocumentId);
            entity.HasIndex(ocr => ocr.DocumentVersionId);
            entity.HasIndex(ocr => ocr.OcrConfidenceScore);
            entity.HasIndex(ocr => ocr.LowConfidenceWarning);
            entity.HasIndex(ocr => ocr.PredictedCategory);

            entity.Property(ocr => ocr.LanguageDetected).HasMaxLength(20).IsRequired();
            entity.Property(ocr => ocr.ExtractedText).IsRequired();
            entity.Property(ocr => ocr.KeywordsDetected).HasMaxLength(2000);
            entity.Property(ocr => ocr.VerifiedBy).HasMaxLength(100);

            entity.HasOne(ocr => ocr.Document)
                .WithMany(d => d.OcrResults)
                .HasForeignKey(ocr => ocr.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ocr => ocr.DocumentVersion)
                .WithMany()
                .HasForeignKey(ocr => ocr.DocumentVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ClinicalEntity Configuration (FR-EXT-001, FR-EXT-002, FR-VIS-001)
        modelBuilder.Entity<ClinicalEntity>(entity =>
        {
            entity.HasKey(ce => ce.Id);
            entity.HasIndex(ce => ce.PatientId);
            entity.HasIndex(ce => ce.DocumentId);
            entity.HasIndex(ce => ce.EncounterId);
            entity.HasIndex(ce => ce.EntityType);
            entity.HasIndex(ce => ce.Code);
            entity.HasIndex(ce => ce.IsVerified);
            entity.HasIndex(ce => ce.ClinicalDate);

            entity.Property(ce => ce.Category).HasMaxLength(100).IsRequired();
            entity.Property(ce => ce.Name).HasMaxLength(255).IsRequired();
            entity.Property(ce => ce.Value).HasMaxLength(500).IsRequired();
            entity.Property(ce => ce.Code).HasMaxLength(50);
            entity.Property(ce => ce.CodeSystem).HasMaxLength(50);
            entity.Property(ce => ce.VerifiedBy).HasMaxLength(100);

            entity.HasOne(ce => ce.Patient)
                .WithMany()
                .HasForeignKey(ce => ce.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ce => ce.Document)
                .WithMany(d => d.ClinicalEntities)
                .HasForeignKey(ce => ce.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ce => ce.Encounter)
                .WithMany()
                .HasForeignKey(ce => ce.EncounterId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // DocumentEmbedding Configuration (FR-SCH-002)
        modelBuilder.Entity<DocumentEmbedding>(entity =>
        {
            entity.HasKey(de => de.Id);
            entity.HasIndex(de => de.DocumentId);
            entity.HasIndex(de => de.PatientId);
            entity.HasIndex(de => de.ChunkIndex);

            entity.Property(de => de.ChunkText).IsRequired();
            entity.Property(de => de.SectionHeader).HasMaxLength(255);

            entity.HasOne(de => de.Document)
                .WithMany(d => d.Embeddings)
                .HasForeignKey(de => de.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(de => de.Patient)
                .WithMany()
                .HasForeignKey(de => de.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
