using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalRecords.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicalEntities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CodeSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: false),
                    TextOffsetStart = table.Column<int>(type: "integer", nullable: true),
                    TextOffsetEnd = table.Column<int>(type: "integer", nullable: true),
                    AttributesJson = table.Column<string>(type: "text", nullable: false),
                    ClinicalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalEntities_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClinicalEntities_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClinicalEntities_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntities_ClinicalDate",
                table: "ClinicalEntities",
                column: "ClinicalDate");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntities_Code",
                table: "ClinicalEntities",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntities_DocumentId",
                table: "ClinicalEntities",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntities_EncounterId",
                table: "ClinicalEntities",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntities_EntityType",
                table: "ClinicalEntities",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntities_IsVerified",
                table: "ClinicalEntities",
                column: "IsVerified");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEntities_PatientId",
                table: "ClinicalEntities",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicalEntities");
        }
    }
}
