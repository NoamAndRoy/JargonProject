using Newtonsoft.Json;

namespace DeJargonizer2025.Helpers
{
    public class GPTApiClient
    {
        private readonly HttpClient _client;

        public GPTApiClient(IHttpClientFactory httpClientFactory)
        {
            _client = httpClientFactory.CreateClient("CustomClient");
        }
        public async Task<string> RephraseText(string prompt)
        {
            HttpRequestMessage request = CreatePostRequest(prompt);
            HttpResponseMessage response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();

            return ExtractText(jsonResponse);
        }
        private HttpRequestMessage CreatePostRequest(string userInput)
        {
            string apiUrl = Environment.GetEnvironmentVariable("OPENAI_API_URL");
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            string model = Environment.GetEnvironmentVariable("OPENAI_API_MODEL");

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("Accept", "application/json");

            var payload = new
            {
                model = model,
                input = new[]
                {
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new
                            {
                                type = "input_text",
                                text = userInput
                            }
                        }
                    }
                },
                text = new
                {
                    format = new
                    {
                        type = "text"
                    }
                },
                reasoning = new
                {
                    effort = "medium"
                },
                max_output_tokens = 1000,
                tools = new object[] { }, // Empty array if not using tools
                store = false
            };

            string jsonContent = JsonConvert.SerializeObject(payload);
            request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            return request;
        }

        private string ExtractText(string jsonResponse)
        {
            var response = JsonConvert.DeserializeAnonymousType(jsonResponse, new
            {
                output = new[]
                {
            new
            {
                type = "",
                content = new[]
                {
                    new
                    {
                        type = "",
                        text = ""
                    }
                }
            }
        }
            });

            if (response?.output != null)
            {
                var message = response.output
                    .FirstOrDefault(o => o.type == "message");

                var outputText = message?.content?
                    .FirstOrDefault(c => c.type == "output_text")?.text;

                return outputText?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
