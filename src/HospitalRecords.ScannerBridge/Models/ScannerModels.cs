namespace HospitalRecords.ScannerBridge.Models;

public record ScannerDeviceDto(
    string DeviceId,
    string Name,
    string Manufacturer,
    string Description,
    bool IsDefault,
    bool IsSimulated
);

public class ScanRequest
{
    public string? DeviceId { get; set; }
    public int ResolutionDpi { get; set; } = 300;
    public string ColorMode { get; set; } = "Color"; // "Color", "Greyscale", "BlackAndWhite"
    public string Source { get; set; } = "Feeder";   // "Feeder", "Flatbed"
    public bool Duplex { get; set; } = false;
}

public record ScanResultDto(
    bool Success,
    string FileName,
    string ContentType,
    long FileSize,
    string Base64Data,
    string? ErrorMessage
);
