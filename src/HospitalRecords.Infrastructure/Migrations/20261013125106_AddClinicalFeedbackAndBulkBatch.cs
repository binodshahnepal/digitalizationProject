using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalRecords.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalFeedbackAndBulkBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BulkBatchJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    TotalPagesScanned = table.Column<int>(type: "integer", nullable: false),
                    TotalDocumentsExtracted = table.Column<int>(type: "integer", nullable: false),
                    MatchedPatientsCount = table.Column<int>(type: "integer", nullable: false),
                    UnassignedDocumentsCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InitiatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkBatchJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalAiFeedbacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClinicianUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    QueryText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AssistantResponseText = table.Column<string>(type: "text", nullable: false),
                    FeedbackType = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClinicianNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SupervisorResolutionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalAiFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalAiFeedbacks_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BulkBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BulkBatchJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemIndex = table.Column<int>(type: "integer", nullable: false),
                    PageStart = table.Column<int>(type: "integer", nullable: false),
                    PageEnd = table.Column<int>(type: "integer", nullable: false),
                    DetectedBarcodeMrn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MatchedPatientId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BulkBatchItems_BulkBatchJobs_BulkBatchJobId",
                        column: x => x.BulkBatchJobId,
                        principalTable: "BulkBatchJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BulkBatchItems_Documents_GeneratedDocumentId",
                        column: x => x.GeneratedDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BulkBatchItems_Patients_MatchedPatientId",
                        column: x => x.MatchedPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BulkBatchItems_BulkBatchJobId",
                table: "BulkBatchItems",
                column: "BulkBatchJobId");

            migrationBuilder.CreateIndex(
                name: "IX_BulkBatchItems_DetectedBarcodeMrn",
                table: "BulkBatchItems",
                column: "DetectedBarcodeMrn");

            migrationBuilder.CreateIndex(
                name: "IX_BulkBatchItems_GeneratedDocumentId",
                table: "BulkBatchItems",
                column: "GeneratedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BulkBatchItems_MatchedPatientId",
                table: "BulkBatchItems",
                column: "MatchedPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_BulkBatchItems_Status",
                table: "BulkBatchItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BulkBatchJobs_BatchReference",
                table: "BulkBatchJobs",
                column: "BatchReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BulkBatchJobs_CreatedAt",
                table: "BulkBatchJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BulkBatchJobs_Status",
                table: "BulkBatchJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAiFeedbacks_CreatedAt",
                table: "ClinicalAiFeedbacks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAiFeedbacks_FeedbackType",
                table: "ClinicalAiFeedbacks",
                column: "FeedbackType");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAiFeedbacks_PatientId",
                table: "ClinicalAiFeedbacks",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAiFeedbacks_Status",
                table: "ClinicalAiFeedbacks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulkBatchItems");

            migrationBuilder.DropTable(
                name: "ClinicalAiFeedbacks");

            migrationBuilder.DropTable(
                name: "BulkBatchJobs");
        }
    }
}
