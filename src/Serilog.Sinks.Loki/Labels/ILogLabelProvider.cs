using System.Collections.Generic;

namespace Serilog.Sinks.Loki.Labels
{
    public interface ILogLabelProvider
    {
        IEnumerable<KeyValuePair<string, string>> GlobalLabels { get;  }
        IEnumerable<string> LabelNames { get; }
    }
}