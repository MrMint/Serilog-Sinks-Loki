using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Http;

namespace Serilog.Sinks.Loki
{
    public class LokiHttpClient : IHttpClient
    {
        protected readonly HttpClient HttpClient;

        public LokiHttpClient(HttpClient httpClient = null)
        {
            HttpClient = httpClient ?? new HttpClient();
        }

        public void SetAuthCredentials(LokiCredentials credentials)
        {
            if (!(credentials is BasicAuthCredentials c))
                return;

            var headers = HttpClient.DefaultRequestHeaders;
            if (headers.Any(x => x.Key == "Authorization"))
                return;

            var token = Base64Encode($"{c.Username}:{c.Password}");
            headers.Add("Authorization", $"Basic {token}");
        }

        public virtual Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
        {
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            return HttpClient.PostAsync(requestUri, content);
        }

        public virtual void Dispose()
            => HttpClient.Dispose();

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public void Configure(IConfiguration configuration)
        { }
    }
}