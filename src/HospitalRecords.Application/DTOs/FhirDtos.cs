namespace HospitalRecords.Application.DTOs;

public class FhirIdentifier
{
    public string System { get; set; } = "https://bpkmch.org.np/identifiers/mrn";
    public string Value { get; set; } = string.Empty;
}

public class FhirHumanName
{
    public string Use { get; set; } = "official";
    public string Family { get; set; } = string.Empty;
    public List<string> Given { get; set; } = new();
}

public class FhirContactPoint
{
    public string System { get; set; } = "phone";
    public string Value { get; set; } = string.Empty;
}

public class FhirPatientResource
{
    public string ResourceType { get; set; } = "Patient";
    public string Id { get; set; } = string.Empty;
    public List<FhirIdentifier> Identifier { get; set; } = new();
    public bool Active { get; set; } = true;
    public List<FhirHumanName> Name { get; set; } = new();
    public List<FhirContactPoint> Telecom { get; set; } = new();
    public string Gender { get; set; } = "unknown";
    public string? BirthDate { get; set; }
}

public class FhirAttachment
{
    public string ContentType { get; set; } = "application/pdf";
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class FhirDocumentContent
{
    public FhirAttachment Attachment { get; set; } = new();
}

public class FhirDocumentReferenceResource
{
    public string ResourceType { get; set; } = "DocumentReference";
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = "current";
    public string DocStatus { get; set; } = "final";
    public string Date { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<FhirDocumentContent> Content { get; set; } = new();
}

public class FhirBundleEntry<T>
{
    public string FullUrl { get; set; } = string.Empty;
    public T Resource { get; set; } = default!;
}

public class FhirBundleResponse<T>
{
    public string ResourceType { get; set; } = "Bundle";
    public string Type { get; set; } = "searchset";
    public int Total { get; set; }
    public List<FhirBundleEntry<T>> Entry { get; set; } = new();
}
