using HospitalRecords.Application.DTOs;

namespace HospitalRecords.Application.Common.Interfaces;

public interface ISearchService
{
    Task<HybridSearchResultDto> SearchAsync(SearchQueryRequest request, CancellationToken cancellationToken = default);
    Task<int> IndexDocumentEmbeddingsAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<int> ReindexAllDocumentsAsync(CancellationToken cancellationToken = default);
}
