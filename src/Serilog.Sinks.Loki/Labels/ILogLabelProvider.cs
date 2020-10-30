using System.Collections.Generic;

namespace Serilog.Sinks.Loki.Labels
{
    public interface ILogLabelProvider
    {
        bool PreserveOriginalTimestamp { get; }
        IEnumerable<KeyValuePair<string, string>> GlobalLabels { get; }
        IEnumerable<string> LabelNames { get; }
    }
}