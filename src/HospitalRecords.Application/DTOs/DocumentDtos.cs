using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Application.DTOs;

public record UploadDocumentFileDto(
    Stream ContentStream,
    string FileName,
    long FileSize
);

public record UploadDocumentsRequest(
    Guid? EncounterId,
    Guid? ClinicalEventId,
    DocumentCategory Category,
    DateOnly ClinicalDate,
    string Language,
    ConfidentialityLevel ConfidentialityLevel,
    List<UploadDocumentFileDto> Files
);

public record DocumentVersionDto(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    string Sha256Checksum,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    int? PageCount,
    string ReasonForVersion,
    DateTimeOffset CreatedAt,
    string CreationActor
);

public record DocumentDto(
    Guid Id,
    Guid PatientId,
    Guid? EncounterId,
    Guid? ClinicalEventId,
    DocumentCategory Category,
    DateOnly ClinicalDate,
    string Language,
    ConfidentialityLevel ConfidentialityLevel,
    DocumentStatus Status,
    int CurrentVersionNumber,
    DocumentVersionDto? CurrentVersion,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DocumentOcrResultDto? LatestOcr = null
);

public record DocumentUploadResult(
    bool Success,
    List<DocumentDto> CreatedDocuments,
    List<string> Errors
);

public record AmendDocumentRequest(
    Stream ContentStream,
    string FileName,
    long FileSize,
    string ReasonForAmendment
);
