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

public class PersuasiveController : ApiController
{
    // Google Sheets API configuration
    private static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
    private static readonly string ApplicationName = "half-life-dejargonizer";
    private static readonly string SpreadsheetId = "12n65H5wH4HNCaEgjBp13faRT4cAQ4Qp9wNrXI1BAt54";
    private static readonly string sheet = "results";

    private readonly HttpClient client;
    private readonly string apiUrl = "https://api.openai.com/v1/engines/gpt-3.5-turbo-instruct/completions";
    private readonly string apiKey = "<openapi-secret>";  // Replace 'openapi-secret' with your actual API key

    readonly Dictionary<int, (int min, int max)> wordCountRanges = new Dictionary<int, (int min, int max)>
    {
        { 120, (100, 140) },
        { 3, (2, 5) },
        { 10, (2, 40) },
    };

    public class ConversationHistory
    {
        public List<Message> Messages { get; set; }
        public int CurrentStage { get; set; }
        public string DetailedAudience { get; set; }
        public string OriginalText { get; set; }
        public string TextLogos { get; set; }
        public string TextAudienceInterests { get; set; }
        public string PathosInterestsReflected { get; set; }
        public string TextPathos { get; set; }
        public string EthosAffiliation { get; set; }
        public string EthosAffiliationReflected { get; set; }
        public string TextEthos { get; set; }
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

    public PersuasiveController()
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
                            history.TargetAudience = "academic";
                            break;
                        case "2":
                            history.TargetAudience = "general";
                            break;
                        case "3":
                            history.TargetAudience = "investors";
                            break;
                        case "4":
                            history.TargetAudience = "government";
                            break;
                    }

                    return new List<string>
                    {
                        "Can you give us more detail about your audience?",
                        "For example, if you chose academic audience, please indicate which discipline. If you chose a general audience, please indicate what age (e.g. high school, adults, etc.)."
                    };
                }
                else
                {
                    return new List<string> {
                        "Please enter your answer as single digit:" +
                        "<div class='chat-option'>(1) an academic audience</div>" +
                        "<div class='chat-option'>(2) a general audience</div>" +
                        "<div class='chat-option'>(3) investors / grant money</div>" +
                        "<div class='chat-option'>(4) government committees</div>"
                    };
                }
            case 2:
                history.DetailedAudience = lastUserText.Text;
                history.CurrentStage++;

                return new List<string> {
                    "Please write a 120-word summary of your research for the audience you chose. Think about the main research goals."
                };
            case 3:
                var response = ValidateWordCount(lastUserText.Text, 120);

                if (!string.IsNullOrEmpty(response))
                {
                    return new List<string> { response };
                }

                history.CurrentStage++;

                history.OriginalText = lastUserText.Text;

                var responses = new List<string> {
                    "This stage of the task will help you check and revise your logos (the content of your summary):",
                };


                TextGrading.Lang = Language.English2020_2023;
                var articleGradingInfo = TextGrading.AnalyzeSingleText(lastUserText.Text.Trim());

                if (articleGradingInfo.RareWordsSyns.Count > 0)
                {
                    var wordsWithSyns = string.Join(", ",
                        articleGradingInfo.RareWordsSyns.Keys.Select(w =>
                            $"<span class=\"rare-word\" title=\"Optional replacemets: {GenerateReplacementSyns(articleGradingInfo, w)}.\">{w}</span>"
                        )
                    );

                    var jargonFeedback = $"{wordsWithSyns} - These words are considered jargon. Please look at the list and see if these words are clear for the" +
                                        $" audience you have chosen, and if there are any key terms that you have left out.";

                    responses.Add(jargonFeedback);
                }

                var rephrasedText = await RephraseText(lastUserText.Text, "Adult");
                var gptSuggestion = "This is another version, as suggested by ChatGPT, for your audience.";

                responses.AddRange(new List<string>
                {
                    gptSuggestion,
                    rephrasedText,
                    "Please look at the feedback above and create a new, revised version:",
                });

                return responses;
            case 4:
                var response2 = ValidateWordCount(lastUserText.Text, 120);

                if (!string.IsNullOrEmpty(response2))
                {
                    return new List<string> { response2 };
                }

                history.CurrentStage++;
                history.TextLogos = lastUserText.Text;

                return new List<string> {
                    "This stage of the task will help you check and revise your pathos (shared values with your audience):",
                    "What is the shared value that interests your audience (in 2-5 words)? For example: helping people; more accessible or accurate " +
                    "technology; solving a problem financially; solving a problemethically; medical treatment; basic science, etc.",
                };
            case 5:
                var response3 = ValidateWordCount(lastUserText.Text, 3);

                if (!string.IsNullOrEmpty(response3))
                {
                    return new List<string> { response3 };
                }

                history.CurrentStage++;
                history.TextAudienceInterests = lastUserText.Text;

                return new List<string> {
                    "Is this reflected in your summary?",
                    "<div class='chat-option'>(1) Yes </div>" +
                    "<div class='chat-option'>(2) No</div>" +
                    "<div class='chat-option'>(3) Not sure</div>",
                };
            case 6:
                if (ValidateUserRespose(lastUserText.Text, new List<string> { "1", "2", "3" }))
                {
                    history.CurrentStage++;

                    switch (lastUserText.Text)
                    {
                        case "1":
                            history.PathosInterestsReflected = "Yes";
                            break;
                        case "2":
                            history.PathosInterestsReflected = "No";
                            break;
                        case "3":
                            history.PathosInterestsReflected = "Not sure";
                            break;
                    }


                    var rephrasedText2 = await RephraseText(history.TextLogos, "Pathos", history.TextAudienceInterests);

                    return new List<string>
                    {
                        rephrasedText2,
                        "Please look at the feedback above and create a new, revised version."
                    };
                }
                else
                {
                    return new List<string> {
                        "Please enter your answer as single digit:" +
                        "<div class='chat-option'>(1) Yes</div>" +
                        "<div class='chat-option'>(2) No</div>" +
                        "<div class='chat-option'>(3) Not sure</div>"
                    };
                }
            case 7:
                var response4 = ValidateWordCount(lastUserText.Text, 120);

                if (!string.IsNullOrEmpty(response4))
                {
                    return new List<string> { response4 };
                }

                history.CurrentStage++;
                history.TextPathos = lastUserText.Text;

                return new List<string> {
                    "This stage of the task will help you check and revise your ethos – (your credibility):",
                    "What information can you include so that your reader will find you to be credible? Please add a short list or one sentence (Ex. Affiliation, experience, etc.).",
                };
            case 8:
                var response5 = ValidateWordCount(lastUserText.Text, 10);

                if (!string.IsNullOrEmpty(response5))
                {
                    return new List<string> { response5 };
                }

                history.CurrentStage++;
                history.EthosAffiliation = lastUserText.Text;

                return new List<string> {
                    "Is this reflected in your summary?",
                    "<div class='chat-option'>(1) Yes </div>" +
                    "<div class='chat-option'>(2) No</div>" +
                    "<div class='chat-option'>(3) Not sure</div>",
                };
            case 9:
                if (ValidateUserRespose(lastUserText.Text, new List<string> { "1", "2", "3" }))
                {
                    history.CurrentStage++;

                    switch (lastUserText.Text)
                    {
                        case "1":
                            history.EthosAffiliationReflected = "Yes";
                            break;
                        case "2":
                            history.EthosAffiliationReflected = "No";
                            break;
                        case "3":
                            history.EthosAffiliationReflected = "Not sure";
                            break;
                    }


                    var rephrasedText2 = await RephraseText(history.TextPathos, "Ethos", history.EthosAffiliation);

                    return new List<string>
                    {
                        rephrasedText2,
                        "Please look at the feedback above and create a new, revised version."
                    };
                }
                else
                {
                    return new List<string> {
                        "Please enter your answer as single digit:" +
                        "<div class='chat-option'>(1) Yes</div>" +
                        "<div class='chat-option'>(2) No</div>" +
                        "<div class='chat-option'>(3) Not sure</div>"
                    };
                }
            case 10:
                var response6 = ValidateWordCount(lastUserText.Text, 120);

                if (!string.IsNullOrEmpty(response6))
                {
                    return new List<string> { response6 };
                }

                history.CurrentStage++;
                history.TextEthos = lastUserText.Text;

                return new List<string> {
                    "Which version is better in your option?" +
                    "<div class='chat-option'>(1) The original 120 word summary</div>" +
                    "<div class='chat-option'>(2) The final 120 word summary</div>" +
                    "<div class='chat-option'>(3) The texts are equal</div>",
                };
            case 11:
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
                        "<div class='chat-option'>(1) The original 120 word summary</div>" +
                        "<div class='chat-option'>(2) The final 120 word summary</div>" +
                        "<div class='chat-option'>(3) The texts are equal</div>",
                    };
                }
            case 12:
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
            case 13:
                var response8 = ValidateWordCount(lastUserText.Text, 10);

                if (!string.IsNullOrEmpty(response8))
                {
                    return new List<string> { response8 };
                }

                history.CurrentStage++;
                history.AdditionalInformation = lastUserText.Text;

                return new List<string> {
                    "We hope you have learned more on persuasive writing!",
                    "We are conducting research on academic writing, and we would appreciate it if you would give us your consent to use your " +
                    "writing outcomes to assess how people write and use this tool. We will not share the content of your writing, just evaluate it." +
                    "<div class='chat-option'>(1) I give my consent</div>" +
                    "<div class='chat-option'>(2) I do not give my consent</div>",
                };
            case 14:
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
                    "Before you write a 120-word summary of your research for a specific audience, please answer the following question:",
                    "Who will be reading this work?" +
                    "<div class='chat-option'>(1) an academic audience</div>" +
                    "<div class='chat-option'>(2) a general audience</div>" +
                    "<div class='chat-option'>(3) investors / grant money</div>" +
                    "<div class='chat-option'>(4) government committees</div>",
                };
        }
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

            var range = $"{sheet}!A:S";
            var valueRange = new ValueRange();

            string ip = GetIp();
            var geoInfo = await GetGeoInfoFromIp(ip);

            var objectList = new List<object>() { DateTime.Now.Date.ToShortDateString(), DateTime.Now.TimeOfDay, ip, geoInfo.Country, geoInfo.Region, geoInfo.City,
                    history.DetailedAudience, history.OriginalText, history.TextLogos, history.TextAudienceInterests, history.PathosInterestsReflected, history.TextPathos,
                history.EthosAffiliation, history.EthosAffiliationReflected, history.TextEthos, history.WhichIsBetter, history.WhyIsBetter, history.AdditionalInformation, history.TargetAudience,  };

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


    private async Task<string> RephraseText(string text, string targetAudience, params string[] args)
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
                prompt = "Please rewrite the following science text for a diverse audience of adults with varying levels of science education, ranging from completing studies at age 15 to taking additional courses long ago. Ensure that the language is accessible and understandable to individuals with a basic understanding of science, avoiding overly technical terms and concepts. You may add additional and important information on the topic:";
                break;
            case "Pathos":
                prompt = string.Format("Please check this research summary for an {0} audience and comment if it reflects {0} value.", args);
                break;
            case "Ethos":
                prompt = string.Format("Please check this research summary and integrate the {0} affiliation into the text.", args);
                break;
        }

        HttpRequestMessage request = CreatePostRequest($"{prompt} \n\n{text}");
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
