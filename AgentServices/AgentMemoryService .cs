using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using FoundryAgentBridge.AgentServices;

namespace FoundryAgentBridge.Services
{
    namespace FoundryAgentBridge.Services
    {
        /// <summary>
        /// Persistent Foundry agent with memory/index support.
        /// Inherits all normal AgentService behavior and adds:
        /// - memory retrieval from MemoryService
        /// - memory writes via MemoryService
        /// - memory context queueing for agent use
        /// </summary>
        public class AgentMemoryService : AgentService
        {
            private MemoryService _memoryService = null;

            public List<MemoryDocument> MemoriesNeedContextLoading { get; } = new();
            public List<MemoryDocument> MemoriesInContext { get; } = new();

            public MemoryService MemoryService
            {
                get => _memoryService;
                set => _memoryService = value;
            }

            public AgentMemoryService(
                AgentsService agentsService,
                string agentId,
                string agentName)
                : base(agentsService, agentId, agentName)
            {
            }

            public AgentMemoryService(HttpClient http, IConfiguration config)
                : base(http, config)
            {
            }

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
        }
    }
}
