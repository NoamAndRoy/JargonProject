using System.Diagnostics;
using JargonProject.Handlers;
using JargonProject.Models;
using JargonProject.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[ApiController]
[Route("api/[controller]")]
public class PersuasiveController : ControllerBase
{
    private readonly SupabaseClient _supabaseClient;
    private readonly HttpClient _client;
    private readonly UsageCounter _usageCounter;
    private readonly IWebHostEnvironment _env;

    private readonly ILogger<PersuasiveController> _logger;

    readonly Dictionary<int, (int min, int max)> wordCountRanges = new Dictionary<int, (int min, int max)>
    {
        { 120, (100, 140) },
        { 3, (2, 5) },
        { 5, (5, 120) },
        { 10, (2, 40) },
    };

    public class PersuasiveConversationHistory
    {
        public List<PersuasiveMessage> Messages { get; set; }
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
        public string WhyIsBetter { get; set; }
        public string WhatYouHaveLearnt { get; set; }
        public string AdditionalInformation { get; set; }

        public string OriginalTextJargon { get; set; }
        public string TextLogosGpt3 { get; set; }
        public string TextPathosGpt3 { get; set; }
        public string TextEthosGpt3 { get; set; }

        public string TargetAudience { get; set; }
        public string WhichIsBetter { get; set; }
        public DateTime StartTime { get; set; }
        public bool CopyPasteCheck { get; set; }
    }

    [Table("persuasive_user_interactions")]
    public class PersuasiveUserInteraction : BaseModel
    {
        [Column("user_id")]
        public string? UserId { get; set; }

        [Column("detailed_audience")]
        public string? DetailedAudience { get; set; }
        [Column("original_text")]
        public string? OriginalText { get; set; }
        [Column("text_logos")]
        public string? TextLogos { get; set; }
        [Column("text_audience_interests")]
        public string? TextAudienceInterests { get; set; }
        [Column("pathos_interests_reflected")]
        public string? PathosInterestsReflected { get; set; }
        [Column("text_pathos")]
        public string? TextPathos { get; set; }
        [Column("ethos_affiliation")]
        public string? EthosAffiliation { get; set; }
        [Column("ethos_affiliation_reflected")]
        public string? EthosAffiliationReflected { get; set; }
        [Column("text_ethos")]
        public string? TextEthos { get; set; }
        [Column("why_is_better")]
        public string? WhyIsBetter { get; set; }
        [Column("what_you_have_learnt")]
        public string? WhatYouHaveLearnt { get; set; }
        [Column("original_text_jargon")]
        public string? OriginalTextJargon { get; set; }
        [Column("text_logos_gpt3")]
        public string? TextLogosGpt3 { get; set; }
        [Column("text_pathos_gpt3")]
        public string? TextPathosGpt3 { get; set; }
        [Column("text_ethos_gpt3")]
        public string? TextEthosGpt3 { get; set; }
        [Column("additional_information")]
        public string? AdditionalInformation { get; set; }

        [Column("target_audience")]
        public string? TargetAudience { get; set; }
        [Column("which_is_better")]
        public string? WhichIsBetter { get; set; }
        [Column("start_time")]
        public DateTime StartTime { get; set; }
        [Column("end_time")]
        public DateTime EndTime { get; set; }
        [Column("copy_paste_check")]
        public bool CopyPasteCheck { get; set; }
    }

    public class PersuasiveMessage
    {
        public string Text { get; set; }
        public bool IsStudent { get; set; }  // Indicates the current stage of the conversation
    }

    public PersuasiveController(IHttpClientFactory httpClientFactory, SupabaseClient supabaseClient, ILogger<PersuasiveController> logger, UsageCounter usageCounter, IWebHostEnvironment env)
    {
        _client = httpClientFactory.CreateClient("CustomClient");
        _supabaseClient = supabaseClient;
        _logger = logger;
        _usageCounter = usageCounter;
        _env = env;
    }


    [HttpPost]
    public async Task<IActionResult> ProcessConversation([FromBody] PersuasiveConversationHistory history)
    {
        try
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();

            var userId = _supabaseClient.GetUserId(authHeader);
            var responseMessages = await DetermineResponse(history, userId);

            // Update history with the response
            history.Messages.AddRange(responseMessages.Select(m => new PersuasiveMessage { Text = m, IsStudent = false }));

            return Ok(new { Messages = responseMessages, UpdatedHistory = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing conversation.");
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }

    private async Task<List<string>> DetermineResponse(PersuasiveConversationHistory history, string userId)
    {
        var lastUserText = history.Messages.LastOrDefault(x => x.IsStudent);

        switch (history.CurrentStage)
        {
            case 1:
                if (ValidateUserRespose(lastUserText.Text, new List<string> { "1", "2", "3", "4" }))
                {
                    history.StartTime = DateTime.Now;
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


                TextGrading.Lang = Language.English2021_2024;
                var articleGradingInfo = TextGrading.AnalyzeSingleText(lastUserText.Text.Trim(), _env);

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

                    history.OriginalTextJargon = string.Join(", ", articleGradingInfo.RareWordsSyns.Keys);
                }

                var rephrasedText = await RephraseText(lastUserText.Text, "Adult");
                var gptSuggestion = "This is another version, as suggested by ChatGPT, which may contain additional relevant information and evidence(‘logos’) for your audience.";

                history.TextLogosGpt3 = rephrasedText;

                responses.AddRange(new List<string>
                {
                    gptSuggestion,
                    rephrasedText,
                    "Please look at your original paragraph and the feedback above. Think about what information you may want to add or change, and create a new, revised version:",
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
                    "technology; solving a problem financially; solving a problem ethically; medical treatment; basic science, etc.",
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
                    history.TextPathosGpt3 = rephrasedText2;

                    return new List<string>
                    {
                        rephrasedText2,
                        "Please carefully read what ChatGPT understood as your shared value (pathos) from what you have written. If you now notice that something is missing or inaccurate in your text, please revise your text below. If you are happy with your text, copy it below as it to progress to the last stage."
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
                    "What information can you include so that your reader will find you to be credible? Please add a short list or one sentence (ex. affiliation, mention of previous work or methods, correct level of vocabulary).",
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
                    history.TextEthosGpt3 = rephrasedText2;

                    return new List<string>
                    {
                        rephrasedText2,
                        "Please read ChatGPT’s ideas on how you may improve your ethos according to the suggestion/s you made above. Then, please revise your text one last time to include this as well."
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
                    "One last question 😊! Could you list (1-3 sentences) about what you have learned about persuasive writing and how you may implement this tool in the future when you write?",
                };
            case 14:
                var response9 = ValidateWordCount(lastUserText.Text, 5);

                if (!string.IsNullOrEmpty(response9))
                {
                    return new List<string> { response9 };
                }

                history.CurrentStage++;
                history.WhatYouHaveLearnt = lastUserText.Text;

                //await SaveToGoogleSheets(history);
                await SaveToSupabase(history, userId);

                _usageCounter.UpdateNumberOfUses(1);

                return new List<string> {
                    "Thank you for using our tool!",
                    "We hope that you have learned more on persuasive writing from this task – and we hope to see you again soon!"
                };

            default:
                history.CurrentStage++;

                return new List<string> {
                    "The following task will ask you to write a series of 3 paragraphs about your research project. Before you write a 120-word summary of your research for a specific audience, please answer the following question:",
                    "Who will be reading this work?" +
                    "<div class='chat-option'>(1) an academic audience</div>" +
                    "<div class='chat-option'>(2) a general audience</div>" +
                    "<div class='chat-option'>(3) investors / grant money</div>" +
                    "<div class='chat-option'>(4) government committees</div>",
                };
        }
    }

    private async Task SaveToSupabase(PersuasiveConversationHistory history, string userId)
    {
        var isSaveUserData = await _supabaseClient.getIsSaveUserData(userId);

        var data = new PersuasiveUserInteraction
        {
            UserId = isSaveUserData ? userId : null,
            TargetAudience = history.TargetAudience,
            StartTime = history.StartTime,
            EndTime = DateTime.Now,
            CopyPasteCheck = history.CopyPasteCheck,
            WhichIsBetter = history.WhichIsBetter,

            DetailedAudience = isSaveUserData ? history.DetailedAudience : null,
            OriginalText = isSaveUserData ? history.OriginalText : null,
            TextLogos = isSaveUserData ? history.TextLogos : null,
            TextAudienceInterests = isSaveUserData ? history.TextAudienceInterests : null,
            PathosInterestsReflected = isSaveUserData ? history.PathosInterestsReflected : null,
            TextPathos = isSaveUserData ? history.TextPathos : null,
            EthosAffiliation = isSaveUserData ? history.EthosAffiliation : null,
            EthosAffiliationReflected = isSaveUserData ? history.EthosAffiliationReflected : null,
            TextEthos = isSaveUserData ? history.TextEthos : null,
            WhyIsBetter = isSaveUserData ? history.WhyIsBetter : null,
            WhatYouHaveLearnt = isSaveUserData ? history.WhatYouHaveLearnt : null,
            AdditionalInformation = isSaveUserData ? history.AdditionalInformation : null,
            OriginalTextJargon = isSaveUserData ? history.OriginalTextJargon: null,
            TextLogosGpt3 = isSaveUserData ? history.TextLogosGpt3: null,
            TextPathosGpt3 = isSaveUserData ? history.TextPathosGpt3: null,
            TextEthosGpt3 = isSaveUserData ? history.TextEthosGpt3 : null,
        };

        try
        {
            await _supabaseClient.client.From<PersuasiveUserInteraction>().Insert(data);
            Debug.WriteLine("Data successfully saved to Supabase.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving data to Supabase: {ex.Message}");
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
        HttpResponseMessage response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        string jsonResponse = await response.Content.ReadAsStringAsync();

        return ExtractText(jsonResponse);
    }

    private HttpRequestMessage CreatePostRequest(string prompt)
    {
        string apiUrl = Environment.GetEnvironmentVariable("OPENAI_API_URL");
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

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
