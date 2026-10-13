using HospitalRecords.Domain.Common;

namespace HospitalRecords.Domain.Entities;

public class DocumentEmbedding : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public int ChunkIndex { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public string? SectionHeader { get; set; }

    /// <summary>
    /// Dense normalized clinical semantic vector representation (384 dimensions)
    /// </summary>
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public int Dimensions { get; set; } = 384;
}
