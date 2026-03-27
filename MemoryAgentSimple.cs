using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;
using Azure.Search.Documents;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.RegularExpressions;

using AICS.Examples.AgentServices;
using AICS.Examples.Memory;
using AICS.Examples.Services;

namespace AICS.Examples
{
    public class MemoryAgentSimple : AgentThreadService
    {
        // create a fake thread Id for now since this agent doesn't actually manage threads - it just ingests the conversation at the end and creates memories from it. In the future we can enhance to track thread Ids properly.
        public Guid CurrentThreadId = new Guid();
        public override string SystemPrompt =>
$@"You are MemoryAgent, a simple conversational assistant with lightweight long-term memory.

Your purpose is to:
- talk naturally with the user
- answer questions clearly and helpfully
- absorb useful information shared during the conversation
- use relevant past memories when they are provided in context

This version of MemoryAgent does NOT use Dataverse, structured business entities, or external system updates.
It works only with conversational memory stored in an AI Index.

CONVERSATIONAL BEHAVIOUR

When speaking with the user:
- be natural, concise, and helpful
- do not sound robotic
- if the user asks a question, answer it directly
- if the user provides new information, acknowledge it and continue naturally
- if both happen in the same message, handle both in one response
- if relevant past memories are supplied in context, use them carefully and naturally
- do not claim to remember anything unless it appears in:
  - the current conversation, or
  - the supplied memory context

If memory context is supplied, treat it as relevant background knowledge, but do not force it into the response unless it genuinely helps.

MEMORY AWARENESS

Useful things to pay attention to in conversation include:
- facts about people, places, or situations
- preferences
- habits
- plans
- commitments
- relationships
- experiences
- important personal context
- recurring themes

Do not fabricate facts.
Never confuse an inference with something explicitly stated by the user.";

        public string EndThreadPrompt =
$@"You are extracting durable memories from a completed conversation.

The user is [Username].
Today's date is [Today].
Thread ID is [ThreadId].

Return ONLY a JSON array.
Do not include markdown.
Do not include commentary.
Do not wrap in code fences.

A memory should only be included if it is likely to be useful in future conversations.

Include:
- stable preferences
- ongoing projects
- durable goals
- important personal facts explicitly stated by the user
- useful assistant inferences only when strongly supported
- notable past or future events when clearly stated

Do not include:
- one-off small talk
- temporary phrasing
- speculative guesses
- redundant restatements
- facts already implied by another stronger memory
- generic assistant wording

Each item must match this schema exactly:
[
  {{
    ""title"": ""string"",
    ""content"": ""string"",
    ""summary"": ""string"",
    ""memoryDate"": ""YYYY-MM-DDTHH:mm:ssZ or null"",
    ""type"": ""UserMemory or AgentInference"",
    ""confidence"": 0.0,
    ""tags"": [""string""],
    ""threadId"": ""[ThreadId]"",
    ""sourceAgent"": ""MemoryAgent"",
    ""associations"": [],
    ""semanticLinks"": []
  }}
]

Conversation:
[conversationJson]";
        // $"You are MemoryAgent. For Thread {this.CurrentThreadId}, output all memories learned in JSON format ready for AI Index: Include MemoryId (use {memoryId}), Title, Content, Summary, Associations (CrmId, CrmType, Role), Confidence (0-1), Tags, Timestamp, SourceAgent. Include references to {finalThreadUpdate}.";

        MemoryService _memoryService = null;
        EmbeddingService _embeddingService = null;
        public List<MemoryDocument> MemoriesNeedContextLoading { get; } = new();
        public List<MemoryDocument> MemoriesInContext { get; } = new();
        public virtual async Task LoadMemoryContext(List<MemoryDocument> memories)
        {
            InsertMemoriesContext(memories);
            await LoadMemoriesIntoContext();
        }

        public virtual async Task LoadMemoriesIntoContext()
        {
            if (MemoriesNeedContextLoading.Count > 0)
            {
                Warn($"Loading Memories ({MemoriesNeedContextLoading.Count}) into Context for use");

                MemoriesInContext.AddRange(MemoriesNeedContextLoading);
                MemoriesNeedContextLoading.Clear();
            }
            else
            {
                Warn("Context is up to date for Memories");
            }
        }

        public virtual void InsertMemoriesContext(List<MemoryDocument> memories)
        {
            foreach (MemoryDocument md in memories)
            {
                bool addMemory = true;

                foreach (MemoryDocument pending in MemoriesNeedContextLoading)
                {
                    if (md.MemoryId == pending.MemoryId)
                    {
                        addMemory = false;
                        break;
                    }
                }

                if (!addMemory)
                {
                    continue;
                }

                foreach (MemoryDocument loaded in MemoriesInContext)
                {
                    if (md.MemoryId == loaded.MemoryId)
                    {
                        addMemory = false;
                        break;
                    }
                }

                if (addMemory)
                {
                    MemoriesNeedContextLoading.Add(md);
                }
            }
        }

        public virtual void ClearLoadedMemoryContext()
        {
            MemoriesNeedContextLoading.Clear();
            MemoriesInContext.Clear();
            Log($"{AgentName} - Cleared loaded memory context.");
        }

        public MemoryService MemoryService
        {
            get
            {
                return _memoryService;
            }
            set
            {
                _memoryService = value;
            }
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

        public MemoryAgentSimple(HttpClient http, IConfiguration config) : 
            base(http, config, "gpt-4.1", "MemoryAgentSimple")
        {
            // specific logic
            base.AgentId = "n/a";
        }

        public override async Task<string> StartThreadAsync(bool ignoreStartPrompt = false)
        {
            string threadId = await base.StartThreadAsync(ignoreStartPrompt);

            string helloPrompt = 
$@"The user is {base.UserName}. 
Today is {DateTime.UtcNow.ToString("yyyy-MM-dd")}.
Say hello and greet the user by name!";
            Warn(helloPrompt);

            string response = await SendMessageAsync(new Message
            {
                role = "user",
                content = helloPrompt
            });

            return response;
        }
        public override async Task<string> ReasonAsync(string userInput)
        {
            // 1. Retrieve relevant memories
            MemoryServiceRequest request = new MemoryServiceRequest();
            request.Search = userInput;
            request.MemoryTypes = new List<MemoryType> { MemoryType.UserMemory, MemoryType.AgentInference };

            MemoryServiceResponse memoryContext = await this.MemoryService.GetMemoriesSemantic(request);
            this.InsertMemoriesContext(memoryContext.Memories);

            string memoryJson = JsonSerializer.Serialize(this.MemoriesNeedContextLoading);

            // 2. Build prompt
            bool hasMemories = this.MemoriesNeedContextLoading != null && this.MemoriesNeedContextLoading.Count > 0;
            string composedInput = userInput;
            if ( hasMemories)
            {
                string memoryText = string.Join("\n", this.MemoriesNeedContextLoading.Select(m => $"- {m.Title}: {m.Content}"));
                composedInput = $@"Relevant past memories: {memoryText}
                User input: {userInput}";
            }

            // 3. Send to agent
            string response = await SendMessageAsync(new Message
            {
                role = "user",
                content = composedInput
            });

            await this.LoadMemoriesIntoContext();

            this.UserInformation.Add(userInput);
            this.AgentResponses.Add(response);

            return response;
        }

        public override async Task EndThreadAsync(string reason = "manual")
        {
            // create the Memory in Dataverse
            Guid memoryId = Guid.NewGuid();
            bool success = false;

            var threadBundle = new
            {
                threadId = this.CurrentThreadId,
                userMessages = this.UserInformation,
                agentResponses = this.AgentResponses,
                timestamp = DateTime.UtcNow
            };
            string conversationJson = JsonSerializer.Serialize(threadBundle);
            string prompt = this.EndThreadPrompt.Replace("[conversationJson]", conversationJson);

            if (base.UserName != null)
            {
                prompt = prompt.Replace("[Username]", base.UserName);
            }

            try
            {
                var responseJson = await this.SendMessageAsync(prompt, true, false);

                this.Log("EndThread - " + responseJson);

                // Parse JSON to List<MemoryDocument>
                var memories = MemoryServiceResponse.FromJson(responseJson);

                this.Log("Established " + memories.Memories.Count + " to save.");
                // Ensure each memory has MemoryId and timestamp
                foreach (var mem in memories.Memories)
                {
                    mem.MemoryId = Guid.NewGuid().ToString();
                    mem.Timestamp = DateTime.UtcNow;

                    try
                    {
                        if (_embeddingService != null)
                            mem.EmbeddingVector = (await _embeddingService.GenerateQueryEmbeddingAsync(mem.Content)).Vector;
                    }
                    catch(Exception embeddingException)
                    {
                        Error("Memory(" + mem.MemoryId + ") - Embedding Error: " + embeddingException.Message);
                    }
                }

                try
                {
                    AISearchSaveResponse response = await _memoryService.SaveMemories(memories.Memories);
                    success = true;
                }
                catch(Exception saveException)
                {
                    Error("SaveMemories Error: " + saveException.Message);
                }
            }
            catch(Exception ex)
            {
                // error?
                this.Error("EndThread Error - " + ex.Message);
            }

            if ( success == true)
            {
                this.ClearLoadedMemoryContext();
                this.UserInformation.Clear();
                this.AgentResponses.Clear();
            }
        }
    }
}