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
using JargonProject.Handlers;
using JargonProject.Models;
using System.Diagnostics;
using System.Web;
using Newtonsoft.Json.Linq;

public class HalfLifeController : ApiController
{
    // Google Sheets API configuration
    private static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
    private static readonly string ApplicationName = "half-life-dejargonizer";
    private static readonly string SpreadsheetId = "1ODVU-XWBxN6Ds6u8z33623EhymQmsnkOoTIZI4ACEBQ";
    private static readonly string sheet = "results";

    private readonly HttpClient client;
    private readonly string apiUrl = "https://api.openai.com/v1/engines/gpt-3.5-turbo-instruct/completions";
    private readonly string apiKey = "<openapi-secret>";  // Replace 'openapi-secret' with your actual API key

    readonly Dictionary<int, (int min, int max)> wordCountRanges = new Dictionary<int, (int min, int max)>
    {
        { 120, (100, 140) },
        { 60, (45, 75) },
        { 30, (20, 40) }
    };

    public class ConversationHistory
    {
        public List<Message> Messages { get; set; }
        public int CurrentStage { get; set; }
        public string Text120First { get; set; }
        public string Text120FirstJargon { get; set; }
        public string Text120FirstGPT3 { get; set; }
        public string Text60 { get; set; }
        public string Text60Jargon { get; set; }
        public string Text60GPT3 { get; set; }
        public string Text30 { get; set; }
        public string Text30Jargon { get; set; }
        public string Text30GPT3 { get; set; }
        public string Text120Last { get; set; }
        public string WhichIsBetter { get; set; }
        public string TargetAudience { get; set; }
    }

    public class Message
    {
        public string Text { get; set; }
        public bool IsStudent { get; set; }  // Indicates the current stage of the conversation
    }

    public HalfLifeController()
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

                if (ValidateUserRespose(lastUserText.Text, new List<string> { "1", "2", "3", "4" }))
                {
                    history.CurrentStage++;

                    switch (lastUserText.Text)
                    {
                        case "1":
                            history.TargetAudience = "Elementary";
                            break;
                        case "2":
                            history.TargetAudience = "Junior";
                            break;
                        case "3":
                            history.TargetAudience = "High";
                            break;
                        case "4":
                            history.TargetAudience = "Adult";
                            break;
                    }

                    return new List<string>
                    {
                        "Please tell me what you study and why it is important.<br />Use about 120 words (equivalent to about one minute of speech)",
                        "OK - set, go! Tell me what you do in 120 words or so (100-140)"
                    };
                }
                else
                {
                    return new List<string> { "Please enter your answer as single digit: " +
                        "<div class='chat-option'>(1) Elementary (primary) school level science (learned science until they were 12 years old)</div>" +
                        "<div class='chat-option'>(2) Junior high school level science (learned science until they were 15 years old)</div>" +
                        "<div class='chat-option'>(3) High school level science (learned science until they were 18 years old)</div>" +
                        "<div class='chat-option'>(4) Adult audience with mixed background</div>" };
                }
            case 2: // Welcome message
                return await ProcessStageAsync(history, 120);
            case 3: // First 120 words
                return await ProcessStageAsync(history, 60);
            case 4: // 60 words
                return await ProcessStageAsync(history, 30);
            case 5: // 30 words
                return await ProcessStageAsync(history, 120, true); // Second round of 120 words
            case 6:
                if (ValidateUserRespose(lastUserText.Text, new List<string> { "1", "2", "3", "4" }))
                {
                    switch (lastUserText.Text)
                    {
                        case "1":
                            history.WhichIsBetter = "Original";
                            break;
                        case "2":
                            history.WhichIsBetter = "Revised";
                            break;
                        case "3":
                            history.WhichIsBetter = "Neither";
                            break;
                        case "4":
                            history.WhichIsBetter = "Both";
                            break;
                    }

                    history.CurrentStage++;

                    return new List<string> {
                        "We are science communication researchers. If you agree, we will save the conversation you had with the bot for a future study (the text, today’s date, and country of origin by IP). If you don’t agree, we won’t save it." +
                        "<div class='chat-option'>(1) You can save my conversation</div>" +
                        "<div class='chat-option'>(2) Don’t save my conversation</div>",
                    };
                }
                else
                {
                    return new List<string> { "Please enter your answer as single digit: " +
                        "<div class='chat-option'>(1) My original text</div>" +
                        "<div class='chat-option'>(2) My revised text</div>" +
                        "<div class='chat-option'>(3) Neither</div>" +
                        "<div class='chat-option'>(4) Both!</div>"};
                }

            case 7:
                if (ValidateUserRespose(lastUserText.Text, new List<string> { "1", "2" }))
                {
                    history.CurrentStage++;

                    if (lastUserText.Text == "1")
                    {
                        await SaveToGoogleSheets(history);
                    }

                    return new List<string> { "We’re done! Hope this was helpful. Now is a good time to further hone your skills at the science communication free online course at edX." };
                }
                else
                {
                    return new List<string> { "Please enter your answer as single digit: " +
                        "<div class='chat-option'>(1) You can save my conversation</div>" +
                        "<div class='chat-option'>(2) Don’t save my conversation</div>" };
                }
            default:
                history.CurrentStage++;

                return new List<string> {
                    "Welcome to the Half-Life writing exercise! We're glad you're here.",
                    "Before we dive into the writing process, could you please provide some insight into the science background of your intended audience? This will assist the AI in tailoring appropriate suggestions for your task:" +
                    "<div class='chat-option'>(1)Elementary (primary) school level science (learned science until they were 12 years old)</div>" +
                    "<div class='chat-option'>(2)Junior high school level science (learned science until they were 15 years old)</div>" +
                    "<div class='chat-option'>(3)High school level science (learned science until they were 18 years old)</div>" +
                    "<div class='chat-option'>(4)Adult audience with mixed background</div>",
                };
        }
    }

    private async Task<List<string>> ProcessStageAsync(ConversationHistory history, int wordLimit, bool lastRound = false)
    {
        var text = history.Messages.LastOrDefault(x => x.IsStudent).Text;
        var response = ValidateWordCount(text, wordLimit);

        if (!string.IsNullOrEmpty(response))
        {
            return new List<string> { response };
        }

        history.CurrentStage++;

        TextGrading.Lang = Language.English2020_2023;
        var articleGradingInfo = TextGrading.AnalyzeSingleText(text.Trim());

        var scoreFeedback = $"Your suitability for a general audience score was {articleGradingInfo.Score}. <br />{(articleGradingInfo.Score > 90 ? "Nicely done!" : "Try to get it higher in the next round.")}";

        var gptSuggestion = "In your next version, you may want to consider the following phrasing suggested by the AI tool GTP:";
        var rephrasedText = await RephraseText(text, history.TargetAudience);

        var responses = new List<string>
        {
            scoreFeedback,
            gptSuggestion,
            rephrasedText,
        };

        if (articleGradingInfo.RareWordsSyns.Count > 0)
        {
            var wordsWithSyns = string.Join(", ",
                articleGradingInfo.RareWordsSyns.Keys.Select(w =>
                    $"<span class=\"rare-word\" title=\"Optional replacemets: {GenerateReplacementSyns(articleGradingInfo, w)}.\">{w}</span>"
                )
            );

            var jargonFeedback = $"That was interesting, but notice that you used the {(articleGradingInfo.RareWordsSyns.Count > 1 ? "words" : "word")}: {wordsWithSyns} that some of your audience might not understand.<br />Consider replacing or explaining some of them next time.";

            responses.Insert(0, jargonFeedback);
        }


        switch (wordLimit)
        {
            case 120:
                if (!lastRound)
                {
                    responses.Add("Now please tell me again what you do and why it is important - but this time use only 60 words! (45-75)");

                    history.Text120First = text;
                    history.Text120FirstGPT3 = rephrasedText;
                    history.Text120FirstJargon = string.Join(", ", articleGradingInfo.RareWordsSyns.Keys);
                }
                else
                {
                    var originalTxt = history.Text120First;
                    var revisedTxt = text;

                    history.Text120Last = text;

                    return new List<string> {
                        "Let’s see if this was effective.",
                        "This is your original text:",
                        originalTxt,
                        "And this is your revised text:",
                        revisedTxt,
                        "Which one do you think is better suited for a general audience? " +
                        "<div class='chat-option'>(1) My original text</div>" +
                        "<div class='chat-option'>(2) My revised text</div>" +
                        "<div class='chat-option'>(3) Neither</div>" +
                        "<div class='chat-option'>(4) Both!</div>"
                    };
                }
                break;
            case 60:
                history.Text60 = text;
                history.Text60GPT3 = rephrasedText;
                history.Text60Jargon = string.Join(", ", articleGradingInfo.RareWordsSyns.Keys);

                responses.Add("OK, now let's take it to the next level!<br />Please tell me what you do and why in only 30 words (20-40).");
                break;
            case 30:
                history.Text30 = text;
                history.Text30GPT3 = rephrasedText;
                history.Text30Jargon = string.Join(", ", articleGradingInfo.RareWordsSyns.Keys);

                responses.Add("Finally, after you distilled your message and noticed some jargon words and difficult phrases you might wish to avoid - I give you all of your 120 words back! I bet it seems a lot now.<br />Please tell me what you study and why it is important using 120 words.");
                break;
        }

        return responses;
    }

    private string GenerateReplacementSyns(ArticleGradingInfo articleGradingInfo, string word)
    {
        if (articleGradingInfo.RareWordsSyns[word].Count == 0)
        {
            return "No replacements were found";
        }

        var syns = articleGradingInfo.RareWordsSyns[word].Select(s => s.Item1);

        return string.Join(", ", syns);
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

            var range = $"{sheet}!A:R";
            var valueRange = new ValueRange();

            string ip = GetIp();
            var geoInfo = await GetGeoInfoFromIp(ip);
 
            var objectList = new List<object>() { DateTime.Now.Date.ToShortDateString(), DateTime.Now.TimeOfDay, ip, geoInfo.Country, geoInfo.Region, geoInfo.City, history.Text120First, history.Text120FirstJargon, history.Text120FirstGPT3, history.Text60, history.Text60Jargon, history.Text60GPT3, history.Text30, history.Text30Jargon, history.Text30GPT3, history.Text120Last, history.WhichIsBetter, history.TargetAudience };
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


    private async Task<string> RephraseText(string text, string targetAudience)
    {
        string prompt = "Summarize this text for an eighth-grade student:";

        switch (targetAudience)
        {
            case "Elementary":
                prompt = "Please rewrite the following science text for readers who have learned science up to the age of 12, when they finished elementary school. Keep the language simple and easy to understand, avoiding technical jargon and complex explanations:";
                break;
            case "Junior":
                prompt = "Please rewrite the following science text for readers who have learned science up to the age of 15, when they finished junior high school. Make sure the language is clear and accessible, avoiding overly technical terms and concepts beyond a basic understanding:";
                break;
            case "High":
                prompt = "Please rewrite the following science text for readers who have learned science up to the age of 18, when they finished high school. Ensure that the language is comprehensible and avoids specialized terminology beyond a high school level understanding:";
                break;
            case "Adult":
                prompt = "Please rewrite the following science text for a diverse audience of adults with varying levels of science education, ranging from completing studies at age 15 to taking additional courses long ago. Ensure that the language is accessible and understandable to individuals with a basic understanding of science, avoiding overly technical terms and concepts:";
                break;
        }

        HttpRequestMessage request = CreatePostRequest($"{prompt} \n\n{text}");
        HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        string jsonResponse = await response.Content.ReadAsStringAsync();

        return ExtractText(jsonResponse);
    }

    private async Task<Dictionary<string, List<string>>> GetSynonyms(List<string> words)
    {
        HttpRequestMessage request = CreatePostRequest($"I want to find synonyms for: {string.Join(", ", words)}. Reply with a json dict of syns without any additional text.");
        HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        string jsonResponse = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(ExtractText(jsonResponse));
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
