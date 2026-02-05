using FileStrongbox.Models;

namespace FileStrongbox.Services;

public class FileService : IFileService
{
    private readonly ICryptoService _cryptoService;

    public FileService(ICryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }

    public async Task<OperationResult> EncryptAsync(string path, string password, FileNameFormat format, string customExtension, IProgress<ProgressInfo>? progress = null)
    {
        var files = GetAllFiles(path);
        if (files.Count == 0)
        {
            return new OperationResult(false, "没有可加密的文件", 0, 0);
        }

        int processed = 0;
        int failed = 0;
        var failedFiles = new List<string>();

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            try
            {
                progress?.Report(new ProgressInfo(
                    file,
                    i,
                    files.Count,
                    (double)i / files.Count * 100));

                await EncryptSingleFileAsync(file, password, format, customExtension);
                processed++;
            }
            catch (Exception ex)
            {
                failed++;
                failedFiles.Add(Path.GetFileName(file));
                System.Diagnostics.Debug.WriteLine($"加密失败 {file}: {ex.Message}");
            }
        }

        string message;
        if (failed == 0)
        {
            message = $"成功加密 {processed} 个文件";
        }
        else if (processed == 0)
        {
            message = $"加密失败，共 {failed} 个文件";
        }
        else
        {
            message = $"加密完成：{processed} 个成功，{failed} 个失败";
        }

        return new OperationResult(failed == 0, message, processed, failed, failedFiles);
    }

    public async Task<OperationResult> DecryptAsync(string path, string password, IProgress<ProgressInfo>? progress = null)
    {
        var files = GetAllFiles(path);
        if (files.Count == 0)
        {
            return new OperationResult(false, "没有找到文件", 0, 0);
        }

        int processed = 0;
        int failed = 0;
        var failedFiles = new List<string>();

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            try
            {
                progress?.Report(new ProgressInfo(
                    file,
                    i,
                    files.Count,
                    (double)i / files.Count * 100));

                await DecryptSingleFileAsync(file, password);
                processed++;
            }
            catch (Exception ex)
            {
                failed++;
                failedFiles.Add(Path.GetFileName(file));
                System.Diagnostics.Debug.WriteLine($"解密失败 {file}: {ex.Message}");
            }
        }

        string message;
        if (failed == 0)
        {
            message = $"成功解密 {processed} 个文件";
        }
        else if (processed == 0)
        {
            message = $"解密失败，可能是密码错误或文件未加密";
        }
        else
        {
            message = $"解密完成：{processed} 个成功，{failed} 个失败（可能是密码错误或文件未加密）";
        }

        return new OperationResult(failed == 0, message, processed, failed, failedFiles);
    }

    private async Task EncryptSingleFileAsync(string sourceFile, string password, FileNameFormat format, string customExtension)
    {
        var directory = Path.GetDirectoryName(sourceFile) ?? ".";
        var originalFileName = Path.GetFileName(sourceFile);
        var encryptedFileName = _cryptoService.GenerateEncryptedFileName(originalFileName, password, format, customExtension);
        var tempFile = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        var destFile = Path.Combine(directory, encryptedFileName);

        try
        {
            await _cryptoService.EncryptFileAsync(sourceFile, tempFile, password, originalFileName);

            if (!File.Exists(tempFile))
            {
                throw new IOException("加密失败：临时文件未创建");
            }

            var tempFileInfo = new FileInfo(tempFile);
            if (tempFileInfo.Length == 0)
            {
                File.Delete(tempFile);
                throw new IOException("加密失败：加密文件为空");
            }

            if (format == FileNameFormat.KeepOriginal)
            {
                var tempDestFile = sourceFile + ".strongbox";

                File.Move(tempFile, tempDestFile);

                try
                {
                    File.Delete(sourceFile);
                }
                catch (IOException)
                {
                    throw new IOException($"源文件被占用，加密文件已保存为: {tempDestFile}");
                }

                try
                {
                    File.Move(tempDestFile, destFile);
                }
                catch (IOException)
                {
                    throw new IOException($"重命名失败，加密文件已保存为: {tempDestFile}");
                }
            }
            else
            {
                if (File.Exists(destFile))
                {
                    File.Delete(destFile);
                }
                File.Move(tempFile, destFile);

                try
                {
                    File.Delete(sourceFile);
                }
                catch (IOException)
                {
                    throw new IOException($"加密完成，但源文件被占用无法删除: {sourceFile}");
                }
            }
        }
        catch (IOException)
        {
            throw;
        }
        catch
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
            throw;
        }
    }

    private async Task DecryptSingleFileAsync(string sourceFile, string password)
    {
        var directory = Path.GetDirectoryName(sourceFile) ?? ".";

        var originalFileName = await _cryptoService.GetOriginalFileNameAsync(sourceFile, password);
        if (string.IsNullOrEmpty(originalFileName))
        {
            throw new InvalidOperationException("解密失败：密码错误或文件未加密");
        }

        var tempFile = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        var destFile = Path.Combine(directory, originalFileName);

        var isSameFile = string.Equals(Path.GetFileName(sourceFile), originalFileName, StringComparison.OrdinalIgnoreCase);

        try
        {
            await _cryptoService.DecryptFileAsync(sourceFile, tempFile, password);

            if (!File.Exists(tempFile))
            {
                throw new IOException("解密失败：临时文件未创建");
            }

            if (isSameFile)
            {
                var tempDestFile = sourceFile + ".decrypted";
                File.Move(tempFile, tempDestFile);

                try
                {
                    File.Delete(sourceFile);
                }
                catch (IOException)
                {
                    throw new IOException($"源文件被占用，解密文件已保存为: {tempDestFile}");
                }

                try
                {
                    File.Move(tempDestFile, destFile);
                }
                catch (IOException)
                {
                    throw new IOException($"重命名失败，解密文件已保存为: {tempDestFile}");
                }
            }
            else
            {
                if (File.Exists(destFile))
                {
                    var baseName = Path.GetFileNameWithoutExtension(originalFileName);
                    var ext = Path.GetExtension(originalFileName);
                    int counter = 1;
                    while (File.Exists(destFile))
                    {
                        destFile = Path.Combine(directory, $"{baseName} ({counter}){ext}");
                        counter++;
                    }
                }
                File.Move(tempFile, destFile);

                try
                {
                    File.Delete(sourceFile);
                }
                catch (IOException)
                {
                    throw new IOException($"解密完成，但加密源文件被占用无法删除: {sourceFile}");
                }
            }
        }
        catch (IOException)
        {
            throw;
        }
        catch
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
            throw;
        }
    }

    private List<string> GetAllFiles(string path)
    {
        var files = new List<string>();

        if (File.Exists(path))
        {
            files.Add(path);
        }
        else if (Directory.Exists(path))
        {
            var allFiles = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                if (!Path.GetFileName(file).StartsWith("."))
                {
                    files.Add(file);
                }
            }
        }

        return files;
    }
}
