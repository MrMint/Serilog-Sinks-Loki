using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Perfolizer.Mathematics.Randomization;
using Serilog.Events;
using Serilog.Parsing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Serilog.Sinks.Loki.Benchmark
{
    public class LokiBatchFormatterBenchmark
    {
        private const int LOG_EVENT_COUNT = 1000;
        private const int LOG_LABEL_COUNT = 4;
        private const int LABEL_CARDINALITY = 10;
        private const int LOG_PROPERTY_COUNT = 7;

        private readonly IEnumerable<string> LabelNames;
        private readonly IEnumerable<LogEvent> logEvents;
        private readonly TextWriter textWriter;
        private readonly LokiBatchFormatter lokiBatchFormatter;

        public LokiBatchFormatterBenchmark()
        {
            this.textWriter = new StreamWriter(Stream.Null);

            var labelValues = Enumerable.Range(0, LABEL_CARDINALITY).Select(i => Guid.NewGuid().ToString()).ToArray();
            var random = new Random();

            var propertyNames = Enumerable.Range(0, LOG_PROPERTY_COUNT)
                .Select(index => Guid.NewGuid().ToString());

            this.LabelNames = propertyNames.Take(LOG_LABEL_COUNT);

            this.logEvents = Enumerable.Range(0, LOG_EVENT_COUNT).Select(index =>
            {
                return new LogEvent(
                    new DateTimeOffset(index, TimeSpan.Zero),
                    LogEventLevel.Debug,
                    null,
                    new MessageTemplate(Guid.NewGuid().ToString(), new List<MessageTemplateToken>()),
                    propertyNames.Select(name =>
                    new LogEventProperty(name, new ScalarValue(labelValues[random.Next(0, LABEL_CARDINALITY - 1)]))).ToList());
            }).ToList();

            this.lokiBatchFormatter = new LokiBatchFormatter(null, this.LabelNames, false);
        }

        [Benchmark]
        public void format() => this.lokiBatchFormatter.Format(this.logEvents, null, this.textWriter);
    }

    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<LokiBatchFormatterBenchmark>();
            Console.ReadLine();
        }
    }
}
