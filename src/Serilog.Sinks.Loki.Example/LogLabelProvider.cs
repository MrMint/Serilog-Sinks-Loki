using System.Collections.Generic;
using Serilog.Sinks.Loki.Labels;

namespace Serilog.Sinks.Loki.Example
{
    public class LogLabelProvider : ILogLabelProvider
    {
        public IEnumerable<KeyValuePair<string, string>> GlobalLabels => new[]
            {
                new KeyValuePair<string, string>("app", "demo"),
                new KeyValuePair<string, string>("namespace", "prod"),
            };

        IEnumerable<string> ILogLabelProvider.LabelNames =>
            new HashSet<string>
            {
                "actorId"
            };
    }
}