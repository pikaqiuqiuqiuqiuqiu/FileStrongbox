using FileStrongbox.Models;

namespace FileStrongbox.Services;

public interface IFileService
{
    Task<OperationResult> EncryptAsync(string path, string password, FileNameFormat format, string customExtension, IProgress<ProgressInfo>? progress = null);
    Task<OperationResult> DecryptAsync(string path, string password, IProgress<ProgressInfo>? progress = null);
}

public record OperationResult(
    bool Success,
    string Message,
    int ProcessedFiles = 0,
    int FailedFiles = 0,
    List<string>? FailedFileNames = null);

public record ProgressInfo(string CurrentFile, int ProcessedFiles, int TotalFiles, double Percentage);
