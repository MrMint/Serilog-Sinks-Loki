using System.Collections.Generic;

namespace Serilog.Sinks.Loki.Labels
{
    public interface ILogLabelProvider
    {
        IList<LokiLabel> GetLabels();
        IEnumerable<string> GetLabelWhiteList();
    }
}