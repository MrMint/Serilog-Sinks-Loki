namespace Serilog.Sinks.Loki
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class LokiContent
    {
        [JsonPropertyName("streams")]
        public List<LokiContentStream> Streams { get; set; } = new List<LokiContentStream>();
    }
}