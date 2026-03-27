using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

using Azure.Search.Documents.Models;
using Azure.Search.Documents;

using Microsoft.Extensions.Configuration;
using AICS.Examples.Services;

namespace AICS.Examples.Memory
{
    public class MemoryService : AISearchService
    {
        EmbeddingService _embeddingService = null;
        private int _top = 100;

        public MemoryService(HttpClient http, IConfiguration config) : base(http, config)
        {
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

        public async Task<MemoryServiceResponse> GetMemoriesSimple(MemoryServiceRequest req)
        {
            MemoryServiceResponse response = new MemoryServiceResponse();
            response.Memories = new List<MemoryDocument>();
            response.Timestamp = DateTime.UtcNow;

            if (req.Entities == null)
                return response;

            if (req.Entities.Count == 0)
            {
                var filter = $"";

                if (req.MemoryTypes != null && req.MemoryTypes.Any())
                {
                    var typesFilter = string.Join(" or ", req.MemoryTypes.Select(t => $"Type eq '{t}'"));
                    filter += $" and ({typesFilter})";
                }

                var options = new SearchOptions
                {
                    Filter = filter,
                    Size = _top
                };

                SearchResults<MemoryDocument> results = await _searchClient.SearchAsync<MemoryDocument>(req.Search, options);

                await foreach (SearchResult<MemoryDocument> result in results.GetResultsAsync())
                {
                    if (result != null)
                    {
                        response.Memories.Add(result.Document);
                    }
                }
            }
            else
            {
                foreach (var entity in req.Entities)
                {
                    // Semantic search query: filter by CrmId and CrmType
                    var filter = $"Associations/any(a: a/CrmId eq '{entity.CrmId}' and a/CrmType eq '{entity.CrmType}')";

                    if (req.MemoryTypes != null && req.MemoryTypes.Any())
                    {
                        var typesFilter = string.Join(" or ", req.MemoryTypes.Select(t => $"Type eq '{t}'"));
                        filter += $" and ({typesFilter})";
                    }

                    var options = new SearchOptions
                    {
                        Filter = filter,
                        Size = _top
                    };

                    SearchResults<MemoryDocument> results = await _searchClient.SearchAsync<MemoryDocument>(req.Search, options);

                    await foreach (SearchResult<MemoryDocument> result in results.GetResultsAsync())
                    {
                        if (result != null)
                        {
                            response.Memories.Add(result.Document);
                        }
                    }
                }
            }

            response.Memories = response.Memories
                .OrderByDescending(response => response.Confidence)
                .ThenByDescending(m => m.Timestamp)
                .ToList();

            return response;
        }

        public async Task<MemoryServiceResponse> GetMemoriesSemantic(
            MemoryServiceRequest req)
        {
            bool entitySpecific = true;

            MemoryServiceResponse response = new MemoryServiceResponse();
            response.Memories = new List<MemoryDocument>();
            response.Timestamp = DateTime.UtcNow;

            if (req.Entities == null)
            {
                entitySpecific = false;
            }
            else
            {
                if (req.Entities.Count == 0)
                    entitySpecific = false;
            }

            try
            {
                if ( entitySpecific == true )
                {
                    foreach (var entity in req.Entities)
                    {
                        // Semantic search query: filter by CrmId and CrmType
                        var filter = $"Associations/any(a: a/CrmId eq '{entity.CrmId}' and a/CrmType eq '{entity.CrmType}')";

                        var options = new SearchOptions
                        {
                            Size = _top,
                            Filter = filter,
                            IncludeTotalCount = true,
                            QueryType = SearchQueryType.Semantic,
                            SemanticSearch = new SemanticSearchOptions()
                            {
                                SemanticConfigurationName = "default-semantic-config"
                            }
                        };

                        // You can also ask a specific question here instead of "*" if you want AI-generated answers
                        var searchResponse = await _searchClient.SearchAsync<MemoryDocument>(req.Search, options);

                        if (searchResponse.Value.SemanticSearch?.Answers != null)
                        {
                            Console.WriteLine("Extractive Answers:");
                            foreach (var answer in searchResponse.Value.SemanticSearch.Answers)
                            {
                                Console.WriteLine($"  {answer.Highlights}");
                            }
                        }

                        await foreach (SearchResult<MemoryDocument> result in searchResponse.Value.GetResultsAsync())
                        {
                            // MemoryDocument object already mapped from index
                            response.Memories.Add(result.Document);

                            // Optional: include highlights
                            if (result.Highlights != null && result.Highlights.Count > 0)
                            {
                                foreach (var highlight in result.Highlights)
                                {
                                    // For demonstration, append highlights to Content
                                    result.Document.Content += "\n[Highlight]: " + string.Join(" | ", highlight.Value);
                                }
                            }
                        }
                    }
                }
                else
                {
                    var options = new SearchOptions
                    {
                        Size = _top,
                        IncludeTotalCount = true,
                        QueryType = SearchQueryType.Semantic,
                        SemanticSearch = new SemanticSearchOptions()
                        {
                            SemanticConfigurationName = "default-semantic-config"
                        }
                    };

                    // You can also ask a specific question here instead of "*" if you want AI-generated answers
                    var searchResponse = await _searchClient.SearchAsync<MemoryDocument>(req.Search, options);

                    if (searchResponse.Value.SemanticSearch?.Answers != null)
                    {
                        Console.WriteLine("Extractive Answers:");
                        foreach (var answer in searchResponse.Value.SemanticSearch.Answers)
                        {
                            Console.WriteLine($"  {answer.Highlights}");
                        }
                    }

                    await foreach (SearchResult<MemoryDocument> result in searchResponse.Value.GetResultsAsync())
                    {
                        // MemoryDocument object already mapped from index
                        response.Memories.Add(result.Document);

                        // Optional: include highlights
                        if (result.Highlights != null && result.Highlights.Count > 0)
                        {
                            foreach (var highlight in result.Highlights)
                            {
                                // For demonstration, append highlights to Content
                                result.Document.Content += "\n[Highlight]: " + string.Join(" | ", highlight.Value);
                            }
                        }
                    }
                }
            }
            catch(Exception searchException)
            {
                throw searchException;
            }

            return response;
        }

        public async Task<AISearchSaveResponse> SaveMemories(List<MemoryDocument> batch)
        {
            Guid id = Guid.NewGuid();

            try
            {
                IndexDocumentsResult result = await _searchClient.MergeOrUploadDocumentsAsync(batch);

                // Check results
                foreach (var r in result.Results)
                {
                    Console.WriteLine($"Key: {r.Key}, Succeeded: {r.Succeeded}, ErrorMessage: {r.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                // if we hit a problem, go 1 memory at a time
                foreach (MemoryDocument md in batch)
                {
                    try
                    {
                        IndexDocumentsResult result = _searchClient.MergeOrUploadDocuments<MemoryDocument>(new List<MemoryDocument>() { md });
                        foreach (var r in result.Results)
                        {
                            Console.WriteLine($"Key: {r.Key}, Succeeded: {r.Succeeded}, ErrorMessage: {r.ErrorMessage}");
                        }
                    }
                    catch (Exception memorySaveFail)
                    {
                        Console.WriteLine("Failure to save Memory " + md.MemoryId + ": " + memorySaveFail.Message);
                    }
                }
            }

            return new AISearchSaveResponse { success = true, id = id };
        }

    }

    public class MemoryServiceRequest
    {
        public List<MemoryServiceRequestEntity> Entities;
        public string Search = "*";
        public List<MemoryType> MemoryTypes { get; set; } = new List<MemoryType>();
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class MemoryServiceRequestEntity
    {
        public string CrmId;
        public string CrmType;

        public MemoryServiceRequestEntity(string crmId, string crmType)
        {
            CrmId = crmId;
            CrmType = crmType;
        }
    }

    public class MemoryServiceResponse
    {
        public string ThreadId { get; set; }
        public DateTime Timestamp { get; set; }

        public List<MemoryDocument> Memories { get; set; }

        public string Summary { get; set; }

        public static MemoryServiceResponse FromJson(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
                throw new ArgumentException("Response is empty", nameof(rawResponse));

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            // 1️⃣ Extract JSON from inside triple-backtick blocks (if present)
            var jsonMatch = Regex.Match(rawResponse, "```json\\s*(.*?)\\s*```", RegexOptions.Singleline);
            var jsonText = jsonMatch.Success ? jsonMatch.Groups[1].Value : rawResponse;

            // 2️⃣ Parse the JSON document
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            // 3️⃣ Pull key fields safely
            /*var threadId = root.TryGetProperty("thread_id", out var t) ? t.GetString() : Guid.NewGuid().ToString();
            var timestamp = root.TryGetProperty("timestamp", out var ts)
                ? ts.GetDateTime()
                : DateTime.UtcNow;
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : string.Empty;*/

            // 4️⃣ Handle either "memories" or "new_memories"
            JsonElement memoriesElement = root;
            /*if (!root.TryGetProperty("memories", out memoriesElement))
            {
                root.TryGetProperty("new_memories", out memoriesElement);
            }*/

            List<MemoryDocument> memoryList = new List<MemoryDocument>();
            try
            {
                var memories = JsonSerializer.Deserialize<List<MemoryDocument>>(memoriesElement.GetRawText(), options);

                memoryList.AddRange(memories);
            }
            catch(Exception singleMemEx)
            {
                var memory = JsonSerializer.Deserialize<MemoryDocument>(memoriesElement.GetRawText(), options);

                memoryList.Add(memory);
            }

            // 5️⃣ Deserialize into list of MemoryDocuments

            // 6️⃣ Ensure field defaults for nlls
            foreach (var m in memoryList)
            {
                if (m.Associations == null)
                {
                    m.Associations ??= new List<Association>();
                }
                else
                {
                    // has Associations
                }
                if ( m.Tags == null )
                    m.Tags ??= new List<string>();
                if (string.IsNullOrEmpty(m.SourceAgent)) m.SourceAgent = "MemoryAgent";
                if (m.Timestamp == default) m.Timestamp = DateTime.UtcNow;
            }

            return new MemoryServiceResponse
            {
                Memories = memoryList,
            };
        }
    }
}
