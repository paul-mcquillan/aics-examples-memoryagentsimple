using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace AICS.Examples.AgentServices
{
    public abstract class AgentServiceBase : IAgentService
    {
        protected readonly HttpClient _http;
        protected readonly string _foundryEndpoint;
        protected readonly string _foundryProjectName;
        protected readonly string _tenantId;
        protected readonly string _clientId;
        protected readonly string _clientSecret;

        private readonly int _tokenExpiryMinutes = 60;
        private string _currentToken = string.Empty;
        private DateTime _currentTokenTime = DateTime.MinValue;

        protected AgentServiceBase(HttpClient http, IConfiguration config, string agentName, string? agentId = null)
        {
            _http = http;
            _foundryEndpoint = config["Foundry:FoundryEndpoint"]
                ?? throw new InvalidOperationException("Missing config Foundry:FoundryEndpoint");
            _foundryProjectName = config["Foundry:FoundryProject"]
                ?? throw new InvalidOperationException("Missing config Foundry:FoundryProject");
            _tenantId = config["AzureAd:TenantId"]
                ?? throw new InvalidOperationException("Missing config AzureAd:TenantId");
            _clientId = config["AzureAd:ClientId"]
                ?? throw new InvalidOperationException("Missing config AzureAd:ClientId");
            _clientSecret = config["AzureAd:ClientSecret"]
                ?? throw new InvalidOperationException("Missing config AzureAd:ClientSecret");

            AgentName = agentName;
            AgentId = agentId;
        }

        public string AgentName { get; protected set; }
        public string? AgentId { get; protected set; }

        public LogLevel LogLevel { get; set; } = LogLevel.Information;

        public bool ThreadEndAgent { get; set; }
        public bool ThreadEndReceiveEntities { get; set; }
        public bool QuestionRouter { get; set; }

        public string EntitiesInScope { get; set; } = string.Empty;

        public string UserName { get; set; }

        public List<string> UserInformation { get; } = new();
        public List<string> AgentResponses { get; } = new();

        public virtual void CleanMemory()
        {
            // default does nothing - override for agents with memory to implement cleaning logic (e.g. remove old memories, remove memories with low relevance, etc.)
        }

        public void InsertUserMemory(string message)
        {
            UserInformation.Add(message);
        }

        public void InsertAgentResponse(string message)
        {
            AgentResponses.Add(message);
        }

        public virtual Task<string> ReasonAsync(
            string msg)
        {
            return Task.FromResult("Base agent cannot reason.");
        }

        public virtual Task<string> SendMessageAsync(
            string message,
            bool waitForResponse = true,
            bool doNotStartThreadAsync = false)
        {
            return SendMessageAsync(
                new Message
                {
                    role = "user",
                    content = message
                },
                waitForResponse,
                doNotStartThreadAsync);
        }

        public abstract Task<string> SendMessageAsync(
            Message messagePayload,
            bool waitForResponse = true,
            bool doNotStartThreadAsync = false);

        public abstract Task EndThreadAsync(string reason = "manual");

        protected async Task<string> GetToken()
        {
            if (string.IsNullOrEmpty(_currentToken) ||
                DateTime.UtcNow >= _currentTokenTime.AddMinutes(_tokenExpiryMinutes))
            {
                _currentToken = await TokenHelper.GetTokenAsync(_tenantId, _clientId, _clientSecret);
                _currentTokenTime = DateTime.UtcNow;
            }

            return _currentToken;
        }

        public void Log(string log)
        {
            if (LogLevel == LogLevel.Information)
            {
                Console.WriteLine($"{AgentName}... {log}");
            }
        }

        public void Warn(string warning)
        {
            if (LogLevel == LogLevel.Information || LogLevel == LogLevel.Warning)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{AgentName}: {warning}");
                Console.ResetColor();
            }
        }

        public void Error(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{AgentName}: ERROR - {error}");
            Console.ResetColor();
        }
    }
}
