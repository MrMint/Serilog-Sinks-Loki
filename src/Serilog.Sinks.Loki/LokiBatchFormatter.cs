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
    using NodaTime;
    using System.Buffers;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.Json;

    internal class HashSetComparer : IEqualityComparer<HashSet<KeyValuePair<string, string>>>
    {
        public bool Equals(HashSet<KeyValuePair<string, string>> x, HashSet<KeyValuePair<string, string>> y)
        {
            return !x.Except(y).Any();
        }

        public int GetHashCode(HashSet<KeyValuePair<string, string>> obj)
        {
            var hash = 19;
            foreach(var pair in obj)
            {
                hash = hash * 31 + pair.Key.GetHashCode() + pair.Value.GetHashCode();
            }
            return hash;
        }
    }

    internal class LokiBatchFormatter : IBatchFormatter
    {
        private readonly IEnumerable<KeyValuePair<string, string>> _globalLabels;
        private readonly HashSet<string> _labelNames;

        public LokiBatchFormatter(IEnumerable<KeyValuePair<string, string>> globalLabels, IEnumerable<string> labelNames)
        {
            _globalLabels = globalLabels ?? new List<KeyValuePair<string, string>>();
            _labelNames = labelNames != null ? new HashSet<string>(labelNames) : new HashSet<string>();
        }

        // Currently supports https://github.com/grafana/loki/blob/master/docs/api.md#post-lokiapiv1push
        public void Format(IEnumerable<LogEvent> logEvents, ITextFormatter formatter, TextWriter output)
        {
            if (logEvents == null)
                throw new ArgumentNullException(nameof(logEvents));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            List<LogEvent> logs = logEvents.ToList();
            if (!logs.Any())
                return;

            // process labels for grouping/sorting
            var sortedStreams = logEvents
                .GroupBy(x => x.Properties
                    .Where(prop => _labelNames.Contains(prop.Key))
                    .Select(prop => new KeyValuePair<string, string>(prop.Key, prop.Value?.ToString()?.Replace("\"", "")?.Replace("\r\n", "\n")?.Replace("\\", "/")))
                    .Concat(new[] { new KeyValuePair<string, string>("level", GetLevel(x.Level)) })
                    .Concat(_globalLabels).ToHashSet(), new HashSetComparer())
                .Select(stream => new KeyValuePair<HashSet<KeyValuePair<string, string>>, IOrderedEnumerable<LogEvent>>(stream.Key, stream.OrderBy(log => log.Timestamp)));

            var logLineBuffer = new ArrayBufferWriter<byte>();
            using var logLineJsonWriter = new Utf8JsonWriter(logLineBuffer);
            var outputBuffer = new ArrayBufferWriter<byte>();
            using var jsonWriter = new Utf8JsonWriter(outputBuffer);

            jsonWriter.WriteStartObject();
            jsonWriter.WriteStartArray("streams");

            foreach (var stream in sortedStreams)
            {
                jsonWriter.WriteStartObject(); 
                jsonWriter.WriteStartObject("stream");

                foreach (var label in stream.Key)
                {
                    jsonWriter.WriteString(label.Key, label.Value);
                }

                jsonWriter.WriteEndObject();

                jsonWriter.WriteStartArray("values");

                foreach (var logEvent in stream.Value)
                {
                    jsonWriter.WriteStartArray();
                    jsonWriter.WriteStringValue((Instant.FromDateTimeOffset(logEvent.Timestamp).ToUnixTimeTicks() * 100).ToString());

                    // Construct a json object for the log line
                    logLineJsonWriter.WriteStartObject();
                    logLineJsonWriter.WriteString("message", logEvent.RenderMessage());

                    foreach (var property in logEvent.Properties)
                    {
                    // Some enrichers pass strings with quotes surrounding the values inside the string,
                    // which results in redundant quotes after serialization and a "bad request" response.
                    // To avoid this, remove all quotes from the value.
                    // We also remove any \r\n newlines and replace with \n new lines to prevent "bad request" responses
                    // We also remove backslashes and replace with forward slashes, Loki doesn't like those either
                    logLineJsonWriter.WriteString(property.Key, property.Value?.ToString()?.Replace("\"", "")?.Replace("\r\n", "\n")?.Replace("\\", "/"));
                    }

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
                        logLineJsonWriter.WriteString("exception", sb?.ToString());
                    }

                    logLineJsonWriter.WriteEndObject();
                    logLineJsonWriter.Flush();
                    jsonWriter.WriteStringValue(Encoding.UTF8.GetString(logLineBuffer.WrittenSpan));
                    jsonWriter.WriteEndArray();
                    logLineJsonWriter.Reset();
                    logLineBuffer.Clear();
                }

                jsonWriter.WriteEndArray();
                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndArray();
            jsonWriter.WriteEndObject();
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