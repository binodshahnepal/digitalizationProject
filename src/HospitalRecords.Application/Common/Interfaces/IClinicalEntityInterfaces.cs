using HospitalRecords.Application.DTOs;

namespace HospitalRecords.Application.Common.Interfaces;

public interface IClinicalEntityExtractionService
{
    /// <summary>
    /// Extracts structured oncology entities from document text via AI Worker and persists them.
    /// </summary>
    Task<List<ClinicalEntityDto>> ExtractAndStoreEntitiesAsync(Guid documentId, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all extracted clinical entities associated with a specific document.
    /// </summary>
    Task<List<ClinicalEntityDto>> GetEntitiesByDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all extracted clinical entities associated with a specific patient across all records.
    /// </summary>
    Task<List<ClinicalEntityDto>> GetEntitiesByPatientAsync(Guid patientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and confirms an extracted entity with clinician review details (FR-EXT-002).
    /// </summary>
    Task<ClinicalEntityDto> VerifyEntityAsync(Guid entityId, VerifyClinicalEntityRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a clinician manual clinical entity.
    /// </summary>
    Task<ClinicalEntityDto> CreateManualEntityAsync(CreateClinicalEntityRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing clinical entity.
    /// </summary>
    Task<ClinicalEntityDto> UpdateEntityAsync(Guid entityId, UpdateClinicalEntityRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a clinical entity.
    /// </summary>
    Task<bool> DeleteEntityAsync(Guid entityId, CancellationToken cancellationToken = default);
}

public interface ITimelineService
{
    /// <summary>
    /// Synthesizes patient encounters, clinical events, digitized documents, and extracted oncology entities into a longitudinal chronological timeline (FR-VIS-001, FR-VIS-002).
    /// </summary>
    Task<PatientTimelineDto> GetPatientTimelineAsync(Guid patientId, CancellationToken cancellationToken = default);
}
