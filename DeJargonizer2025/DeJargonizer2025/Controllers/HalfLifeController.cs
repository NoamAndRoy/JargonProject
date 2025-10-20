using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using DeJargonizer2025.Helpers;
using JargonProject.Handlers;
using JargonProject.Models;
using JargonProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using static JargonProject.Controllers.TextGradingController;

[ApiController]
[Route("api/[controller]")]
public class HalfLifeController : ControllerBase
{
    private readonly SupabaseClient _supabaseClient;
    private readonly HttpClient _client;
    private readonly ILogger<HalfLifeController> _logger;
    private readonly UsageCounter _usageCounter;
    private readonly GPTApiClient _gptApiClient;
    private readonly IWebHostEnvironment _env;
    private readonly GoogleSheetsService _googleSheetsService;
    private readonly string _fullFeedbackSheetName;
    private readonly string _noFeedbackSheetName;
    private readonly string? _spreadsheetId;

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
        public bool isResearch { get; set; }

        public string? StudentName { get; set; }
        public string? TaskId { get; set; }

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

    public HalfLifeController(IHttpClientFactory httpClientFactory, SupabaseClient supabaseClient, ILogger<HalfLifeController> logger,
                                UsageCounter usageCounter, IWebHostEnvironment env, GPTApiClient gptApiClient,
                                GoogleSheetsService googleSheetsService,
                                IConfiguration configuration)
    {
        _client = httpClientFactory.CreateClient("CustomClient");
        _supabaseClient = supabaseClient;
        _logger = logger;
        _usageCounter = usageCounter;
        _env = env;
        _gptApiClient = gptApiClient;
        _googleSheetsService = googleSheetsService;
        _fullFeedbackSheetName = Environment.GetEnvironmentVariable("HALFLIFE_SHEET_FULL") ?? "results-jargon-ai";
        _noFeedbackSheetName = Environment.GetEnvironmentVariable("HALFLIFE_SHEET_NO_FEEDBACK") ?? "results-basic";
        _spreadsheetId = Environment.GetEnvironmentVariable("HALFLIFE_SPREADSHEET_ID")
                         ?? configuration["GoogleSheets:SpreadsheetId"];
    }

    [HttpPost]
    public async Task<IActionResult> ProcessConversation([FromBody] HalfLifeConversationHistory history)
    {
        try
        {
            var userId = await HttpContext.TryGetUserIdAsync();
            var responseMessages = await DetermineResponse(history, userId, includeFeedback: true);

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

    [HttpPost("no-feedback")]
    public async Task<IActionResult> ProcessConversationWithoutFeedback([FromBody] HalfLifeConversationHistory history)
    {
        try
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            var userId = await HttpContext.TryGetUserIdAsync();
            var responseMessages = await DetermineResponse(history, userId, includeFeedback: false);

            history.Messages.AddRange(responseMessages.Select(m => new HalfLifeMessage { Text = m, IsStudent = false }));

            return Ok(new { Messages = responseMessages, UpdatedHistory = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing conversation without feedback.");
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }


    private async Task<List<string>> DetermineResponse(
        HalfLifeConversationHistory history,
        string? userId,
        bool includeFeedback)
    {
        bool isResearch = history.isResearch;

        history.Messages ??= new List<HalfLifeMessage>();
        var lastUserMessage = history.Messages.LastOrDefault(x => x.IsStudent)?.Text;

        // helper to avoid duplicating the audience prompt
        static string AudiencePrompt() =>
            "Before we dive into the writing process, could you please provide some insight into the science background of your intended audience? This will assist the AI in tailoring appropriate suggestions for your task:" +
            "<div class='chat-option'>(1) Elementary (primary) school level science (learned science until they were 12 years old)</div>" +
            "<div class='chat-option'>(2) Junior high school level science (learned science until they were 15 years old)</div>" +
            "<div class='chat-option'>(3) High school level science (learned science until they were 18 years old)</div>" +
            "<div class='chat-option'>(4) Adult audience with mixed background</div>";

        switch (history.CurrentStage)
        {
            case 0:
                if (isResearch)
                {
                    history.CurrentStage = 1;
                    return new List<string>
                {
                    "Welcome to the Half-Life writing exercise! We're glad you're here.",
                    "Before we begin, please share your full name so we can save your progress for the research study."
                };
                }
                else
                {
                    // Skip name step when not in research mode
                    history.CurrentStage = 2;
                    return new List<string>
                {
                    "Welcome to the Half-Life writing exercise! We're glad you're here.",
                    AudiencePrompt()
                };
                }

            case 1:
                // If somehow we reached stage 1 while not in research mode, skip to audience.
                if (!isResearch)
                {
                    history.CurrentStage = 2;
                    return new List<string> { AudiencePrompt() };
                }

                if (!string.IsNullOrWhiteSpace(lastUserMessage))
                {
                    history.StudentName = lastUserMessage.Trim();
                    history.CurrentStage = 2;

                    var thankYou = string.IsNullOrWhiteSpace(history.StudentName)
                        ? "Thanks!"
                        : $"Thanks, {history.StudentName}!";

                    return new List<string> { thankYou, AudiencePrompt() };
                }

                return new List<string> { "Please share your name so we can continue." };

            case 2:
                if (ValidateUserRespose(lastUserMessage, new List<string> { "1", "2", "3", "4" }))
                {
                    history.StartTime = DateTime.UtcNow;
                    history.CurrentStage = 3;

                    switch (lastUserMessage!.Trim())
                    {
                        case "1": history.TargetAudience = "Elementary"; break;
                        case "2": history.TargetAudience = "Junior"; break;
                        case "3": history.TargetAudience = "High"; break;
                        case "4": history.TargetAudience = "Adult"; break;
                    }

                    return new List<string>
                {
                    "Please tell me what you study and why it is important.<br />Use about 120 words (equivalent to about one minute of speech)"
                };
                }

                return new List<string>
            {
                "Please enter your answer as single digit: " +
                "<div class='chat-option'>(1) Elementary (primary) school level science (learned science until they were 12 years old)</div>" +
                "<div class='chat-option'>(2) Junior high school level science (learned science until they were 15 years old)</div>" +
                "<div class='chat-option'>(3) High school level science (learned science until they were 18 years old)</div>" +
                "<div class='chat-option'>(4) Adult audience with mixed background</div>"
            };

            case 3:
                return await ProcessStageAsync(history, 120, includeFeedback);
            case 4:
                return await ProcessStageAsync(history, 60, includeFeedback);
            case 5:
                return await ProcessStageAsync(history, 30, includeFeedback);
            case 6:
                return await ProcessStageAsync(history, 120, includeFeedback, true);

            case 7:
                if (ValidateUserRespose(lastUserMessage, new List<string> { "1", "2", "3", "4" }))
                {
                    switch (lastUserMessage!.Trim())
                    {
                        case "1": history.WhichIsBetter = "Original"; break;
                        case "2": history.WhichIsBetter = "Revised"; break;
                        case "3": history.WhichIsBetter = "Neither"; break;
                        case "4": history.WhichIsBetter = "Both"; break;
                    }

                    history.CurrentStage++;

                    history.TaskId = await SaveResultsAsync(history, userId, includeFeedback, isResearch);
                    _usageCounter.UpdateNumberOfUses(1);

                    return new List<string>
                {
                    "We’re done! Hope this was helpful. Now is a good time to further hone your skills at the science communication free online course at edX."
                };
                }

                return new List<string>
            {
                "Please enter your answer as single digit: " +
                "<div class='chat-option'>(1) My original text</div>" +
                "<div class='chat-option'>(2) My revised text</div>" +
                "<div class='chat-option'>(3) Neither</div>" +
                "<div class='chat-option'>(4) Both!</div>"
            };

            default:
                history.CurrentStage = 0;
                return await DetermineResponse(history, userId, includeFeedback);
        }
    }

    private async Task<string?> SaveResultsAsync(HalfLifeConversationHistory history, string userId, bool includeFeedback, bool isResearch)
    {
        if (isResearch)
        {
            var sheetName = includeFeedback ? _fullFeedbackSheetName : _noFeedbackSheetName;
            await SaveToGoogleSheetsAsync(history, sheetName, includeFeedback);
            return null;
        }

        string? taskId = null;

        try
        {
            taskId = await SaveToSupabase(history, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save HalfLife session to Supabase");
        }

        return taskId;
    }

    private async Task<string?> SaveToSupabase(HalfLifeConversationHistory history, string? userId)
    {
        var isSaveUserData = await _supabaseClient.getIsSaveUserData(userId);

        var data = new HalfLifeUserInteraction
        {
            UserId = isSaveUserData ? userId : null,
            TargetAudience = history.TargetAudience,
            StartTime = history.StartTime,
            EndTime = DateTime.UtcNow,
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
            var result = await _supabaseClient.client.From<HalfLifeUserInteraction>().Insert(data);
            Debug.WriteLine("Data successfully saved to Supabase.");

            using JsonDocument doc = JsonDocument.Parse(result.Content);
            string id = doc.RootElement[0].GetProperty("id").GetString();

            return id;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving data to Supabase: {ex.Message}");
        }

        return null;
    }

    private async Task SaveToGoogleSheetsAsync(HalfLifeConversationHistory history, string sheetName, bool includeFeedback)
    {
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            _logger.LogWarning("Google Sheets sheet name is not configured for this Half-Life variant.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_spreadsheetId))
        {
            _logger.LogWarning("Google Sheets spreadsheet id is not configured for Half-Life.");
            return;
        }

        try
        {
            var ip = IpHelper.GetClientIp(HttpContext);
            var geoInfo = await GetGeoInfoFromIpAsync(ip);

            var timestamp = DateTime.UtcNow;

            List<object> values;

            if (includeFeedback)
            {
                values = new List<object>
            {
                timestamp.ToString("dd/MM/yyyy"),
                timestamp.ToString("HH:mm:ss"),
                history.StudentName ?? string.Empty,
                ip,
                geoInfo.Country,
                geoInfo.Region,
                geoInfo.City,
                history.TargetAudience ?? string.Empty,
                history.Text120First ?? string.Empty,
                history.Text120FirstJargon ?? string.Empty,
                history.Text120FirstGPT3 ?? string.Empty,
                history.Text60 ?? string.Empty,
                history.Text60Jargon ?? string.Empty,
                history.Text60GPT3 ?? string.Empty,
                history.Text30 ?? string.Empty,
                history.Text30Jargon ?? string.Empty,
                history.Text30GPT3 ?? string.Empty,
                history.Text120Last ?? string.Empty,
                history.WhichIsBetter ?? string.Empty,
            };
            }
            else
            {
                values = new List<object>
            {
                timestamp.ToString("dd/MM/yyyy"),
                timestamp.ToString("HH:mm:ss"),
                history.StudentName ?? string.Empty,
                ip,
                geoInfo.Country,
                geoInfo.Region,
                geoInfo.City,
                history.TargetAudience ?? string.Empty,
                history.Text120First ?? string.Empty,
                history.Text60 ?? string.Empty,
                history.Text30 ?? string.Empty,
                history.Text120Last ?? string.Empty,
                history.WhichIsBetter ?? string.Empty,
            };
            }

            await _googleSheetsService.AppendRowAsync(_spreadsheetId, sheetName, values);
            Debug.WriteLine("Data successfully saved to Google Sheets.");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Half-Life interaction to Google Sheets");
        }
    }

    private async Task<(string Country, string Region, string City)> GetGeoInfoFromIpAsync(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || ip == "Unknown" || ip == "127.0.0.1" || ip == "::1")
        {
            return ("Unknown", "Unknown", "Unknown");
        }

        try
        {
            var response = await _client.GetStringAsync($"http://ip-api.com/json/{ip}?fields=status,country,regionName,city");
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            if (root.TryGetProperty("status", out var statusElement) && statusElement.GetString() == "success")
            {
                var country = root.TryGetProperty("country", out var countryElement) ? countryElement.GetString() ?? "Unknown" : "Unknown";
                var region = root.TryGetProperty("regionName", out var regionElement) ? regionElement.GetString() ?? "Unknown" : "Unknown";
                var city = root.TryGetProperty("city", out var cityElement) ? cityElement.GetString() ?? "Unknown" : "Unknown";

                return (country, region, city);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to resolve geo information for IP {Ip}", ip);
        }

        return ("Unknown", "Unknown", "Unknown");
    }

    private async Task<List<string>> ProcessStageAsync(HalfLifeConversationHistory history, int wordLimit, bool includeFeedback, bool lastRound = false)
    {
        var text = history.Messages.LastOrDefault(x => x.IsStudent)?.Text ?? string.Empty;
        var response = ValidateWordCount(text, wordLimit);

        if (!string.IsNullOrEmpty(response))
        {
            return new List<string> { response };
        }

        history.CurrentStage++;

        if (!includeFeedback)
        {
            return ProcessStageWithoutFeedback(history, wordLimit, text, lastRound);
        }

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
                    $"<span class='rare-word' title='Optional replacemets: {GenerateReplacementSyns(articleGradingInfo, w)}.'>{w}</span>"
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

                    return new List<string>
                    {
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

    private List<string> ProcessStageWithoutFeedback(HalfLifeConversationHistory history, int wordLimit, string text, bool lastRound)
    {
        var responses = new List<string>();

        switch (wordLimit)
        {
            case 120:
                if (!lastRound)
                {
                    responses.Add("Thank you for sharing your explanation.");
                    responses.Add("Now please tell me again what you do and why it is important - but this time use only 60 words! (45-75)");

                    history.Text120First = text;
                    history.Text120FirstGPT3 = string.Empty;
                    history.Text120FirstJargon = string.Empty;
                    history.Text120FitrstJargonScore = 0;
                    history.Text120FitrstRareWords = 0;
                    history.Text120FitrstTotalWords = CountWords(text);
                    history.Text120FitrstRareWordsPercentage = 0;
                }
                else
                {
                    var originalTxt = history.Text120First;
                    var revisedTxt = text;

                    history.Text120Last = text;
                    history.Text120LastJargonScore = 0;
                    history.Text120LastRareWords = 0;
                    history.Text120LastTotalWords = CountWords(text);
                    history.Text120LastRareWordsPercentage = 0;

                    return new List<string>
                    {
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
                responses.Add("Nice job tightening your explanation.");
                responses.Add("OK, now let's take it to the next level!<br />Please tell me what you do and why in only 30 words (20-40).");

                history.Text60 = text;
                history.Text60GPT3 = string.Empty;
                history.Text60Jargon = string.Empty;
                history.Text60JargonScore = 0;
                history.Text60RareWords = 0;
                history.Text60TotalWords = CountWords(text);
                history.Text60RareWordsPercentage = 0;
                break;
            case 30:
                responses.Add("That was concise!");
                responses.Add("You now get all of your 120 words back. Please tell me what you study and why it is important using 120 words.");

                history.Text30 = text;
                history.Text30GPT3 = string.Empty;
                history.Text30Jargon = string.Empty;
                history.Text30JargonScore = 0;
                history.Text30RareWords = 0;
                history.Text30TotalWords = CountWords(text);
                history.Text30RareWordsPercentage = 0;
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

    private int CountWords(string text)
    {
        return text.Split(new char[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private string ValidateWordCount(string text, int wordLimit)
    {
        int wordCount = CountWords(text);
        Debug.WriteLine($"text: " + text);

        if (wordCount < wordCountRanges[wordLimit].min || wordCount > wordCountRanges[wordLimit].max)
        {
            return $"Please use {wordCountRanges[wordLimit].min}-{wordCountRanges[wordLimit].max} words.<br />Currently, you used {wordCount}.";
        }
        return null; // No error, proceed with normal flow
    }

    private bool ValidateUserRespose(string lastUserText, List<string> possibleResponses)
    {
        if (string.IsNullOrWhiteSpace(lastUserText))
        {
            return false;
        }

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

        return await _gptApiClient.RephraseText($"{prompt} \n\n{text}");
    }
}
