using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AICS.Examples.AgentServices
{
    public interface IAgentService
    {
        string AgentName { get; }
        string? AgentId { get; }

        Task<string> ReasonAsync(
            string msg);

        Task<string> SendMessageAsync(
            string message,
            bool waitForResponse = true,
            bool doNotStartThreadAsync = false);

        Task<string> SendMessageAsync(
            Message messagePayload,
            bool waitForResponse = true,
            bool doNotStartThreadAsync = false);

        Task EndThreadAsync(string reason = "manual");
    }

    public enum ReturnFormat
    {
        JSON,
        Text
    }

    public class Message
    {
        [JsonPropertyName("role")]
        public string role { get; set; }
        [JsonPropertyName("content")]
        public string content { get; set; }
    }

    public class AgentResponse
    {
        public string ResponseId { get; set; } = string.Empty;
        public string ResponseText { get; set; }
        public string ResponseJson { get; set; }

        public int TimeTaken { get; set; }
        public int Tokens { get; set; }
        public string ResponseType { get; set; }


        public string ToolName { get; set; }
        public string ToolOutput { get; set; }
    }
}
