using System.Collections.Generic;
using Serilog.Sinks.Loki.Labels;

namespace Serilog.Sinks.Loki.Tests.Infrastructure
{
    public class TestLabelProvider : ILogLabelProvider
    {
        public IEnumerable<KeyValuePair<string, string>> GlobalLabels => new[]
            {
                new KeyValuePair<string, string>("app", "tests"),
            };

        IEnumerable<string> ILogLabelProvider.LabelNames =>
            new HashSet<string>
            {
                "app", "level"
            };
    }
}