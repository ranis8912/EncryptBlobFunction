using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public static class EncryptBlobFunction
{
    public class EncryptRequest
    {
        public string StorageAccountName { get; set; }
        public string BlobContainerName { get; set; }
        public string BlobFileName { get; set; }
        public string KeyVaultUrl { get; set; }
        public string KeyName { get; set; }
    }

    [FunctionName("EncryptBlobFunction")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        ILogger log)
    {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<EncryptRequest>(requestBody);

        if (data == null)
            return new BadRequestObjectResult("Invalid request body.");

        var credential = new DefaultAzureCredential();

        // Step 1: Download file from Blob
        string blobUri = $"https://{data.StorageAccountName}.blob.core.windows.net/{data.BlobContainerName}/{data.BlobFileName}";
        var blobClient = new BlobClient(new Uri(blobUri), credential);
        var downloadStream = new MemoryStream();
        await blobClient.DownloadToAsync(downloadStream);
        byte[] fileBytes = downloadStream.ToArray();

        // Step 2: Generate AES key and encrypt file
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        aes.GenerateIV();

        byte[] encryptedFile;
        using (var aesEncryptor = aes.CreateEncryptor())
        using (var ms = new MemoryStream())
        {
            using var cryptoStream = new CryptoStream(ms, aesEncryptor, CryptoStreamMode.Write);
            cryptoStream.Write(fileBytes, 0, fileBytes.Length);
            cryptoStream.FlushFinalBlock();
            encryptedFile = ms.ToArray();
        }

        // Step 3: Get RSA public key from Key Vault
        var keyClient = new KeyClient(new Uri(data.KeyVaultUrl), credential);
        var key = await keyClient.GetKeyAsync(data.KeyName);
        RSA rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = key.Value.Key.N,
            Exponent = key.Value.Key.E
        });

        // Step 4: Encrypt AES key with RSA
        byte[] encryptedAesKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);
        byte[] encryptedIv = rsa.Encrypt(aes.IV, RSAEncryptionPadding.OaepSHA256);

        // Step 5: Combine encrypted AES key, IV, and file
        using var finalStream = new MemoryStream();
        finalStream.Write(BitConverter.GetBytes(encryptedAesKey.Length));
        finalStream.Write(encryptedAesKey);
        finalStream.Write(BitConverter.GetBytes(encryptedIv.Length));
        finalStream.Write(encryptedIv);
        finalStream.Write(encryptedFile);

        // Step 6: Upload encrypted blob
        string encryptedBlobUri = $"https://{data.StorageAccountName}.blob.core.windows.net/{data.BlobContainerName}/{data.BlobFileName}.encrypted";
        var encryptedBlobClient = new BlobClient(new Uri(encryptedBlobUri), credential);
        finalStream.Position = 0;
        await encryptedBlobClient.UploadAsync(finalStream, overwrite: true);

        return new OkObjectResult($"Encrypted file uploaded as {data.BlobFileName}.encrypted");
    }
}
