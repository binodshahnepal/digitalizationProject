using HospitalRecords.Application.DTOs;

namespace HospitalRecords.Application.Common.Interfaces;

public interface IClinicalSafetyBenchmarkService
{
    Task<ClinicalFeedbackDto> SubmitFeedbackAsync(SubmitClinicalFeedbackRequest request, CancellationToken cancellationToken = default);
    Task<List<ClinicalFeedbackDto>> GetFeedbackListAsync(Guid? patientId, CancellationToken cancellationToken = default);
    Task<ClinicalFeedbackDto> UpdateFeedbackStatusAsync(Guid feedbackId, UpdateFeedbackStatusRequest request, CancellationToken cancellationToken = default);
    Task<ClinicalSafetyBenchmarkReportDto> RunSafetyBenchmarkAsync(CancellationToken cancellationToken = default);
    Task<DisasterRecoveryDrillResultDto> RehearseDisasterRecoveryDrillAsync(CancellationToken cancellationToken = default);
}
