using System.Diagnostics;
using DeJargonizer2025.Helpers;
using JargonProject.Services;
using Microsoft.AspNetCore.Mvc;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[ApiController]
[Route("api/[controller]")]
public class EthicsController : ControllerBase
{
    private readonly SupabaseClient _supabaseClient;
    private readonly UsageCounter _usageCounter;
    private readonly GPTApiClient _gptApiClient;
    private readonly ILogger<EthicsController> _logger;

    readonly Dictionary<int, (int min, int max)> wordCountRanges = new Dictionary<int, (int min, int max)>
    {
        { 120, (100, 140) },
        { 3, (2, 5) },
        { 10, (20, 60) },
        { 20, (100, 120) },
    };

    public class EthicsConversationHistory
    {
        public List<EthicsMessage> Messages { get; set; }
        public int CurrentStage { get; set; }
        public string OriginalText { get; set; }
        public string MitigateText { get; set; }
        public string FinalText { get; set; }
        public string WhyIsBetter { get; set; }
        public string AdditionalInformation { get; set; }
        public string MitigateTextGpt3 { get; set; }

        public string WhichIsBetter { get; set; }
        public DateTime StartTime { get; set; }
        public bool CopyPasteCheck { get; set; }
    }

    [Table("ethics_user_interactions")]
    public class EthicsUserInteraction : BaseModel
    {
        [Column("user_id")]
        public string? UserId { get; set; }

        [Column("original_text")]
        public string? OriginalText { get; set; }
        [Column("mitigate_text")]
        public string? MitigateText { get; set; }
        [Column("final_text")]
        public string? FinalText { get; set; }
        [Column("why_is_better")]
        public string? WhyIsBetter { get; set; }
        [Column("additional_information")]
        public string? AdditionalInformation { get; set; }

        [Column("mitigate_text_gpt3")]
        public string? MitigateTextGpt3 { get; set; }

        [Column("which_is_better")]
        public string? WhichIsBetter { get; set; }
        [Column("start_time")]
        public DateTime StartTime { get; set; }
        [Column("end_time")]
        public DateTime EndTime { get; set; }
        [Column("copy_paste_check")]
        public bool CopyPasteCheck { get; set; }
    }

    public class EthicsMessage
    {
        public string Text { get; set; }
        public bool IsStudent { get; set; }  // Indicates the current stage of the conversation
    }

    public EthicsController(SupabaseClient supabaseClient, ILogger<EthicsController> logger, UsageCounter usageCounter, GPTApiClient gptApiClient)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
        _usageCounter = usageCounter;
        _gptApiClient = gptApiClient;
    }

    [HttpPost]
    public async Task<IActionResult> ProcessConversation([FromBody] EthicsConversationHistory history)
    {
        try
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();

            var userId = await HttpContext.TryGetUserIdAsync();
            var responseMessages = await DetermineResponse(history, userId);

            // Update history with the response
            history.Messages.AddRange(responseMessages.Select(m => new EthicsMessage { Text = m, IsStudent = false }));

            return Ok(new { Messages = responseMessages, UpdatedHistory = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing conversation.");
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }

    private async Task<List<string>> DetermineResponse(EthicsConversationHistory history, string? userId)
    {
        var lastUserText = history.Messages.LastOrDefault(x => x.IsStudent);

        switch (history.CurrentStage)
        {
            case 1:
                var response = ValidateWordCount(lastUserText.Text, 20);
                history.StartTime = DateTime.Now;

                if (!string.IsNullOrEmpty(response))
                {
                    return new List<string> { response };
                }

                history.CurrentStage++;
                history.OriginalText = lastUserText.Text;

                return new List<string> {
                    "Think about your above description and add 1-2 potential ways to resolve the issue. Your text should be about 120 words.",
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
                history.MitigateTextGpt3 = rephrasedText;

                return new List<string> {
                    "Below there are some suggestions from ChatGPT about potential ethical issues that might arise in your project:",
                    rephrasedText,
                    "Looking at the above suggestions and your texts, please create a final 120-word version of the research aim, ethical issue and potential solution. Type your new, revised version here:",
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
                    "Look back at your original and your final 120-word text. Which version is better in your opinion?" +
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
                        "Why is the version you chose better? Or why are they equal? (20-60 words)"
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
                //await SaveToGoogleSheets(history);
                await SaveToSupabase(history, userId);

                _usageCounter.UpdateNumberOfUses(1);

                return new List<string> {
                    "We hope you have learned more about writing about ethics!",
                    "Thank you!!"
                };

            default:
                history.CurrentStage++;

                return new List<string> {
                    "Think about your research project and an ethical issue that may arise when you want to present this work to the public/stakeholders/academics:",
                    "Briefly describe the research aim and an ethical issue of that project in 100-120 words:",
                };
        }
    }


    private async Task SaveToSupabase(EthicsConversationHistory history, string? userId)
    {
        var isSaveUserData = await _supabaseClient.getIsSaveUserData(userId);

        var data = new EthicsUserInteraction
        {
            UserId = isSaveUserData ? userId : null,
            StartTime = history.StartTime,
            EndTime = DateTime.Now,
            CopyPasteCheck = history.CopyPasteCheck,
            WhichIsBetter = history.WhichIsBetter,

            MitigateTextGpt3 = isSaveUserData ? history.MitigateTextGpt3 : null,
            OriginalText = isSaveUserData ? history.OriginalText : null,
            MitigateText = isSaveUserData ? history.MitigateText : null,
            FinalText = isSaveUserData ? history.FinalText : null,
            WhyIsBetter = isSaveUserData ? history.WhyIsBetter : null,
            AdditionalInformation = isSaveUserData ? history.AdditionalInformation : null,
        };

        try
        {
            await _supabaseClient.client.From<EthicsUserInteraction>().Insert(data);
            Debug.WriteLine("Data successfully saved to Supabase.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving data to Supabase: {ex.Message}");
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

    private async Task<string> RephraseText(string text)
    {
        string prompt = $"Here is a brief academic description of a research project and an ethical issue that may arise: {text}. What other questions or issues might arise in such a project?";

        return await _gptApiClient.RephraseText(prompt);
    }
}
