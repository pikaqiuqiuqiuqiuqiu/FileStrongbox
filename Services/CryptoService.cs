using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using FileStrongbox.Models;

namespace FileStrongbox.Services;

public class CryptoService : ICryptoService
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int Iterations = 100000;
    private const int ChunkSize = 1024 * 1024;

    public async Task EncryptFileAsync(string sourceFile, string destFile, string password, string originalFileName)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("密码不能为空", nameof(password));
        if (string.IsNullOrEmpty(originalFileName))
            throw new ArgumentException("文件名不能为空", nameof(originalFileName));
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException("源文件不存在", sourceFile);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = DeriveKey(password, salt);

        var fileNameBytes = Encoding.UTF8.GetBytes(originalFileName);
        var encryptedFileName = EncryptData(fileNameBytes, key, out var fileNameNonce, out var fileNameTag);

        await using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        await destStream.WriteAsync(salt);
        await destStream.WriteAsync(fileNameNonce);
        await destStream.WriteAsync(fileNameTag);
        var fileNameLengthBytes = BitConverter.GetBytes(encryptedFileName.Length);
        await destStream.WriteAsync(fileNameLengthBytes);
        await destStream.WriteAsync(encryptedFileName);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        await destStream.WriteAsync(nonce);

        using var aesGcm = new AesGcm(key, TagSize);
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        var encryptedBuffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        var outputBuffer = ArrayPool<byte>.Shared.Rent(ChunkSize + 4 + TagSize);
        var tag = new byte[TagSize];

        try
        {
            int bytesRead;
            long chunkIndex = 0;

            while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, ChunkSize))) > 0)
            {
                var plaintext = buffer.AsSpan(0, bytesRead);
                var ciphertext = encryptedBuffer.AsSpan(0, bytesRead);

                var aad = BitConverter.GetBytes(chunkIndex);
                var chunkNonce = GenerateChunkNonce(nonce, chunkIndex);

                aesGcm.Encrypt(chunkNonce, plaintext, ciphertext, tag, aad);

                BitConverter.TryWriteBytes(outputBuffer.AsSpan(0, 4), bytesRead);
                ciphertext.CopyTo(outputBuffer.AsSpan(4));
                tag.CopyTo(outputBuffer.AsSpan(4 + bytesRead));

                await destStream.WriteAsync(outputBuffer.AsMemory(0, 4 + bytesRead + TagSize));
                chunkIndex++;
            }

            await destStream.WriteAsync(BitConverter.GetBytes(0));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            ArrayPool<byte>.Shared.Return(encryptedBuffer, clearArray: true);
            ArrayPool<byte>.Shared.Return(outputBuffer, clearArray: true);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public async Task<string> DecryptFileAsync(string sourceFile, string destFile, string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("密码不能为空", nameof(password));
        if (!File.Exists(sourceFile))
            throw new FileNotFoundException("加密文件不存在", sourceFile);

        var fileInfo = new FileInfo(sourceFile);
        if (fileInfo.Length < SaltSize + NonceSize + TagSize + 4)
            throw new CryptographicException("文件太小，不是有效的加密文件");

        await using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var salt = new byte[SaltSize];
        await sourceStream.ReadExactlyAsync(salt);
        var key = DeriveKey(password, salt);

        var cipherBuffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        var plainBuffer = ArrayPool<byte>.Shared.Rent(ChunkSize);

        try
        {
            var fileNameNonce = new byte[NonceSize];
            await sourceStream.ReadExactlyAsync(fileNameNonce);
            var fileNameTag = new byte[TagSize];
            await sourceStream.ReadExactlyAsync(fileNameTag);
            var fileNameLengthBytes = new byte[4];
            await sourceStream.ReadExactlyAsync(fileNameLengthBytes);
            var fileNameLength = BitConverter.ToInt32(fileNameLengthBytes);

            if (fileNameLength <= 0 || fileNameLength > 1024)
            {
                throw new CryptographicException("Invalid file format");
            }

            var encryptedFileName = new byte[fileNameLength];
            await sourceStream.ReadExactlyAsync(encryptedFileName);

            var originalFileName = Encoding.UTF8.GetString(
                DecryptData(encryptedFileName, key, fileNameNonce, fileNameTag));

            var nonce = new byte[NonceSize];
            await sourceStream.ReadExactlyAsync(nonce);

            await using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var aesGcm = new AesGcm(key, TagSize);

            var chunkSizeBytes = new byte[4];
            var tag = new byte[TagSize];
            long chunkIndex = 0;

            while (true)
            {
                await sourceStream.ReadExactlyAsync(chunkSizeBytes);
                var chunkSize = BitConverter.ToInt32(chunkSizeBytes);

                if (chunkSize == 0) break;

                if (chunkSize < 0 || chunkSize > ChunkSize)
                {
                    throw new CryptographicException($"Invalid chunk size: {chunkSize}. File may be corrupted or encrypted with incompatible version.");
                }

                await sourceStream.ReadExactlyAsync(cipherBuffer.AsMemory(0, chunkSize));
                await sourceStream.ReadExactlyAsync(tag);

                var aad = BitConverter.GetBytes(chunkIndex);
                var chunkNonce = GenerateChunkNonce(nonce, chunkIndex);

                aesGcm.Decrypt(chunkNonce, cipherBuffer.AsSpan(0, chunkSize), tag, plainBuffer.AsSpan(0, chunkSize), aad);
                await destStream.WriteAsync(plainBuffer.AsMemory(0, chunkSize));

                chunkIndex++;
            }

            return originalFileName;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(cipherBuffer, clearArray: true);
            ArrayPool<byte>.Shared.Return(plainBuffer, clearArray: true);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public string GenerateEncryptedFileName(string originalFileName, string password, FileNameFormat format, string customExtension)
    {
        var hash = GenerateHashedFileName(originalFileName, password);
        return format switch
        {
            FileNameFormat.KeepOriginal => originalFileName,
            FileNameFormat.FullEncrypt => hash,
            FileNameFormat.NewExtension => hash + customExtension,
            _ => originalFileName
        };
    }

    public async Task<string?> GetOriginalFileNameAsync(string encryptedFile, string password)
    {
        if (string.IsNullOrEmpty(password) || !File.Exists(encryptedFile))
            return null;

        var fileInfo = new FileInfo(encryptedFile);
        if (fileInfo.Length < SaltSize + NonceSize + TagSize + 4)
            return null;

        try
        {
            await using var stream = new FileStream(encryptedFile, FileMode.Open, FileAccess.Read, FileShare.Read);

            var salt = new byte[SaltSize];
            await stream.ReadExactlyAsync(salt);
            var key = DeriveKey(password, salt);

            try
            {
                var fileNameNonce = new byte[NonceSize];
                await stream.ReadExactlyAsync(fileNameNonce);
                var fileNameTag = new byte[TagSize];
                await stream.ReadExactlyAsync(fileNameTag);
                var fileNameLengthBytes = new byte[4];
                await stream.ReadExactlyAsync(fileNameLengthBytes);
                var fileNameLength = BitConverter.ToInt32(fileNameLengthBytes);

                if (fileNameLength <= 0 || fileNameLength > 1024)
                {
                    return null;
                }

                var encryptedFileName = new byte[fileNameLength];
                await stream.ReadExactlyAsync(encryptedFileName);

                return Encoding.UTF8.GetString(
                    DecryptData(encryptedFileName, key, fileNameNonce, fileNameTag));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
    }

    private static byte[] EncryptData(byte[] data, byte[] key, out byte[] nonce, out byte[] tag)
    {
        nonce = RandomNumberGenerator.GetBytes(NonceSize);
        tag = new byte[TagSize];
        var ciphertext = new byte[data.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, data, ciphertext, tag);

        return ciphertext;
    }

    private static byte[] DecryptData(byte[] ciphertext, byte[] key, byte[] nonce, byte[] tag)
    {
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    private static byte[] GenerateChunkNonce(byte[] baseNonce, long chunkIndex)
    {
        var nonce = new byte[NonceSize];
        baseNonce.CopyTo(nonce, 0);

        var indexBytes = BitConverter.GetBytes(chunkIndex);
        for (int i = 0; i < 8; i++)
        {
            nonce[4 + i] ^= indexBytes[i];
        }

        return nonce;
    }

    private static string GenerateHashedFileName(string originalFileName, string password)
    {
        var combined = Encoding.UTF8.GetBytes(originalFileName + password);
        var hash = SHA256.HashData(combined);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
