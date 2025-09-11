using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Skyline.DataMiner.CICD.Tools.Validator
{
    internal class APICall
    {
       
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public APICall(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
        }

        public async Task<string> DownloadProtocolVersionAsync(string catalogId, string version, string outputDirectory)
        {
            string url = $"https://catalogapi-prod.cca-prod.aks.westeurope.dataminer.services/api/key-catalog/v2-0/{catalogId}/version/{version}/download";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string fileName = $"protocol_{catalogId}_{version}.xml";
            string filePath = Path.Combine(outputDirectory, fileName);

            using (var fileStream = File.Create(filePath))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            return filePath;
        }
    }
    
}

