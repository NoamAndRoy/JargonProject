using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CriticalThinkingController : ApiController
{
    private static readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
    private static readonly string ApplicationName = "critical-thinking-chatbot";
    private static readonly string SpreadsheetId = "1n0uVf-ReplaceWithActualId"; // TODO: replace with the actual Google Sheet ID
    private static readonly string SheetName = "results";

    private readonly HttpClient client;
    private readonly SupabaseClient supabaseClient;
    private readonly string apiUrl = "https://api.openai.com/v1/engines/gpt-3.5-turbo-instruct/completions";
    private readonly string apiKey = "openapi-secret";

    public class ConversationHistory
    {
        public List<Message> Messages { get; set; } = new List<Message>();
        public int CurrentStage { get; set; }
        public bool isResearch { get; set; }
        public string ParticipantName { get; set; }
        public DateTime StartTime { get; set; }

        public string InitialSummary { get; set; }
        public string CurrentSummary { get; set; }

        public string Question1Answer { get; set; }
        public string RevisionAfterQuestion1 { get; set; }
        public string Feedback1 { get; set; }

        public string Question2Answer { get; set; }
        public string RevisionAfterQuestion2 { get; set; }
        public string Feedback2 { get; set; }

        public string Question3Answer { get; set; }
        public string RevisionAfterQuestion3 { get; set; }
        public string Feedback3 { get; set; }

        public string Question4Answer { get; set; }
        public string RevisionAfterQuestion4 { get; set; }
        public string Feedback4 { get; set; }

        public string Question5Answer { get; set; }
        public string RevisionAfterQuestion5 { get; set; }
        public string Feedback5 { get; set; }

        public string FinalSummary { get; set; }

        public string ReflectionAnswer1 { get; set; }
        public string ReflectionAnswer2 { get; set; }
        public string ReflectionAnswer3 { get; set; }
        public string ReflectionAnswer4 { get; set; }
        public string ReflectionOpenResponse { get; set; }
    }

    public class Message
    {
        public string Text { get; set; }
        public bool IsStudent { get; set; }
    }

    public CoherenceController()
    {
        var handler = new HttpClientHandler
        {
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12
        };
        handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        client = new HttpClient(handler);
        client.DefaultRequestHeaders.ConnectionClose = false;

        supabaseClient = (SupabaseClient)GlobalConfiguration.Configuration.Properties["SupabaseClient"];
    }

    [HttpPost]
    public async Task<IHttpActionResult> ProcessConversation([FromBody] ConversationHistory history)
    {
        try
        {
            history.Messages = history.Messages ?? new List<Message>();

            var authHeader = HttpContext.Current.Request.Headers["Authorization"];
            var userId = supabaseClient.GetUserId(authHeader);

            var responseMessages = await DetermineResponse(history, userId);

            history.Messages.AddRange(responseMessages.Select(m => new Message { Text = m, IsStudent = false }));

            return Ok(new { Messages = responseMessages, UpdatedHistory = history });
        }
        catch (Exception ex)
        {
            return InternalServerError(ex);
        }
    }

    private async Task<List<string>> DetermineResponse(ConversationHistory history, string userId)
    {
        var lastStudentMessage = history.Messages.LastOrDefault(m => m.IsStudent)?.Text?.Trim();

        switch (history.CurrentStage)
        {
            case 0:
                history.CurrentStage = history.isResearch ? 1 : 2;

                var greeting = new List<string>
                {
                    "Hi, I’m your academic writing assistant. My main focus is helping you improve critical thinking in your writing. Please upload or paste your text, and I’ll guide you by asking questions that help you track and refine your writing. If any question feels unclear, you can ask me for clarification or examples."
                };

                if (history.isResearch)
                {
                    greeting.Add("Please provide your full name so we can save your data for the research study.");
                }
                else
                {
                    history.ParticipantName = null;
                    history.StartTime = DateTime.UtcNow;
                    greeting.Add("Please provide a 150–200-word summary of your research before starting the first question.");
                }

                return greeting;

            case 1:
                if (!history.isResearch)
                {
                    history.StartTime = DateTime.UtcNow;
                    history.CurrentStage = 2;
                    return new List<string>
                    {
                        "Please provide a 150–200-word summary of your research before starting the first question."
                    };
                }

                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please provide the name you would like us to record." };
                }

                history.ParticipantName = lastStudentMessage.Trim();
                history.StartTime = DateTime.UtcNow;
                history.CurrentStage = 2;
                return new List<string>
                {
                    "Thank you.",
                    "Please provide a 150–200-word summary of your research before starting the first question."
                };

            case 2:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please provide a 150–200-word summary of your research before starting the first question." };
                }

                var summaryValidation = ValidateWordCount(lastStudentMessage, 150, 200);
                if (summaryValidation != null)
                {
                    return new List<string> { summaryValidation };
                }

                history.InitialSummary = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 3;
                return new List<string>
                {
                    "1. Please answer the question as best as possible in 1-2 sentences: The main purpose of this text is . . . (Here you are trying to state as accurately as possible your purpose for writing the article. What were you trying to accomplish?)",
                    "Answer (30-80 words)"
                };

            case 3:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please describe the main purpose in 30-80 words." };
                }

                var q1Validation = ValidateWordCount(lastStudentMessage, 30, 80);
                if (q1Validation != null)
                {
                    return new List<string> { q1Validation };
                }

                history.Question1Answer = lastStudentMessage;
                history.Feedback1 = await GenerateFeedbackAsync(1, history);
                history.CurrentStage = 4;
                return new List<string>
                {
                    history.Feedback1,
                    "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant. Please make sure the purpose is stated clearly. (150–200 words)"
                };

            case 4:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please provide a 150–200-word revision." };
                }

                var revision1Validation = ValidateWordCount(lastStudentMessage, 150, 200);
                if (revision1Validation != null)
                {
                    return new List<string> { revision1Validation };
                }

                history.RevisionAfterQuestion1 = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 5;
                return new List<string>
                {
                    "2. Please answer the question as best as possible in 1-2 sentences: The key research question(s) (whether stated or unstated) at issue for this research is/are . . . (Your goal is to figure out the key question that was in your mind when you wrote the article)",
                    "Answer (30-80 words)"
                };

            case 5:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please state the key research question(s) in 30-80 words." };
                }

                var q2Validation = ValidateWordCount(lastStudentMessage, 30, 80);
                if (q2Validation != null)
                {
                    return new List<string> { q2Validation };
                }

                history.Question2Answer = lastStudentMessage;
                history.Feedback2 = await GenerateFeedbackAsync(2, history);
                history.CurrentStage = 6;
                return new List<string>
                {
                    history.Feedback2,
                    "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant. Please try to make sure the key questions are stated clearly. (150–200 words)"
                };

            case 6:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please share a 150–200-word revision." };
                }

                var revision2Validation = ValidateWordCount(lastStudentMessage, 150, 200);
                if (revision2Validation != null)
                {
                    return new List<string> { revision2Validation };
                }

                history.RevisionAfterQuestion2 = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 7;
                return new List<string>
                {
                    "3. Please answer the question as best as possible in 1-2 sentences: The most important information in this text is . . . (You want to identify the key information you used, or presupposed, in the full article you are writing to support your main arguments. Here you are looking for facts, experiences, data you are using to support your conclusions).",
                    "Answer (30-100 words)"
                };

            case 7:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please describe the key information in 30-100 words." };
                }

                var q3Validation = ValidateWordCount(lastStudentMessage, 30, 100);
                if (q3Validation != null)
                {
                    return new List<string> { q3Validation };
                }

                history.Question3Answer = lastStudentMessage;
                history.Feedback3 = await GenerateFeedbackAsync(3, history);
                history.CurrentStage = 8;
                return new List<string>
                {
                    history.Feedback3,
                    "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant. Please make sure the key information is stated clearly. (150–200 words)"
                };

            case 8:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please share a 150–200-word revision." };
                }

                var revision3Validation = ValidateWordCount(lastStudentMessage, 150, 200);
                if (revision3Validation != null)
                {
                    return new List<string> { revision3Validation };
                }

                history.RevisionAfterQuestion3 = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 9;
                return new List<string>
                {
                    "4. Please answer the question as best as possible in 1-2 sentences: The main conclusion(s) in this text is/are. . . (You want to identify the most important conclusions that you came to and presented/will present in the full article you are writing).",
                    "Answer (30-90 words)"
                };

            case 9:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please share the main conclusion(s) in 30-90 words." };
                }

                var q4Validation = ValidateWordCount(lastStudentMessage, 30, 90);
                if (q4Validation != null)
                {
                    return new List<string> { q4Validation };
                }

                history.Question4Answer = lastStudentMessage;
                history.Feedback4 = await GenerateFeedbackAsync(4, history);
                history.CurrentStage = 10;
                return new List<string>
                {
                    history.Feedback4,
                    "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant. Please make sure the conclusion is stated clearly. (150–200 words)"
                };

            case 10:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please share a 150–200-word revision." };
                }

                var revision4Validation = ValidateWordCount(lastStudentMessage, 150, 200);
                if (revision4Validation != null)
                {
                    return new List<string> { revision4Validation };
                }

                history.RevisionAfterQuestion4 = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 11;
                return new List<string>
                {
                    "5. Please answer the question as best as possible in 1-2 sentences: What is the significance of the text in the broader context? (Ask yourself: How does this work contribute to the field, and why does it matter?).",
                    "Answer (30-80 words)"
                };

            case 11:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please explain the broader significance in 30-80 words." };
                }

                var q5Validation = ValidateWordCount(lastStudentMessage, 30, 80);
                if (q5Validation != null)
                {
                    return new List<string> { q5Validation };
                }

                history.Question5Answer = lastStudentMessage;
                history.Feedback5 = await GenerateFeedbackAsync(5, history);
                history.CurrentStage = 12;
                return new List<string>
                {
                    history.Feedback5,
                    "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant. Please make sure the significance in the broader context is stated clearly. (150–200 words)"
                };

            case 12:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please share a 150–200-word revision." };
                }

                var revision5Validation = ValidateWordCount(lastStudentMessage, 150, 200);
                if (revision5Validation != null)
                {
                    return new List<string> { revision5Validation };
                }

                history.RevisionAfterQuestion5 = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 13;
                return new List<string>
                {
                    "After answering all 5 questions, revise your summary/paragraphs and write/copy the final revised version below. (150–200 words)"
                };

            case 13:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please provide the final 150–200-word version." };
                }

                var finalValidation = ValidateWordCount(lastStudentMessage, 150, 200);
                if (finalValidation != null)
                {
                    return new List<string> { finalValidation };
                }

                history.FinalSummary = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 14;
                return new List<string>
                {
                    "Thank you! We hope you learned about improving your academic texts with the help of critical thinking!",
                    "The final step is to answer four short, close-ended reflection questions about the chatbot use and one optional open question. For each statement, please select an option from a 5-point Likert scale where 1 = strongly disagree and 5 = strongly agree.",
                    "Reflection 1: The chatbot was friendly and easy to interact with. Answer: choose one 1- strongly disagree  2- disagree 3- Neutral 4- agree 5- strongly agree"
                };

            case 14:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<string> { "Please reply with a single number from 1 (strongly disagree) to 5 (strongly agree)." };
                }

                history.ReflectionAnswer1 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 15;
                return new List<string>
                {
                    "Reflection 2: I found the chatbot challenging in a way that stimulated my critical thinking. Answer: choose one 1- strongly disagree  2- disagree 3- Neutral 4- agree 5- strongly agree"
                };

            case 15:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<string> { "Please reply with a single number from 1 to 5." };
                }

                history.ReflectionAnswer2 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 16;
                return new List<string>
                {
                    "Reflection 3: The chatbot was useful for improving the quality of my writing. Answer: choose one 1- strongly disagree  2- disagree 3- Neutral 4- agree 5- strongly agree"
                };

            case 16:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<string> { "Please reply with a single number from 1 to 5." };
                }

                history.ReflectionAnswer3 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 17;
                return new List<string>
                {
                    "Reflection 4: The chatbot made the task more difficult than it needed to be. Answer: choose one 1- strongly disagree  2- disagree 3- Neutral 4- agree 5- strongly agree"
                };

            case 17:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<string> { "Please reply with a single number from 1 to 5." };
                }

                history.ReflectionAnswer4 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 18;
                return new List<string>
                {
                    "How did you feel while using the chatbot during your writing task? How did it help you, if it did? What did you learn about critical thinking and improving your academic writing?"
                };

            case 18:
                history.ReflectionOpenResponse = lastStudentMessage ?? string.Empty;
                history.CurrentStage = 19;

                if (history.isResearch)
                {
                    await SaveToGoogleSheets(history, userId);
                }

                return new List<string>
                {
                    "Thank you for sharing your reflections. Your responses have been recorded."
                };

            default:
                return new List<string>
                {
                    "If you need further assistance, feel free to start a new conversation."
                };
        }
    }

    private string ValidateWordCount(string text, int min, int max)
    {
        var words = text?.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        if (words.Length < min || words.Length > max)
        {
            return $"Please use {min}-{max} words. Currently, you used {words.Length}.";
        }

        return null;
    }

    private bool IsValidLikertChoice(string input)
    {
        var normalized = NormalizeLikertChoice(input);
        return normalized is "1" or "2" or "3" or "4" or "5";
    }

    private string NormalizeLikertChoice(string input)
    {
        return input?.Trim().ToLowerInvariant() switch
        {
            "1" => "1",
            "2" => "2",
            "3" => "3",
            "4" => "4",
            "5" => "5",
            "strongly disagree" => "1",
            "disagree" => "2",
            "neutral" => "3",
            "agree" => "4",
            "strongly agree" => "5",
            _ => input?.Trim()
        } ?? string.Empty;
    }

    private async Task<string> GenerateFeedbackAsync(int questionNumber, ConversationHistory history)
    {
        try
        {
            var prompt = BuildFeedbackPrompt(questionNumber, history);
            var request = CreatePostRequest(prompt);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseContent);
            return json["choices"]?[0]?["text"]?.ToString().Trim() ?? "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error generating feedback: {ex.Message}");
            return "I encountered an issue generating feedback. Please revise using your best judgment.";
        }
    }

    private string BuildFeedbackPrompt(int questionNumber, ConversationHistory history)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an academic writing assistant helping a PhD STEM student strengthen critical thinking in their writing.");
        sb.AppendLine("Always address the student as \"you\".");
        sb.AppendLine("Provide suggestions and feedback only; do not rewrite their text.");
        sb.AppendLine("Keep feedback to 2-3 sentences.");
        sb.AppendLine("Maintain a professional, supportive, academic tone.");
        sb.AppendLine("Focus on fostering critical engagement, revision, and iterative improvement.");

        switch (questionNumber)
        {
            case 1:
                sb.AppendLine("Student's initial summary (150-200 words):");
                sb.AppendLine(history.InitialSummary ?? string.Empty);
                sb.AppendLine("Student's answer about the main purpose (30-80 words):");
                sb.AppendLine(history.Question1Answer ?? string.Empty);
                sb.AppendLine("Evaluate whether the summary accurately presents the purpose of the research and offer suggestions to clarify the purpose if needed.");
                break;
            case 2:
                sb.AppendLine("Student's revised summary (after Question 1, 150-200 words):");
                sb.AppendLine(history.RevisionAfterQuestion1 ?? history.CurrentSummary ?? string.Empty);
                sb.AppendLine("Student's key research questions answer (30-80 words):");
                sb.AppendLine(history.Question2Answer ?? string.Empty);
                sb.AppendLine("Assess whether the research questions are clear and aligned with the stated purpose. Suggest ways to strengthen clarity and alignment.");
                break;
            case 3:
                sb.AppendLine("Student's revised summary (after Question 2, 150-200 words):");
                sb.AppendLine(history.RevisionAfterQuestion2 ?? history.CurrentSummary ?? string.Empty);
                sb.AppendLine("Student's description of key information (30-100 words):");
                sb.AppendLine(history.Question3Answer ?? string.Empty);
                sb.AppendLine("Comment on how well the key information supports the research questions and whether additional facts/data are needed.");
                break;
            case 4:
                sb.AppendLine("Student's revised summary (after Question 3, 150-200 words):");
                sb.AppendLine(history.RevisionAfterQuestion3 ?? history.CurrentSummary ?? string.Empty);
                sb.AppendLine("Student's conclusions answer (30-90 words):");
                sb.AppendLine(history.Question4Answer ?? string.Empty);
                sb.AppendLine("Evaluate whether the conclusions address the stated questions and goals, and note if any claims need stronger support.");
                break;
            case 5:
                sb.AppendLine("Student's revised summary (after Question 4, 150-200 words):");
                sb.AppendLine(history.RevisionAfterQuestion4 ?? history.CurrentSummary ?? string.Empty);
                sb.AppendLine("Student's explanation of broader significance (30-80 words):");
                sb.AppendLine(history.Question5Answer ?? string.Empty);
                sb.AppendLine("Discuss how clearly the response conveys the broader contribution and suggest improvements to highlight significance.");
                break;
        }

        return sb.ToString();
    }

    private HttpRequestMessage CreatePostRequest(string prompt)
    {
        var requestBody = new
        {
            prompt,
            max_tokens = 256,
            temperature = 0.4,
            top_p = 1.0,
            frequency_penalty = 0,
            presence_penalty = 0
        };

        var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
        return request;
    }

    private async Task SaveToGoogleSheets(ConversationHistory history, string userId)
    {
        try
        {
            var isSaveUserData = await supabaseClient.getIsSaveUserData(userId);
            if (!isSaveUserData)
            {
                return;
            }

            var service = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = GetCredentials(),
                ApplicationName = ApplicationName,
            });

            var ip = GetIp();
            var geo = await GetGeoInfoFromIp(ip);

            var row = new List<object>
            {
                DateTime.UtcNow.ToString("yyyy-MM-dd"),
                DateTime.UtcNow.ToString("HH:mm:ss"),
                history.isResearch ? "Research" : "Regular",
                isSaveUserData ? userId : null,
                isSaveUserData ? history.ParticipantName : null,
                ip,
                geo.Country,
                geo.Region,
                geo.City,
                history.InitialSummary,
                history.Question1Answer,
                history.RevisionAfterQuestion1,
                history.Question2Answer,
                history.RevisionAfterQuestion2,
                history.Question3Answer,
                history.RevisionAfterQuestion3,
                history.Question4Answer,
                history.RevisionAfterQuestion4,
                history.Question5Answer,
                history.RevisionAfterQuestion5,
                history.FinalSummary,
                history.ReflectionAnswer1,
                history.ReflectionAnswer2,
                history.ReflectionAnswer3,
                history.ReflectionAnswer4,
                history.ReflectionOpenResponse
            };

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { row }
            };

            var appendRequest = service.Spreadsheets.Values.Append(valueRange, SpreadsheetId, $"{SheetName}!A:Z");
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            appendRequest.Execute();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving to Google Sheets: {ex.Message}");
        }
    }

    private string GetIp()
    {
        var ip = HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
        if (string.IsNullOrEmpty(ip))
        {
            ip = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
        }

        if (string.IsNullOrEmpty(ip))
        {
            ip = HttpContext.Current.Request.UserHostAddress;
        }

        return ip ?? string.Empty;
    }

    private async Task<(string Country, string Region, string City)> GetGeoInfoFromIp(string ip)
    {
        try
        {
            using (var geoClient = new HttpClient())
            {
                var response = await geoClient.GetStringAsync($"http://ip-api.com/json/{ip}?fields=status,country,regionName,city");
                var json = JObject.Parse(response);

                if (json["status"]?.ToString() == "success")
                {
                    return (
                        json["country"]?.ToString() ?? "Unknown",
                        json["regionName"]?.ToString() ?? "Unknown",
                        json["city"]?.ToString() ?? "Unknown");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching geo info: {ex.Message}");
        }

        return ("Unknown", "Unknown", "Unknown");
    }

    private GoogleCredential GetCredentials()
    {
        string path = HttpContext.Current.Server.MapPath(@"~\half-life-dejargonizer-f4e89fe2bc96.json");
        return GoogleCredential.FromFile(path).CreateScoped(Scopes);
    }
}
