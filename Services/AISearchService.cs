using Azure;
using Azure.Core;
using Azure.Search.Documents;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AICS.Examples.Services
{
    public class AISearchSaveResponse
    {
        public bool success { get; set; }
        public Guid id { get; set; }
    }

    public class AgentFeedback
    {
        public string AgentId { get; set; }
        public string ThreadId { get; set; }
        public string RunId { get; set; }
        public string Role { get; set; }
        public string MessageText { get; set; }
    }

    public class AISearchService
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint = "https://asst-aisearchservice.search.windows.net";
        private readonly string _apiKey = "defaultAPIKey";
        private readonly string _indexName = "defaultIndexName";
        internal EmbeddingService _embeddingService = null;
        internal SearchClient _searchClient = null;
        public AISearchService(HttpClient http, IConfiguration config, string indexName = "")
        {
            _httpClient = http;
            _endpoint = config["AISearchService:Endpoint"];
            _apiKey = config["AISearchService:ApiKey"];

            if (!String.IsNullOrEmpty(indexName)) {
                _indexName = indexName;
            }
            else {
                _indexName = config["AISearchService:IndexName"];
            }
        }

        public AISearchService(HttpClient http, string endPoint, string apiKey, string indexName)
        {
            _httpClient = http;
            _endpoint = endPoint;
            _apiKey = apiKey;
            _indexName = indexName;
        }

        public EmbeddingService EmbeddingService
        {
            get
            {
                return _embeddingService;
            }
            set
            {
                _embeddingService = value;
            }
        }

        public void Start()
        {
            _searchClient = new SearchClient(
                new Uri(_endpoint),
                _indexName,
                new AzureKeyCredential(_apiKey)
            );
        }

        public void End()
        {
            _searchClient = null;
        }
    }
}
