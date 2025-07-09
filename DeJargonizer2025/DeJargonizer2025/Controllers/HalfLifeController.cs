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
public class HalfLifeController : ControllerBase
{
    private readonly SupabaseClient _supabaseClient;
    private readonly HttpClient _client;
    private readonly ILogger<HalfLifeController> _logger;
    private readonly UsageCounter _usageCounter;
    private readonly IWebHostEnvironment _env;

    readonly Dictionary<int, (int min, int max)> wordCountRanges = new Dictionary<int, (int min, int max)>
    {
        { 120, (100, 140) },
        { 60, (45, 75) },
        { 30, (20, 40) }
    };

    [Table("halflife_user_interactions")]
    public class HalfLifeUserInteraction : BaseModel
    {
        [Column("user_id")]
        public string? UserId { get; set; }
        [Column("target_audience")]
        public string? TargetAudience { get; set; }
        [Column("start_time")]
        public DateTime StartTime { get; set; }
        [Column("end_time")]
        public DateTime EndTime { get; set; }
        [Column("copy_paste_check")]
        public bool CopyPasteCheck { get; set; }
        [Column("which_is_better")]
        public string? WhichIsBetter { get; set; }

        // 120 Words - First
        [Column("text120_first")]
        public string? Text120First { get; set; }
        [Column("text120_first_jargon")]
        public string? Text120FirstJargon { get; set; }
        [Column("text120_first_gpt3")]
        public string? Text120FirstGPT3 { get; set; }
        [Column("text120_first_total_words")]
        public int Text120FirstTotalWords { get; set; }
        [Column("text120_first_rare_words")]
        public int Text120FirstRareWords { get; set; }
        [Column("text120_first_rare_words_percentage")]
        public double Text120FirstRareWordsPercentage { get; set; }
        [Column("text120_first_jargon_score")]
        public double Text120FirstJargonScore { get; set; }

        // 60 Words
        [Column("text60")]
        public string? Text60 { get; set; }
        [Column("text60_jargon")]
        public string? Text60Jargon { get; set; }
        [Column("text60_gpt3")]
        public string? Text60GPT3 { get; set; }
        [Column("text60_total_words")]
        public int Text60TotalWords { get; set; }
        [Column("text60_rare_words")]
        public int Text60RareWords { get; set; }
        [Column("text60_rare_words_percentage")]
        public double Text60RareWordsPercentage { get; set; }
        [Column("text60_jargon_score")]
        public double Text60JargonScore { get; set; }

        // 30 Words
        [Column("text30")]
        public string? Text30 { get; set; }
        [Column("text30_jargon")]
        public string? Text30Jargon { get; set; }
        [Column("text30_gpt3")]
        public string? Text30GPT3 { get; set; }
        [Column("text30_total_words")]
        public int Text30TotalWords { get; set; }
        [Column("text30_rare_words")]
        public int Text30RareWords { get; set; }
        [Column("text30_rare_words_percentage")]
        public double Text30RareWordsPercentage { get; set; }
        [Column("text30_jargon_score")]
        public double Text30JargonScore { get; set; }

        // 120 Words - Last
        [Column("text120_last")]
        public string? Text120Last { get; set; }
        [Column("text120_last_total_words")]
        public int Text120LastTotalWords { get; set; }
        [Column("text120_last_rare_words")]
        public int Text120LastRareWords { get; set; }
        [Column("text120_last_rare_words_percentage")]
        public double Text120LastRareWordsPercentage { get; set; }
        [Column("text120_last_jargon_score")]
        public double Text120LastJargonScore { get; set; }
    }

    public class HalfLifeConversationHistory 
    {
        public List<HalfLifeMessage> Messages { get; set; }
        public int CurrentStage { get; set; }

        public string Text120First { get; set; }
        public string Text120FirstJargon { get; set; }
        public string Text120FirstGPT3 { get; set; }
        public int Text120FitrstTotalWords { get; set; }
        public int Text120FitrstRareWords { get; set; }
        public double Text120FitrstRareWordsPercentage { get; set; }
        public double Text120FitrstJargonScore { get; set; }


        public string Text60 { get; set; }
        public string Text60Jargon { get; set; }
        public string Text60GPT3 { get; set; }
        public int Text60TotalWords { get; set; }
        public int Text60RareWords { get; set; }
        public double Text60RareWordsPercentage { get; set; }
        public double Text60JargonScore { get; set; }


        public string Text30 { get; set; }
        public string Text30Jargon { get; set; }
        public string Text30GPT3 { get; set; }
        public int Text30TotalWords { get; set; }
        public int Text30RareWords { get; set; }
        public double Text30RareWordsPercentage { get; set; }
        public double Text30JargonScore { get; set; }


        public string Text120Last { get; set; }
        public int Text120LastTotalWords { get; set; }
        public int Text120LastRareWords { get; set; }
        public double Text120LastRareWordsPercentage { get; set; }
        public double Text120LastJargonScore { get; set; }


        public string WhichIsBetter { get; set; }
        public string TargetAudience { get; set; }
        public DateTime StartTime { get; set; }
        public bool CopyPasteCheck { get; set; }
    }

    public class HalfLifeMessage
    {
        public string Text { get; set; }
        public bool IsStudent { get; set; }  // Indicates the current stage of the conversation
    }

    public HalfLifeController(IHttpClientFactory httpClientFactory, SupabaseClient supabaseClient, ILogger<HalfLifeController> logger, UsageCounter usageCounter, IWebHostEnvironment env)
    {
        _client = httpClientFactory.CreateClient("CustomClient");
        _supabaseClient = supabaseClient;
        _logger = logger;
        _usageCounter = usageCounter;
        _env = env;
    }

    [HttpPost]
    public async Task<IActionResult> ProcessConversation([FromBody] HalfLifeConversationHistory  history)
    {
        try
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();

            var userId = _supabaseClient.GetUserId(authHeader);
            var responseMessages = await DetermineResponse(history, userId);

            // Update history with the response
            history.Messages.AddRange(responseMessages.Select(m => new HalfLifeMessage { Text = m, IsStudent = false }));

            return Ok(new { Messages = responseMessages, UpdatedHistory = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing conversation.");
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }


    private async Task<List<string>> DetermineResponse(HalfLifeConversationHistory  history, string userId)
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

                    //await SaveToGoogleSheets(history);
                    await SaveToSupabase(history, userId);


                    _usageCounter.UpdateNumberOfUses(1);

                    return new List<string> { "We’re done! Hope this was helpful. Now is a good time to further hone your skills at the science communication free online course at edX." };
                }
                else
                {
                    return new List<string> { "Please enter your answer as single digit: " +
                        "<div class='chat-option'>(1) My original text</div>" +
                        "<div class='chat-option'>(2) My revised text</div>" +
                        "<div class='chat-option'>(3) Neither</div>" +
                        "<div class='chat-option'>(4) Both!</div>"};
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

    private async Task SaveToSupabase(HalfLifeConversationHistory  history, string userId)
    {
        var isSaveUserData = await _supabaseClient.getIsSaveUserData(userId);

        var data = new HalfLifeUserInteraction
        {
            UserId = isSaveUserData ? userId : null,
            TargetAudience = history.TargetAudience,
            StartTime = history.StartTime,
            EndTime = DateTime.Now,
            CopyPasteCheck = history.CopyPasteCheck,
            WhichIsBetter = history.WhichIsBetter,

            // 120 Words - First
            Text120First = isSaveUserData ? history.Text120First : null,
            Text120FirstJargon = history.Text120FirstJargon,
            Text120FirstGPT3 = isSaveUserData ? history.Text120FirstGPT3 : null,
            Text120FirstTotalWords = history.Text120FitrstTotalWords,
            Text120FirstRareWords = history.Text120FitrstRareWords,
            Text120FirstRareWordsPercentage = history.Text120FitrstRareWordsPercentage,
            Text120FirstJargonScore = history.Text120FitrstJargonScore,

            // 60 Words
            Text60 = isSaveUserData ? history.Text60 : null,
            Text60Jargon = history.Text60Jargon,
            Text60GPT3 = isSaveUserData ? history.Text60GPT3 : null,
            Text60TotalWords = history.Text60TotalWords,
            Text60RareWords = history.Text60RareWords,
            Text60RareWordsPercentage = history.Text60RareWordsPercentage,
            Text60JargonScore = history.Text60JargonScore,

            // 30 Words
            Text30 = isSaveUserData ? history.Text30 : null,
            Text30Jargon = history.Text30Jargon,// Save Jargon words to DB?
            Text30GPT3 = isSaveUserData ? history.Text30GPT3 : null,
            Text30TotalWords = history.Text30TotalWords,
            Text30RareWords = history.Text30RareWords,
            Text30RareWordsPercentage = history.Text30RareWordsPercentage,
            Text30JargonScore = history.Text30JargonScore,

            // 120 Words - Last
            Text120Last = isSaveUserData ? history.Text120Last : null,
            Text120LastTotalWords = history.Text120LastTotalWords,
            Text120LastRareWords = history.Text120LastRareWords,
            Text120LastRareWordsPercentage = history.Text120LastRareWordsPercentage,
            Text120LastJargonScore = history.Text120LastJargonScore
        };

        try
        {
            await _supabaseClient.client.From<HalfLifeUserInteraction>().Insert(data);
            Debug.WriteLine("Data successfully saved to Supabase.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving data to Supabase: {ex.Message}");
        }
    }

    private async Task<List<string>> ProcessStageAsync(HalfLifeConversationHistory  history, int wordLimit, bool lastRound = false)
    {
        var text = history.Messages.LastOrDefault(x => x.IsStudent)?.Text ?? string.Empty;
        var response = ValidateWordCount(text, wordLimit);

        if (!string.IsNullOrEmpty(response))
        {
            return new List<string> { response };
        }

        history.CurrentStage++;

        TextGrading.Lang = Language.English2021_2024;
        var articleGradingInfo = TextGrading.AnalyzeSingleText(text.Trim(), _env);

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
                    history.Text120FitrstJargonScore = articleGradingInfo.Score;
                    history.Text120FitrstRareWords = articleGradingInfo.RareWords.Count;
                    history.Text120FitrstTotalWords = articleGradingInfo.CleanedWords.Count;
                    history.Text120FitrstRareWordsPercentage = articleGradingInfo.RareWords.Count / (double)articleGradingInfo.CleanedWords.Count;
                }
                else
                {
                    var originalTxt = history.Text120First;
                    var revisedTxt = text;

                    history.Text120Last = text;
                    history.Text120LastJargonScore = articleGradingInfo.Score;
                    history.Text120LastRareWords = articleGradingInfo.RareWords.Count;
                    history.Text120LastTotalWords = articleGradingInfo.CleanedWords.Count;
                    history.Text120LastRareWordsPercentage = articleGradingInfo.RareWords.Count / (double)articleGradingInfo.CleanedWords.Count;

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
                history.Text60JargonScore = articleGradingInfo.Score;
                history.Text60RareWords = articleGradingInfo.RareWords.Count;
                history.Text60TotalWords = articleGradingInfo.CleanedWords.Count;
                history.Text60RareWordsPercentage = articleGradingInfo.RareWords.Count / (double)articleGradingInfo.CleanedWords.Count;

                responses.Add("OK, now let's take it to the next level!<br />Please tell me what you do and why in only 30 words (20-40).");
                break;
            case 30:
                history.Text30 = text;
                history.Text30GPT3 = rephrasedText;
                history.Text30Jargon = string.Join(", ", articleGradingInfo.RareWordsSyns.Keys);
                history.Text30JargonScore = articleGradingInfo.Score;
                history.Text30RareWords = articleGradingInfo.RareWords.Count;
                history.Text30TotalWords = articleGradingInfo.CleanedWords.Count;
                history.Text30RareWordsPercentage = articleGradingInfo.RareWords.Count / (double)articleGradingInfo.CleanedWords.Count;

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
