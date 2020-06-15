using System.Collections.Generic;

namespace Serilog.Sinks.Loki.Labels
{
    public interface ILogLabelProvider
    {
        IList<LokiLabel> GlobalLabels { get;  }
        IEnumerable<string> LabelNames { get; }
    }
}