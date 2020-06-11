using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Http;
using Serilog.Sinks.Loki.Labels;

namespace Serilog.Sinks.Loki
{
    using System.Buffers;
    using System.Text;
    using System.Text.Json;

    internal class LokiBatchFormatter : IBatchFormatter 
    {
        private readonly IList<LokiLabel> _globalLabels;
        private readonly HashSet<string> _labelWhiteList;

        public LokiBatchFormatter()
        {
            _globalLabels = new List<LokiLabel>();
        }

        public LokiBatchFormatter(IList<LokiLabel> globalLabels, IEnumerable<string> labelWhiteList)
        {
            _globalLabels = globalLabels ?? new List<LokiLabel>();
            _labelWhiteList = labelWhiteList != null ? new HashSet<string>(labelWhiteList) : null;
        }

        public void Format(IEnumerable<LogEvent> logEvents, ITextFormatter formatter, TextWriter output)
        {
            if (logEvents == null)
                throw new ArgumentNullException(nameof(logEvents));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            List<LogEvent> logs = logEvents.ToList();
            if (!logs.Any())
                return;

            var buffer = new ArrayBufferWriter<byte>();
            using var jsonWriter = new Utf8JsonWriter(buffer);
            var content = new LokiContent();
            foreach (LogEvent logEvent in logs)
            {
                var stream = new LokiContentStream();
                content.Streams.Add(stream);

                stream.Labels.Add(new LokiLabel("level", GetLevel(logEvent.Level)));
                foreach (LokiLabel globalLabel in _globalLabels)
                    stream.Labels.Add(new LokiLabel(globalLabel.Key, globalLabel.Value));
                var labels = logEvent.Properties.Where(x => _labelWhiteList.Contains(x.Key));

                foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
                {
                    if (_labelWhiteList.Contains(property.Key))
                    {
                        // Some enrichers pass strings with quotes surrounding the values inside the string,
                        // which results in redundant quotes after serialization and a "bad request" response.
                        // To avoid this, remove all quotes from the value.
                        // We also remove any \r\n newlines and replace with \n new lines to prevent "bad request" responses
                        // We also remove backslashes and replace with forward slashes, Loki doesn't like those either
                        stream.Labels.Add(new LokiLabel(property.Key, property.Value.ToString().Replace("\"", "").Replace("\r\n", "\n").Replace("\\", "/")));
                    } else
                    {
                        jsonWriter.WriteString(property.Key, property.Value.ToString().Replace("\"", ""));
                    }
                }

                var localTime = DateTime.Now;
                var localTimeAndOffset = new DateTimeOffset(localTime, TimeZoneInfo.Local.GetUtcOffset(localTime));
                var time = localTimeAndOffset.ToString("o");
                
                jsonWriter.WriteString("message", logEvent.RenderMessage());

                if (logEvent.Exception != null)
                {
                    var sb = new StringBuilder();
                    var e = logEvent.Exception;
                    while (e != null)
                    {
                        sb.AppendLine(e.Message);
                        sb.AppendLine(e.StackTrace);
                        e = e.InnerException;
                    }
                    jsonWriter.WriteString("exception", sb.ToString());
                }
                jsonWriter.Flush();
                
                stream.Entries.Add(new LokiEntry(time, Encoding.UTF8.GetString(buffer.WrittenSpan)));
                buffer.Clear();
                jsonWriter.Reset();
            }

            if (content.Streams.Count > 0)
                output.Write(JsonSerializer.Serialize(content));
        }

        public void Format(IEnumerable<string> logEvents, TextWriter output)
        {
            throw new NotImplementedException();
        }

        private static string GetLevel(LogEventLevel level)
        {
            if (level == LogEventLevel.Information)
                return "info";

            return level.ToString().ToLower();
        }
    }
}