using System.Collections.Generic;
using Serilog.Sinks.Loki.Labels;

namespace Serilog.Sinks.Loki.Tests.Infrastructure
{
    public class TestLabelProvider : ILogLabelProvider
    {
        public IList<LokiLabel> GetLabels()
        {
            return new List<LokiLabel>
            {
                new LokiLabel("app", "tests")
            };
        }

        public IEnumerable<string> LabelNames()
        {
            return new HashSet<string>
            {
                "app", "level"
            };
        }

    }
}