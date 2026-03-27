using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace AICS.Examples.Services
{
    public class EmbeddingResult
    {
        public List<float> Vector { get; set; }
        public string Input { get; set; }
    }

    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _deploymentName;
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _apiVersion = "2023-05-15";

        // In-memory cache to avoid repeated embedding calls
        private readonly Dictionary<string, List<float>> _cache = new(StringComparer.OrdinalIgnoreCase);

        // Semantic similarity threshold (cosine similarity)
        private readonly double _similarityThreshold;

        public EmbeddingService(HttpClient http, IConfiguration config, double similarityThreshold = 0.92)
        {
            _httpClient = http;
            _deploymentName = config["EmbeddingService:Deployment"];
            _endpoint = config["EmbeddingService:Endpoint"];
            _apiKey = config["EmbeddingService:ApiKey"];
            _similarityThreshold = similarityThreshold;
        }

        public async Task<EmbeddingResult> GetEmbeddingAsync(string input)
        {
            EmbeddingResult er = new EmbeddingResult();
            er.Input = input;
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or empty", nameof(input));

            if (_cache.TryGetValue(input, out var cached))
            {
                er.Vector = cached;
                return er;
            }                

            var url = $"{_endpoint}/openai/deployments/{_deploymentName}/embeddings?api-version={_apiVersion}";

            var payload = new { input };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var vector = doc.RootElement
                            .GetProperty("data")[0]
                            .GetProperty("embedding")
                            .EnumerateArray()
                            .Select(v => v.GetSingle())
                            .ToList();

            _cache[input] = vector;
            er.Vector = vector;

            return er;
        }

        // Generate embedding for a query (wrapper)
        public Task<EmbeddingResult> GenerateQueryEmbeddingAsync(string query)
            => GetEmbeddingAsync(query);

        // Cosine similarity between two vectors
        public double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            if (a.Count != b.Count)
                throw new InvalidOperationException("Vector lengths do not match for cosine similarity.");

            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Count; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        // Check if two vectors are semantically duplicates
        public bool IsSemanticallyDuplicate(IReadOnlyList<float> a, IReadOnlyList<float> b)
            => CosineSimilarity(a, b) >= _similarityThreshold;

        // Clear cache if needed (e.g., thread/session reset)
        public void ClearCache() => _cache.Clear();
    }
}
