using HospitalRecords.Application.DTOs;

namespace HospitalRecords.Application.Common.Interfaces;

public interface IRagChatService
{
    Task<ClinicalRagResponseDto> AskClinicalAssistantAsync(ClinicalRagQueryRequest request, CancellationToken cancellationToken = default);
    Task<List<SuggestedQuestionDto>> GetSuggestedQuestionsAsync(Guid patientId, CancellationToken cancellationToken = default);
}
