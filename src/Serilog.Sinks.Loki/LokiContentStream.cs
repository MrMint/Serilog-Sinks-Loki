namespace Serilog.Sinks.Loki
{
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json.Serialization;
    using Labels;

    internal class LokiContentStream
    {
        [JsonIgnore]
        public List<LokiLabel> Labels { get; } = new List<LokiLabel>();
            
        [JsonPropertyName("labels")]
        public string LabelsString {
            get
            {
                StringBuilder sb = new StringBuilder("{");
                bool firstLabel = true;
                foreach (LokiLabel label in Labels)
                {
                    if (firstLabel)
                        firstLabel = false;
                    else
                        sb.Append(",");

                    sb.Append(label.Key);
                    sb.Append("=\"");
                    sb.Append(label.Value);
                    sb.Append("\"");
                }
        
                sb.Append("}");
                return sb.ToString();
            } 
        }
            
        
        [JsonPropertyName("entries")]
        public List<LokiEntry> Entries { get; set; } = new List<LokiEntry>();
    }
}