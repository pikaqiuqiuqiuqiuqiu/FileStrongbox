using FileStrongbox.Models;

namespace FileStrongbox.Services;

public interface ICryptoService
{
    Task EncryptFileAsync(string sourceFile, string destFile, string password, string originalFileName);
    Task<string> DecryptFileAsync(string sourceFile, string destFile, string password);
    string GenerateEncryptedFileName(string originalFileName, string password, FileNameFormat format, string customExtension);
    Task<string?> GetOriginalFileNameAsync(string encryptedFile, string password);
}
