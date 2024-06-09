using System.Web.Http;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Services;
using Google.Apis.Sheets.v4.Data;
using System.Collections.Generic;
using System;
using System.Linq;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Web;
using Newtonsoft.Json.Linq;

public class EthicsController : ApiController
{
    // Google Sheets API configuration
    private static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
    private static readonly string ApplicationName = "half-life-dejargonizer";
    private static readonly string SpreadsheetId = "1HbveKqUSjnmplmE7QQCggQN07ZR0GHzhMa3NZ58qfsI";
    private static readonly string sheet = "results";

    private readonly HttpClient client;
    private readonly string apiUrl = "https://api.openai.com/v1/engines/gpt-3.5-turbo-instruct/completions";
    private readonly string apiKey = "openapi-secret";  // Replace 'openapi-secret' with your actual API key

    readonly Dictionary<int, (int min, int max)> wordCountRanges = new Dictionary<int, (int min, int max)>
    {
        { 120, (100, 140) },
        { 3, (2, 5) },
        { 10, (2, 30) },
        { 20, (15, 30) },
    };

    public class ConversationHistory
    {
        public List<Message> Messages { get; set; }
        public int CurrentStage { get; set; }
        public string OriginalText { get; set; }
        public string MitigateText { get; set; }
        public string FinalText { get; set; }
        public string WhichIsBetter { get; set; }
        public string WhyIsBetter { get; set; }
        public string AdditionalInformation { get; set; }
        public string TargetAudience { get; set; }
    }

    public class Message
    {
        public string Text { get; set; }
        public bool IsStudent { get; set; }  // Indicates the current stage of the conversation
    }

    public EthicsController()
    {
        var httpClientHandler = new HttpClientHandler();
        httpClientHandler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
        httpClientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => { return true; };

        client = new HttpClient(httpClientHandler);
        client.DefaultRequestHeaders.ConnectionClose = false;
    }


    [HttpPost]
    public async Task<IHttpActionResult> ProcessConversation([FromBody] ConversationHistory history)
    {
        try
        {
            var responseMessages = await DetermineResponse(history);

            // Update history with the response
            history.Messages.AddRange(responseMessages.Select(m => new Message { Text = m, IsStudent = false }));

            return Ok(new { Messages = responseMessages, UpdatedHistory = history });
        }
        catch (Exception ex)
        {
            return InternalServerError(ex);
        }
    }



    private async Task<List<string>> DetermineResponse(ConversationHistory history)
    {
        var lastUserText = history.Messages.LastOrDefault(x => x.IsStudent);

        switch (history.CurrentStage)
        {
            case 1:
                var response = ValidateWordCount(lastUserText.Text, 20);

                if (!string.IsNullOrEmpty(response))
                {
                    return new List<string> { response };
                }

                history.CurrentStage++;
                history.OriginalText = lastUserText.Text;

                return new List<string> {
                    "Take your above description and add 1-2 potential ways to mitigate the issue. Your text should be 120 words.",
                };

            case 2:
                var response2 = ValidateWordCount(lastUserText.Text, 120);

                if (!string.IsNullOrEmpty(response2))
                {
                    return new List<string> { response2 };
                }

                history.CurrentStage++;
                history.MitigateText = lastUserText.Text;

                var rephrasedText = await RephraseText(lastUserText.Text);
                var gptSuggestion = "Looking at the above suggestions and your texts, please create a final 120-word version of the research aim, ethical issue and potential solution. New/revised version:";

                return new List<string> {
                    rephrasedText,
                    gptSuggestion,
                };

            case 3:
                var response3 = ValidateWordCount(lastUserText.Text, 120);

                if (!string.IsNullOrEmpty(response3))
                {
                    return new List<string> { response3 };
                }

                history.CurrentStage++;
                history.FinalText = lastUserText.Text;

                return new List<string> {
                    "Which version is better in your option?" +
                    "<div class='chat-option'>(1) The original 120 word summary </div>" +
                    "<div class='chat-option'>(2) The final 120 word summary </div>" +
                    "<div class='chat-option'>(3) The texts are equal</div>",
                };
            case 4:
                if (ValidateUserRespose(lastUserText.Text, new List<string> { "1", "2", "3" }))
                {
                    history.CurrentStage++;

                    switch (lastUserText.Text)
                    {
                        case "1":
                            history.WhichIsBetter = "Original";
                            break;
                        case "2":
                            history.WhichIsBetter = "Final";
                            break;
                        case "3":
                            history.WhichIsBetter = "Equal";
                            break;
                    }

                    return new List<string>
                    {
                        "Why is the version you chose better? (1-3 sentences)"
                    };
                }
                else
                {
                    return new List<string> {
                        "Please enter your answer as single digit:" +
                        "<div class='chat-option'>(1) The original 120 word summary </div>" +
                        "<div class='chat-option'>(2) The final 120 word summary </div>" +
                        "<div class='chat-option'>(3) The texts are equal</div>"
                    };
                }

            case 5:
                var response7 = ValidateWordCount(lastUserText.Text, 10);

                if (!string.IsNullOrEmpty(response7))
                {
                    return new List<string> { response7 };
                }

                history.CurrentStage++;
                history.WhyIsBetter = lastUserText.Text;

                return new List<string> {
                    "Following this task, what information would you take care to add to your research summary next time you write?",
                };

            case 6:
                var response8 = ValidateWordCount(lastUserText.Text, 10);

                if (!string.IsNullOrEmpty(response8))
                {
                    return new List<string> { response8 };
                }

                history.CurrentStage++;
                history.AdditionalInformation = lastUserText.Text;

                return new List<string> {
                    "We hope you have learned more on persuasive writing!",
                    "We are conducting research on academic writing, abd we would appreciate it if you would give us your consent to use your " +
                    "writing outcomes to assess how people write abd use this tool. We will not share the content of your writing, just evaluate it." +
                    "<div class='chat-option'>(1) I give my consent </div>" +
                    "<div class='chat-option'>(2) I do not give my consent</div>",
                };
            case 7:
                if (ValidateUserRespose(lastUserText.Text, new List<string> { "1", "2" }))
                {
                    history.CurrentStage++;

                    if (lastUserText.Text == "1")
                    {
                        await SaveToGoogleSheets(history);
                    }

                    return new List<string> { "Thank you!!" };
                }
                else
                {
                    return new List<string> {
                        "Please enter your answer as single digit:" +
                        "<div class='chat-option'>(1) I give my consent</div>" +
                        "<div class='chat-option'>(2) I do not give my consent</div>"
                    };
                }

            default:
                history.CurrentStage++;

                return new List<string> {
                    "Think about your research project and an ethical issue that may arise when you want to present this work to the public/stakeholders/academics:",
                    "Briefly describe the research aim and an ethical issue of that project in 2-3 sentences:",
                };
        }
    }

    private string ValidateWordCount(string text, int wordLimit)
    {
        int wordCount = text.Split(new char[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        Debug.WriteLine($"text: " + text);

        if (wordCount < wordCountRanges[wordLimit].min || wordCount > wordCountRanges[wordLimit].max)
        {
            return $"Please use {wordCountRanges[wordLimit].min}-{wordCountRanges[wordLimit].max} words.<br />Currently, you used {wordCount}.";
        }
        return null; // No error, proceed with normal flow
    }

    private bool ValidateUserRespose(string lastUserText, List<string> possibleResponses)
    {
        return possibleResponses.Any(r => r == lastUserText.Trim());
    }

    private async Task SaveToGoogleSheets(ConversationHistory history)
    {
        try
        {
            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = GetCredentials(),
                ApplicationName = ApplicationName,
            });

            var range = $"{sheet}!A:M";
            var valueRange = new ValueRange();

            string ip = GetIp();
            var geoInfo = await GetGeoInfoFromIp(ip);

            var objectList = new List<object>() { DateTime.Now.Date.ToShortDateString(), DateTime.Now.TimeOfDay, ip, geoInfo.Country, geoInfo.Region, geoInfo.City,
                    history.OriginalText, history.MitigateText, history.FinalText, history.WhichIsBetter, history.WhyIsBetter, history.AdditionalInformation, history.TargetAudience,  };

            valueRange.Values = new List<IList<object>> { objectList };

            var appendRequest = service.Spreadsheets.Values.Append(valueRange, SpreadsheetId, range);
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            var appendResponse = appendRequest.Execute();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public string GetIp()
    {
        string ip = HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
        string ip2 = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
        string ip3 = HttpContext.Current.Request.UserHostAddress;

        //Debug.WriteLine($"ip:{ip}, ip2:{ip2}, ip3:{ip3}");

        if (string.IsNullOrEmpty(ip))
        {
            ip = ip2;
        }

        if (string.IsNullOrEmpty(ip))
        {
            ip = ip3;
        }

        return ip;
    }

    private async Task<(string Country, string Region, string City)> GetGeoInfoFromIp(string ip)
    {
        using (var client = new HttpClient())
        {
            var response = await client.GetStringAsync($"http://ip-api.com/json/{ip}?fields=status,country,regionName,city");
            var json = JObject.Parse(response);

            if (json["status"].ToString() == "success")
            {
                string country = json["country"].ToString();

                string region = json["regionName"].ToString();
                string city = json["city"].ToString();
                return (country, region, city);
            }
            else
            {
                // Handle failure response
                return ("Unknown", "Unknown", "Unknown");
            }
        }
    }

    private GoogleCredential GetCredentials()
    {
        string path = HttpContext.Current.Server.MapPath(@"~\half-life-dejargonizer-f4e89fe2bc96.json");

        GoogleCredential credential = GoogleCredential.FromFile(path).CreateScoped(Scopes);
        return credential;
    }


    private async Task<string> RephraseText(string text)
    {
        string prompt = $"Here is a brief academic description of a research project and an ethical issue that may arise: {text}. What other questions or issues might arise in such a project?";

        HttpRequestMessage request = CreatePostRequest(prompt);
        HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        string jsonResponse = await response.Content.ReadAsStringAsync();

        return ExtractText(jsonResponse);
    }

    private HttpRequestMessage CreatePostRequest(string prompt)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        var payload = new
        {
            prompt = prompt,
            temperature = 0.7,
            max_tokens = 150,
            top_p = 1.0,
            frequency_penalty = 0.0,
            presence_penalty = 0.0
        };

        string jsonContent = JsonConvert.SerializeObject(payload);
        request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        return request;
    }

    private string ExtractText(string jsonResponse)
    {
        var response = JsonConvert.DeserializeAnonymousType(jsonResponse, new
        {
            choices = new[] {
                new { text = "" }
            }
        });

        if (response != null && response.choices.Length > 0)
        {
            return response.choices[0].text.Trim();
        }
        return string.Empty;
    }
}
