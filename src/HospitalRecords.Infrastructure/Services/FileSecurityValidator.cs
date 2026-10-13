using System.Security.Cryptography;
using HospitalRecords.Application.Common.Interfaces;

namespace HospitalRecords.Infrastructure.Services;

public class FileSecurityValidator : IFileSecurityValidator
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB limit (FR-DOC-002)

    private static readonly byte[] PdfMagic = { 0x25, 0x50, 0x44, 0x46 }; // %PDF
    private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] TiffLeMagic = { 0x49, 0x49, 0x2A, 0x00 }; // II*.
    private static readonly byte[] TiffBeMagic = { 0x4D, 0x4D, 0x00, 0x2A }; // MM.*
    private static readonly byte[] ExeMagic = { 0x4D, 0x5A }; // MZ header (Windows PE/executable)

    public FileValidationResult Validate(Stream stream, string fileName, long fileSize)
    {
        if (fileSize <= 0)
        {
            return new FileValidationResult(false, null, null, "File is empty.");
        }

        if (fileSize > MaxFileSizeBytes)
        {
            return new FileValidationResult(false, null, null, $"File size exceeds safety limit of {MaxFileSizeBytes / (1024 * 1024)}MB.");
        }

        // Read initial header bytes for magic number verification
        var header = new byte[16];
        var originalPosition = stream.Position;
        stream.Position = 0;
        var bytesRead = stream.Read(header, 0, header.Length);

        if (bytesRead < 4)
        {
            stream.Position = originalPosition;
            return new FileValidationResult(false, null, null, "File header is corrupted or unreadable.");
        }

        // 1. Antivirus / Executable Malicious File Guard (FR-DOC-002)
        if (header[0] == ExeMagic[0] && header[1] == ExeMagic[1])
        {
            stream.Position = originalPosition;
            return new FileValidationResult(false, null, null, "Security policy violation: Executable binaries (.exe/.dll) are prohibited.");
        }

        // 2. Identify allowed true MIME type via magic bytes
        string? contentType = null;
        if (StartsWith(header, PdfMagic))
        {
            contentType = "application/pdf";
        }
        else if (StartsWith(header, JpegMagic))
        {
            contentType = "image/jpeg";
        }
        else if (StartsWith(header, PngMagic))
        {
            contentType = "image/png";
        }
        else if (StartsWith(header, TiffLeMagic) || StartsWith(header, TiffBeMagic))
        {
            contentType = "image/tiff";
        }

        if (contentType == null)
        {
            stream.Position = originalPosition;
            return new FileValidationResult(false, null, null, "Unsupported file format. Only PDF, JPG, PNG, and TIFF are permitted under FR-DOC-001.");
        }

        // 3. Compute cryptographic SHA-256 Checksum
        stream.Position = 0;
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        var checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Reset stream position for downstream consumers
        stream.Position = originalPosition;

        return new FileValidationResult(true, contentType, checksum, null);
    }

    private static bool StartsWith(byte[] data, byte[] prefix)
    {
        if (data.Length < prefix.Length) return false;
        for (int i = 0; i < prefix.Length; i++)
        {
            if (data[i] != prefix[i]) return false;
        }
        return true;
    }
}
