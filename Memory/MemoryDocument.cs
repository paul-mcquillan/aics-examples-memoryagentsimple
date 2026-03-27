using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICS.Examples.Memory
{
    public enum MemoryType
    {
        UserMemory,
        AgentInference
    }

    // Define your MemoryDocument class tfgbdo match AI Index schema
    public class MemoryDocument
    {
        public string MemoryId { get; set; }                    // Unique identifier for the memory
        public string Title { get; set; }                       // Short descriptive label
        public string Content { get; set; }                     // Full statement of the memory
        public string Summary { get; set; }                     // Optional one-line description
        public DateTime? MemoryDate { get; set; }              // Date the memory/event actually occurred (nullable)
        public DateTime Timestamp { get; set; }                // When the memory was recorded
        public string Type { get; set; }                   // UserMemory or AgentInference
        public double? Confidence { get; set; }                 // Confidence score (0-1)
        public List<string> Tags { get; set; } = new List<string>();
        public List<Association> Associations { get; set; } = new List<Association>();
        public List<string> SemanticLinks { get; set; } = new List<string>(); // References to related topics/entities
        public string ThreadId { get; set; }                   // Thread in which memory was created
        public string SourceAgent { get; set; }                // Name of the agent producing the memory
        public List<float> EmbeddingVector { get; set; } = new List<float>(); // Vector for AI semantic search
    }

    public class Association
    {
        public Association(string crmid, string crmtype)
        {
            CrmId = crmid;
            CrmType = crmtype;
        }

        public string CrmId { get; set; }
        public string CrmType { get; set; }
        public string Role { get; set; }
    }
}
