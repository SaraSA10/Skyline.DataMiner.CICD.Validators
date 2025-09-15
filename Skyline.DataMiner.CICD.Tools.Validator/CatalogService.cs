using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
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
        private readonly string _apiKey;

        public CatalogService(ILogger logger, string apiKey)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
            }
        }

        public async Task<string> DownloadPreviousProtocolVersion(string catalogId, IProtocolModel currentProtocol)
        {
            try
            {
                string protocolName = currentProtocol.Protocol?.Name?.Value;
                string currentVersion = currentProtocol.Protocol?.Version?.Value;

                if (string.IsNullOrEmpty(protocolName) || string.IsNullOrEmpty(currentVersion))
                {
                    throw new Exception("Could not determine protocol name or version from protocol.xml");
                }

                string previousVersion = GetPreviousVersion(currentVersion);
                string downloadUrl = $"https://api.dataminer.services/api/key-catalog/v2-0/{catalogId}/versions/{previousVersion}/download";

                // Use the _httpClient that has the API key set
                HttpResponseMessage response = await _httpClient.GetAsync(downloadUrl);

                // If download fails with 404/400, try to find correct catalog ID
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    _logger.LogWarning($"Catalog ID {catalogId} appears invalid. Searching public catalog...");
                    string publicCatalogId = await FindCatalogIdFromPublicCatalog(protocolName);

                    if (publicCatalogId != null)
                    {
                        _logger.LogInformation($"Found catalog ID {publicCatalogId} from public catalog. Retrying download...");
                        downloadUrl = $"https://api.dataminer.services/api/key-catalog/v2-0/{publicCatalogId}/versions/{previousVersion}/download";
                        response = await _httpClient.GetAsync(downloadUrl);
                    }
                }

                response.EnsureSuccessStatusCode();

                string tempFilePath = Path.Combine(Path.GetTempPath(), $"{protocolName}_{previousVersion}.xml");
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
        public async Task<string> FindCatalogIdFromPublicCatalog(string protocolName)
        {
            // Create a separate HttpClient for public catalog that doesn't require API key
            using (var publicCatalogClient = new HttpClient())
            {
                try
                {
                    if (string.IsNullOrEmpty(protocolName))
                    {
                        _logger.LogError("Protocol name is required to search public catalog");
                        return null;
                    }

                    // Use the correct API endpoint and parameters for public catalog
                    string searchUrl = $"https://api.dataminer.services/api/public-catalog/v2-0/catalogs/search?query={Uri.EscapeDataString(protocolName)}&pageLimit=50";

                    HttpResponseMessage response = await publicCatalogClient.GetAsync(searchUrl);

                    // Log detailed error information
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"Public catalog API returned {response.StatusCode}: {errorContent}");
                        return null;
                    }

                    string responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug($"Public catalog response: {responseContent}");

                    using JsonDocument doc = JsonDocument.Parse(responseContent);
                    JsonElement root = doc.RootElement;

                    // Handle the correct response format with "catalogSearchPage" array
                    if (root.TryGetProperty("catalogSearchPage", out JsonElement searchPage) && searchPage.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement catalog in searchPage.EnumerateArray())
                        {
                            // Check if this catalog matches our protocol name
                            if (catalog.TryGetProperty("name", out JsonElement nameElement))
                            {
                                string catalogName = nameElement.GetString();
                                if (catalogName != null && catalogName.Equals(protocolName, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (catalog.TryGetProperty("id", out JsonElement idElement))
                                    {
                                        return idElement.GetString();
                                    }
                                }
                            }
                        }

                        // If we didn't find an exact match, return the first result
                        foreach (JsonElement catalog in searchPage.EnumerateArray())
                        {
                            if (catalog.TryGetProperty("id", out JsonElement idElement))
                            {
                                return idElement.GetString();
                            }
                        }
                    }

                    _logger.LogWarning($"No catalog ID found in response for protocol: {protocolName}");
                    _logger.LogDebug($"Response content: {responseContent}");
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to search public catalog for protocol: {ProtocolName}", protocolName);
                    return null;
                }
            }
        }
        private string GetPreviousVersion(string currentVersion)
        {
            var versionParts = currentVersion.Split('.');
            if (versionParts.Length < 4)
            {
                throw new FormatException($"Version format should have at least 4 parts: {currentVersion}");
            }

            if (!int.TryParse(versionParts[3], out int buildNumber))
            {
                throw new FormatException($"Invalid build number in version: {currentVersion}");
            }

            versionParts[3] = (buildNumber - 1).ToString();
            return string.Join(".", versionParts);
        }
    }
}