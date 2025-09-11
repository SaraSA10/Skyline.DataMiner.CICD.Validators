using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Skyline.DataMiner.CICD.Models.Protocol.Read;
using Skyline.DataMiner.CICD.Models.Protocol.Read.Interfaces;

namespace Skyline.DataMiner.CICD.Tools.Validator
{
    internal class CatalogService
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        public CatalogService(ILogger logger, string apiKey)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
            }
        }

        public async Task<string> DownloadPreviousProtocolVersion(string catalogId, IProtocolModel currentProtocol, string tempDirectory)
        {
            try
            {
                string protocolName = currentProtocol.Protocol?.Name?.Value;
                string currentVersion = currentProtocol.Protocol?.Version?.Value;

                if (string.IsNullOrEmpty(protocolName) || string.IsNullOrEmpty(currentVersion))
                {
                    throw new Exception("Could not determine protocol name or version from protocol.xml");
                }

                _logger.LogInformation($"Current version: {currentVersion}");

                string previousVersion = GetPreviousVersion(currentVersion);
                _logger.LogInformation($"Attempting to download version {previousVersion} of {protocolName} from catalog {catalogId}");

                // Use correct base URL and endpoint structure
                string downloadUrl = $"https://api.dataminer.services/api/key-catalog/v2-0/{catalogId}/versions/{previousVersion}/download";

                HttpResponseMessage response = await _httpClient.GetAsync(downloadUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Version {previousVersion} not found. Trying alternative version formats...");

                    string[] alternativeVersions = GetAlternativeVersionFormats(currentVersion);
                    foreach (var altVersion in alternativeVersions)
                    {
                        _logger.LogInformation($"Trying alternative version: {altVersion}");
                        downloadUrl = $"https://api.dataminer.services/api/key-catalog/v2-0/{catalogId}/versions/{altVersion}/download";
                        response = await _httpClient.GetAsync(downloadUrl);

                        if (response.IsSuccessStatusCode)
                        {
                            previousVersion = altVersion;
                            break;
                        }
                    }
                }

                response.EnsureSuccessStatusCode();

                string tempFilePath = Path.Combine(tempDirectory, $"{protocolName}_{previousVersion}.xml");
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                _logger.LogInformation($"Downloaded previous version {previousVersion} to: {tempFilePath}");
                return tempFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download previous protocol version from catalog");
                throw;
            }
        }
        private string GetPreviousVersion(string currentVersion)
        {
            // Parse the version and decrement the last component
            var versionParts = currentVersion.Split('.');
            if (versionParts.Length < 4)
            {
                throw new FormatException($"Version format should have at least 4 parts: {currentVersion}");
            }

            if (!int.TryParse(versionParts[3], out int buildNumber))
            {
                throw new FormatException($"Invalid build number in version: {currentVersion}");
            }

            if (buildNumber <= 0)
            {
                throw new ArgumentException($"Cannot decrement build number: {currentVersion}");
            }

            // Decrement the build number
            versionParts[3] = (buildNumber - 1).ToString();
            return string.Join(".", versionParts);
        }

        private string[] GetAlternativeVersionFormats(string currentVersion)
        {
            // Try different version formats that might be used in the catalog
            var versionParts = currentVersion.Split('.');

            if (versionParts.Length >= 4)
            {
                // For version 1.2.0.2, try:
                // - 1.2.0.1 (already tried)
                // - 1.2.0
                // - 1.2
                return new[]
                {
                    $"{versionParts[0]}.{versionParts[1]}.{versionParts[2]}", // 1.2.0
                    $"{versionParts[0]}.{versionParts[1]}" // 1.2
                };
            }

            return new string[0];
        }
    }
}