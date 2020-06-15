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
        private readonly HashSet<string> _labelNames;

        public LokiBatchFormatter()
        {
            _globalLabels = new List<LokiLabel>();
        }

        public LokiBatchFormatter(IList<LokiLabel> globalLabels, IEnumerable<string> labelNames)
        {
            _globalLabels = globalLabels ?? new List<LokiLabel>();
            _labelNames = labelNames != null ? new HashSet<string>(labelNames) : new HashSet<string>();
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

            var outputBuffer = new ArrayBufferWriter<byte>();
            using var jsonWriter = new Utf8JsonWriter(outputBuffer);

            var logLineBuffer = new ArrayBufferWriter<byte>();
            using var logLineJsonWriter = new Utf8JsonWriter(logLineBuffer);

            jsonWriter.WriteStartObject();
            jsonWriter.WriteStartArray("streams");

            foreach (LogEvent logEvent in logs)
            {
                jsonWriter.WriteStartObject();

                jsonWriter.WriteStartObject("stream");
                jsonWriter.WriteString("level", GetLevel(logEvent.Level));

                foreach (LokiLabel globalLabel in _globalLabels)
                {
                    jsonWriter.WriteString(globalLabel.Key, globalLabel.Value);
                }
                foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties.Where(x => _labelNames.Contains(x.Key)))
                {
                    jsonWriter.WriteString(property.Key, property.Value.ToString().Replace("\"", "").Replace("\r\n", "\n").Replace("\\", "/"));
                }

                jsonWriter.WriteEndObject();

                jsonWriter.WriteStartArray("values");
                jsonWriter.WriteStartArray();

                var localTime = DateTime.Now;
                var localTimeAndOffset = new DateTimeOffset(localTime, TimeZoneInfo.Local.GetUtcOffset(localTime));
                var time = localTimeAndOffset.ToString("o");
                jsonWriter.WriteStringValue(time);

                logLineJsonWriter.WriteStartObject();
                foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties.Where(x => !_labelNames.Contains(x.Key)))
                {
                    // Some enrichers pass strings with quotes surrounding the values inside the string,
                    // which results in redundant quotes after serialization and a "bad request" response.
                    // To avoid this, remove all quotes from the value.
                    // We also remove any \r\n newlines and replace with \n new lines to prevent "bad request" responses
                    // We also remove backslashes and replace with forward slashes, Loki doesn't like those either
                    logLineJsonWriter.WriteString(property.Key, property.Value.ToString().Replace("\"", "").Replace("\r\n", "\n").Replace("\\", "/"));
                }

                logLineJsonWriter.WriteString("message", logEvent.RenderMessage());

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
                    logLineJsonWriter.WriteString("exception", sb.ToString());
                }
                logLineJsonWriter.WriteEndObject();
                logLineJsonWriter.Flush();

                jsonWriter.WriteStringValue(Encoding.UTF8.GetString(logLineBuffer.WrittenSpan));
                jsonWriter.WriteEndArray();
                jsonWriter.WriteEndArray();
                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndArray();
            jsonWriter.Flush();

            output.Write(Encoding.UTF8.GetString(outputBuffer.WrittenSpan));
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