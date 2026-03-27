using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AICS.Examples.AgentServices
{
    /// <summary>
    /// Lightweight, in-memory, Responses API-based agent-like service.
    /// - No persistent agent resource
    /// - No thread resource
    /// - Uses system instructions + previous_response_id
    /// - Good for demos / Hello World samples
    /// </summary>
    public class AgentThreadService : AgentServiceBase
    {
        private Guid _currentThreadId = Guid.Empty;
        public virtual string Model { get; set; }
        public virtual string SystemPrompt { get; set; }
        public Guid CurrentThreadId { 
            get {
                return _currentThreadId;
            }
        }

        public string? PreviousResponseId { get; private set; }

        /// <summary>
        /// If true, the service chains turns using previous_response_id.
        /// If false, each turn is stateless unless prior context is manually supplied.
        /// </summary>
        public bool UseConversationState { get; set; } = true;

        /// <summary>
        /// If true, asks the service to store response state server-side.
        /// </summary>
        public bool StoreResponses { get; set; } = true;

        public AgentThreadService(
            HttpClient http,
            IConfiguration config,
            string model,
            string? agentName = null)
            : base(http, config, agentName ?? "ThreadAgent", null)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));

            Message message = new Message
            {
                role = "system",
                content = this.SystemPrompt
            };            
        }

        public virtual async Task<string> StartThreadAsync(bool ignoreStartPrompt = false)
        {
            _currentThreadId = Guid.NewGuid();
            Warn($"{AgentName} - Starting New Thread {_currentThreadId}");

            // 3. Send to agent
            string response = await SendMessageAsync(new Message
            {
                role = "system",
                content = this.SystemPrompt
            });

            return _currentThreadId.ToString();
        }

        public void ResetConversation()
        {
            PreviousResponseId = null;
            Warn("Conversation reset. previous_response_id cleared.");
        }

        public override async Task<string> SendMessageAsync(
            Message messagePayload,
            bool waitForResponse = true,
            bool doNotStartThreadAsync = false)
        {
            try
            {
                AgentResponse response = await ExecuteTurnAsync(messagePayload);

                if (!string.IsNullOrWhiteSpace(response.ResponseText))
                {
                    Warn($"{AgentName} - Response: {response.ResponseText} ({response.TimeTaken}ms)");
                    return response.ResponseText;
                }

                //base.InsertUserMemory(messagePayload.content);
                //base.InsertAgentResponse(response.ResponseText);

                return string.Empty;
            }
            catch (Exception ex)
            {
                string errorMsg = "SendMessageAsync - Error: " + ex.Message;
                Error(errorMsg);
                return errorMsg;
            }
        }

        public async Task<AgentResponse> ExecuteTurnAsync(
            Message messagePayload)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", await GetToken());

                var request = BuildResponsesRequest(messagePayload);
                string url = $"{_foundryEndpoint}/openai/v1/responses";

                HttpResponseMessage httpResponse = await _http.PostAsJsonAsync(url, request);
                string rawJson = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Responses API call failed ({(int)httpResponse.StatusCode}): {rawJson}");
                }

                using JsonDocument json = JsonDocument.Parse(rawJson);
                JsonElement root = json.RootElement;

                string responseId = root.TryGetProperty("id", out var idProp)
                    ? idProp.GetString() ?? string.Empty
                    : string.Empty;

                string outputText = ExtractOutputText(root);

                if (UseConversationState && !string.IsNullOrWhiteSpace(responseId))
                {
                    PreviousResponseId = responseId;
                }

                sw.Stop();

                return new AgentResponse
                {
                    ResponseId = responseId,
                    ResponseText = outputText,
                    TimeTaken = (int)sw.ElapsedMilliseconds,
                    ResponseJson = rawJson
                };
            }
            catch
            {
                sw.Stop();
                throw;
            }
        }

        public override Task EndThreadAsync(string reason = "manual")
        {
            ResetConversation();
            return Task.CompletedTask;
        }

        private object BuildResponsesRequest(Message messagePayload)
        {
            var input = new List<object>();

            if (!string.IsNullOrWhiteSpace(SystemPrompt))
            {
                input.Add(new
                {
                    role = "system",
                    content = SystemPrompt
                });
            }

            input.Add(new
            {
                role = string.IsNullOrWhiteSpace(messagePayload.role) ? "user" : messagePayload.role,
                content = messagePayload.content ?? string.Empty
            });

            var request = new Dictionary<string, object?>()
            {
                ["model"] = Model,
                ["input"] = input,
                ["store"] = StoreResponses
            };

            if (UseConversationState && !string.IsNullOrWhiteSpace(PreviousResponseId))
            {
                request["previous_response_id"] = PreviousResponseId;
            }

            return request;
        }

        private static string ExtractOutputText(JsonElement root)
        {
            if (!root.TryGetProperty("output", out JsonElement outputArray) ||
                outputArray.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            var parts = new List<string>();

            foreach (JsonElement outputItem in outputArray.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("type", out JsonElement typeProp))
                    continue;

                if (!string.Equals(typeProp.GetString(), "message", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!outputItem.TryGetProperty("content", out JsonElement contentArray) ||
                    contentArray.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement contentItem in contentArray.EnumerateArray())
                {
                    if (!contentItem.TryGetProperty("type", out JsonElement contentTypeProp))
                        continue;

                    if (!string.Equals(contentTypeProp.GetString(), "output_text", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (contentItem.TryGetProperty("text", out JsonElement textProp))
                    {
                        string? text = textProp.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            parts.Add(text);
                        }
                    }
                }
            }

            return string.Join("", parts);
        }
    }
}