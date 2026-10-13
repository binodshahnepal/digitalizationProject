using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalRecords.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentOcrResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "DocumentOcrResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: false),
                    ExtractedNepaliText = table.Column<string>(type: "text", nullable: true),
                    OcrConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    LanguageDetected = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    WordCount = table.Column<int>(type: "integer", nullable: false),
                    PredictedCategory = table.Column<int>(type: "integer", nullable: false),
                    ClassificationConfidence = table.Column<double>(type: "double precision", nullable: false),
                    KeywordsDetected = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LowConfidenceWarning = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerifiedByHuman = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessingDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOcrResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentOcrResults_DocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentOcrResults_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOcrResults_DocumentId",
                table: "DocumentOcrResults",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOcrResults_DocumentVersionId",
                table: "DocumentOcrResults",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOcrResults_LowConfidenceWarning",
                table: "DocumentOcrResults",
                column: "LowConfidenceWarning");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOcrResults_OcrConfidenceScore",
                table: "DocumentOcrResults",
                column: "OcrConfidenceScore");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOcrResults_PredictedCategory",
                table: "DocumentOcrResults",
                column: "PredictedCategory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentOcrResults");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
