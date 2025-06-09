using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SkillRadar.Console.Models;

namespace SkillRadar.Console.Services
{
    public class AzureStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _reportsContainerName = "reports";
        private readonly string _articlesContainerName = "articles";
        private readonly string _archiveContainerName = "archive";

        public AzureStorageService(string? connectionStringOrAccountName)
        {
            if (string.IsNullOrEmpty(connectionStringOrAccountName))
            {
                throw new ArgumentException("Azure Storage connection string or account name is required", nameof(connectionStringOrAccountName));
            }
            
            // Check if it's a connection string or just account name
            if (connectionStringOrAccountName.Contains("DefaultEndpointsProtocol"))
            {
                _blobServiceClient = new BlobServiceClient(connectionStringOrAccountName);
            }
            else
            {
                // Assume it's an account name and use DefaultAzureCredential for managed identity
                var blobUri = new Uri($"https://{connectionStringOrAccountName}.blob.core.windows.net");
                _blobServiceClient = new BlobServiceClient(blobUri, new Azure.Identity.DefaultAzureCredential());
            }
        }

        public async Task UploadReportAsync(string fileName, string content, string contentType = "text/plain")
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_reportsContainerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                var blobClient = containerClient.GetBlobClient(fileName);
                var bytes = Encoding.UTF8.GetBytes(content);
                
                using var stream = new MemoryStream(bytes);
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
                };
                await blobClient.UploadAsync(stream, uploadOptions);
                
                System.Console.WriteLine($"✅ Uploaded report: {fileName} to Azure Storage");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ Failed to upload report {fileName}: {ex.Message}");
                throw;
            }
        }

        public async Task UploadArticlesAsync(List<Article> articles, string fileName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_articlesContainerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                var json = JsonSerializer.Serialize(articles, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var blobClient = containerClient.GetBlobClient(fileName);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                using var stream = new MemoryStream(bytes);
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
                };
                await blobClient.UploadAsync(stream, uploadOptions);
                
                System.Console.WriteLine($"✅ Uploaded {articles.Count} articles: {fileName} to Azure Storage");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ Failed to upload articles {fileName}: {ex.Message}");
                throw;
            }
        }

        public async Task UploadTrendReportAsync(TrendReport trendReport, string fileName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_archiveContainerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                var json = JsonSerializer.Serialize(trendReport, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var blobClient = containerClient.GetBlobClient(fileName);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                using var stream = new MemoryStream(bytes);
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
                };
                await blobClient.UploadAsync(stream, uploadOptions);
                
                System.Console.WriteLine($"✅ Uploaded trend report: {fileName} to Azure Storage");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ Failed to upload trend report {fileName}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_reportsContainerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
                System.Console.WriteLine("✅ Azure Storage connection successful");
                return true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ Azure Storage connection failed: {ex.Message}");
                return false;
            }
        }
    }
}