using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DeJargonizer2025.Helpers;
using JargonProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[ApiController]
[Route("api/[controller]")]
public class CriticalThinkingController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SupabaseClient _supabaseClient;
    private readonly ILogger<CriticalThinkingController> _logger;
    private readonly GoogleSheetsService _googleSheetsService;
    private readonly GPTApiClient _gptApiClient;
    private readonly string _spreadsheetId;
    private readonly string _feedbackSheetName;
    private readonly string _noFeedbackSheetName;

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
        public string ReflectionOpenResponse { get; set; }

        public string TaskId { get; set; }
        public bool FeedbackEnabled { get; set; }
    }

    public class Message
    {
        public string Text { get; set; }
        public bool IsStudent { get; set; }
    }

    [Table("critical_thinking_user_interactions")]
    public class CriticalThinkingUserInteraction : BaseModel
    {
        [Column("user_id")]
        public string? UserId { get; set; }

        [Column("start_time")]
        public DateTime StartTime { get; set; }

        [Column("end_time")]
        public DateTime EndTime { get; set; }

        [Column("initial_summary")]
        public string? InitialSummary { get; set; }

        [Column("question1_answer")]
        public string? Question1Answer { get; set; }

        [Column("revision_after_question1")]
        public string? RevisionAfterQuestion1 { get; set; }

        [Column("feedback1")]
        public string? Feedback1 { get; set; }

        [Column("question2_answer")]
        public string? Question2Answer { get; set; }

        [Column("revision_after_question2")]
        public string? RevisionAfterQuestion2 { get; set; }

        [Column("feedback2")]
        public string? Feedback2 { get; set; }

        [Column("question3_answer")]
        public string? Question3Answer { get; set; }

        [Column("revision_after_question3")]
        public string? RevisionAfterQuestion3 { get; set; }

        [Column("feedback3")]
        public string? Feedback3 { get; set; }

        [Column("question4_answer")]
        public string? Question4Answer { get; set; }

        [Column("revision_after_question4")]
        public string? RevisionAfterQuestion4 { get; set; }

        [Column("feedback4")]
        public string? Feedback4 { get; set; }

        [Column("question5_answer")]
        public string? Question5Answer { get; set; }

        [Column("revision_after_question5")]
        public string? RevisionAfterQuestion5 { get; set; }

        [Column("feedback5")]
        public string? Feedback5 { get; set; }

        [Column("final_summary")]
        public string? FinalSummary { get; set; }

        [Column("reflection_answer1")]
        public string? ReflectionAnswer1 { get; set; }

        [Column("reflection_answer2")]
        public string? ReflectionAnswer2 { get; set; }

        [Column("reflection_answer3")]
        public string? ReflectionAnswer3 { get; set; }

        [Column("reflection_open_response")]
        public string? ReflectionOpenResponse { get; set; }
    }

    public CriticalThinkingController(
        IHttpClientFactory httpClientFactory,
        SupabaseClient supabaseClient,
        ILogger<CriticalThinkingController> logger,
        IConfiguration configuration,
        GoogleSheetsService googleSheetsService,
        GPTApiClient gptApiClient)
    {
        _httpClientFactory = httpClientFactory;
        _supabaseClient = supabaseClient;
        _logger = logger;
        _googleSheetsService = googleSheetsService;
        _gptApiClient = gptApiClient;

        _spreadsheetId = configuration["CriticalThinkingChatbot:SpreadsheetId"]
            ?? Environment.GetEnvironmentVariable("CRITICAL_THINKING_SPREADSHEET_ID")
            ?? string.Empty;

        _feedbackSheetName = configuration["CriticalThinkingChatbot:SheetNameWithFeedback"]
            ?? Environment.GetEnvironmentVariable("CRITICAL_THINKING_SHEET_NAME_WITH_FEEDBACK")
            ?? string.Empty;

        _noFeedbackSheetName = configuration["CriticalThinkingChatbot:SheetNameWithoutFeedback"]
            ?? Environment.GetEnvironmentVariable("CRITICAL_THINKING_SHEET_NAME_NO_FEEDBACK")
            ?? string.Empty;
    }

    [HttpPost]
    public async Task<IActionResult> ProcessConversation([FromBody] ConversationHistory history)
    {
        try
        {
            history.Messages ??= new List<Message>();

            var userId = await HttpContext.TryGetUserIdAsync();

            var responseMessages = await DetermineResponse(history, userId, includeFeedback: true);

            history.Messages.AddRange(responseMessages.Select(m => new Message { Text = m, IsStudent = false }));

            return Ok(new { Messages = responseMessages, UpdatedHistory = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing critical-thinking conversation");
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }

    [HttpPost("no-feedback")]
    public async Task<IActionResult> ProcessConversationWithoutFeedback([FromBody] ConversationHistory history)
    {
        try
        {
            history.Messages ??= new List<Message>();

            var userId = await HttpContext.TryGetUserIdAsync();

            var responseMessages = await DetermineResponse(history, userId, includeFeedback: false);

            history.Messages.AddRange(responseMessages.Select(m => new Message { Text = m, IsStudent = false }));

            return Ok(new { Messages = responseMessages, UpdatedHistory = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing critical-thinking conversation without feedback");
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }

    private async Task<List<string>> DetermineResponse(ConversationHistory history, string? userId, bool includeFeedback)
    {
        history.FeedbackEnabled = includeFeedback;
        var lastStudentMessage = history.Messages.LastOrDefault(m => m.IsStudent)?.Text?.Trim();

        switch (history.CurrentStage)
        {
            case 0:
                history.CurrentStage = history.isResearch ? 1 : 2;

                var greeting = new List<string>
                {
                    "Hi, I’m your academic writing assistant. My main focus is helping you improve critical thinking in your writing. Critical thinking helps improve academic writing by guiding writers to clarify their purpose, formulate clear questions, evaluate the accuracy and relevance of information, and present logical conclusions to build well-reasoned arguments. Please upload or paste your text, and I’ll guide you by asking questions that help you track and refine your writing."
                };

                if (history.isResearch)
                {
                    greeting.Add("Please provide your full name so we can save your data for the research study.");
                }
                else
                {
                    history.ParticipantName = null;
                    history.StartTime = DateTime.UtcNow;
                    greeting.Add("Please provide a 150-500-word summary of your research before starting the first question.");
                }

                return greeting;

            case 1:
                if (!history.isResearch)
                {
                    history.StartTime = DateTime.UtcNow;
                    history.CurrentStage = 2;
                    return new List<string>
                    {
                        "Please provide a 150-500-word summary of your research before starting the first question."
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
                    "Please provide a 150-500-word summary of your research before starting the first question."
                };

            case 2:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please provide a 150-500-word summary of your research before starting the first question." };
                }

                var summaryValidation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (summaryValidation != null)
                {
                    return new List<string> { summaryValidation };
                }

                history.InitialSummary = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 3;
                return new List<string>
                {
                    "Question 1: Please copy the relevant sentence/s to best answer the question: The main purpose of this text is . . . (Here you are trying to state as accurately as possible your purpose for writing the article. What were you trying to accomplish?)",
                    "Answer (20-70 words)"
                };

            case 3:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please describe the main purpose in 20-70 words." };
                }

                var q1Validation = ValidateWordCount(lastStudentMessage, 20, 70);
                if (q1Validation != null)
                {
                    return new List<string> { q1Validation };
                }

                history.Question1Answer = lastStudentMessage;

                if (includeFeedback)
                {
                    history.Feedback1 = await GenerateFeedbackAsync(1, history);
                }
                else
                {
                    history.Feedback1 = null;
                }

                history.CurrentStage = 4;

                var responses1 = new List<string>();

                if (includeFeedback && !string.IsNullOrWhiteSpace(history.Feedback1))
                {
                    responses1.Add(history.Feedback1);
                }

                var revisionPrompt1 = includeFeedback
                    ? "These are some suggestionst to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant. Please make sure the purpose is stated clearly. (150-500 words)"
                    : "Please write a revised version (150–500 words), using your own ideas and insights about clarifying the purpose.";

                responses1.Add(revisionPrompt1);

                return responses1;

            case 4:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please provide a 150–500-word revision." };
                }

                var revision1Validation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (revision1Validation != null)
                {
                    return new List<string> { revision1Validation };
                }

                history.RevisionAfterQuestion1 = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 5;
                return new List<string>
                {
                    "Question 2: Please answer the question as best as possible in 1-2 sentences: The key research question(s) (whether stated or unstated) at issue for this research is/are . . . (Your goal is to figure out the key question that was in your mind when you wrote the article)",
                    "Answer (20-70 words)"
                };

            case 5:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please state the key research question(s) in 20-70 words." };
                }

                var q2Validation = ValidateWordCount(lastStudentMessage, 20, 70);
                if (q2Validation != null)
                {
                    return new List<string> { q2Validation };
                }

                history.Question2Answer = lastStudentMessage;

                if (includeFeedback)
                {
                    history.Feedback2 = await GenerateFeedbackAsync(2, history);
                }
                else
                {
                    history.Feedback2 = null;
                }

                history.CurrentStage = 6;

                var responses2 = new List<string>();

                if (includeFeedback && !string.IsNullOrWhiteSpace(history.Feedback2))
                {
                    responses2.Add(history.Feedback2);
                }

                var revisionPrompt2 = includeFeedback
                    ? "These are some suggestionst to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant. Please try to make sure the key questions are stated clearly. (150-500 words)"
                    : "Please write a revised version (150-500 words), drawing on your own ideas to clarify the key research questions.";

                responses2.Add(revisionPrompt2);

                return responses2;

            case 6:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please share a 150-500-word revision." };
                }

                var revision2Validation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (revision2Validation != null)
                {
                    return new List<string> { revision2Validation };
                }

                history.RevisionAfterQuestion2 = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 7;
                return new List<string>
                {
                    "Question 3: Please answer the question as best as possible in 1-2 sentences: The most important information in this text is . . . (You want to identify the key information you used, or presupposed, in the full article you are writing to support your main arguments. Here you are looking for facts, experiences, data you are using to support your conclusions).",
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

                if (includeFeedback)
                {
                    history.Feedback3 = await GenerateFeedbackAsync(3, history);
                }
                else
                {
                    history.Feedback3 = null;
                }

                history.CurrentStage = 8;

                var responses3 = new List<string>();

                if (includeFeedback && !string.IsNullOrWhiteSpace(history.Feedback3))
                {
                    responses3.Add(history.Feedback3);
                }

                var revisionPrompt3 = includeFeedback
                    ? "These are some suggestionst to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant. Please make sure the key information is stated clearly. (150-500 words)"
                    : "Please write a revised version (150-500 words), focusing on clearly presenting the key information you identified.";

                responses3.Add(revisionPrompt3);

                return responses3;

            case 8:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please share a 150-500-word revision." };
                }

                var revision3Validation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (revision3Validation != null)
                {
                    return new List<string> { revision3Validation };
                }

                history.RevisionAfterQuestion3 = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 9;
                return new List<string>
                {
                    "Question 4: Please answer the question as best as possible in 1-2 sentences: The main conclusion(s) in this text is/are. . . (You want to identify the most important conclusions that you came to and presented/will present in the full article you are writing).",
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

                if (includeFeedback)
                {
                    history.Feedback4 = await GenerateFeedbackAsync(4, history);
                }
                else
                {
                    history.Feedback4 = null;
                }

                history.CurrentStage = 10;

                var responses4 = new List<string>();

                if (includeFeedback && !string.IsNullOrWhiteSpace(history.Feedback4))
                {
                    responses4.Add(history.Feedback4);
                }

                var revisionPrompt4 = includeFeedback
                    ? "These are some suggestionst to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant. Please make sure the conclusion is stated clearly. (150-500 words)"
                    : "Please write a revised version (150-500 words), making sure your conclusions are clearly stated using your own insights.";

                responses4.Add(revisionPrompt4);

                return responses4;

            case 10:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please share a 150-500-word revision." };
                }

                var revision4Validation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (revision4Validation != null)
                {
                    return new List<string> { revision4Validation };
                }

                history.RevisionAfterQuestion4 = lastStudentMessage;
                history.CurrentSummary = lastStudentMessage;
                history.CurrentStage = 11;
                return new List<string>
                {
                    "Question 5: Please answer the question as best as possible in 1-2 sentences: What is the significance of the text in the broader context? (Ask yourself: How does this work contribute to the field, and why does it matter?).",
                    "Answer (20-70 words)"
                };

            case 11:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<string> { "Please explain the broader significance in 20-70 words." };
                }

                var q5Validation = ValidateWordCount(lastStudentMessage, 20, 70);
                if (q5Validation != null)
                {
                    return new List<string> { q5Validation };
                }

                history.Question5Answer = lastStudentMessage;

                if (includeFeedback)
                {
                    history.Feedback5 = await GenerateFeedbackAsync(5, history);
                }
                else
                {
                    history.Feedback5 = null;
                }

                history.FinalSummary = history.CurrentSummary;
                history.CurrentStage = 12;

                var responses5 = new List<string>();

                if (includeFeedback && !string.IsNullOrWhiteSpace(history.Feedback5))
                {
                    responses5.Add(history.Feedback5);
                }

                responses5.Add("Thank you! We hope you learned about improving your academic texts with the help of critical thinking!");
                responses5.Add("The final step is to answer three short, close-ended reflection questions about the chatbot use and one optional open question. For each statement, please select an option from a 5-point Likert scale where 1 = strongly disagree and 5 = strongly agree.");
                responses5.Add(BuildLikertOptionsMessage("Reflection 1: The chatbot was friendly and easy to interact with."));

                return responses5;

            case 12:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<string> { "Please reply with a single number from 1 (strongly disagree) to 5 (strongly agree)." };
                }

                history.ReflectionAnswer1 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 13;
                return new List<string>
                {
                    BuildLikertOptionsMessage("Reflection 2: I found the chatbot challenging in a way that stimulated my critical thinking.")
                };

            case 13:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<string> { "Please reply with a single number from 1 to 5." };
                }

                history.ReflectionAnswer2 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 14;
                return new List<string>
                {
                    BuildLikertOptionsMessage("Reflection 3: The chatbot was useful for improving the quality of my writing.")
                };

            case 14:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<string> { "Please reply with a single number from 1 to 5." };
                }

                history.ReflectionAnswer3 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 15;
                return new List<string>
                {
                    "How did you feel while using the chatbot during your writing task? How did it help you, if it did? What did you learn about critical thinking and improving your academic writing?"
                };

            case 15:
                history.ReflectionOpenResponse = lastStudentMessage ?? string.Empty;
                history.CurrentStage = 16;

                if (history.isResearch)
                {
                    await SaveToGoogleSheets(history, userId, includeFeedback);
                }
                else
                {
                    history.TaskId = await SaveToSupabase(history, userId);
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

    private string BuildLikertOptionsMessage(string prefix = null)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(prefix))
        {
            builder.Append(prefix).Append(" ");
        }

        builder.Append("Please type one of the following options:" +
                       "<div class='chat-option'>(1) Strongly disagree</div>" +
                       "<div class='chat-option'>(2) Disagree</div>" +
                       "<div class='chat-option'>(3) Neutral</div>" +
                       "<div class='chat-option'>(4) Agree</div>" +
                       "<div class='chat-option'>(5) Strongly agree</div>");

        return builder.ToString();
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
        var trimmed = input?.Trim();
        return trimmed != null && new List<string> { "1", "2", "3", "4", "5" }.Any(r => r == trimmed);
    }

    private string NormalizeLikertChoice(string input)
    {
        return input?.Trim() ?? string.Empty;
    }

    private async Task<string> GenerateFeedbackAsync(int questionNumber, ConversationHistory history)
    {
        try
        {
            var prompt = BuildFeedbackPrompt(questionNumber, history);
            return await _gptApiClient.RephraseText(prompt);
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
                sb.AppendLine("Student's initial summary (150-500 words):");
                sb.AppendLine(history.InitialSummary ?? string.Empty);
                sb.AppendLine("Student's answer about the main purpose (20-70 words):");
                sb.AppendLine(history.Question1Answer ?? string.Empty);
                sb.AppendLine("Evaluate whether the summary accurately presents the purpose of the research and offer suggestions to clarify the purpose if needed.");
                break;
            case 2:
                sb.AppendLine("Student's revised summary (after Question 1, 150-500 words):");
                sb.AppendLine(history.RevisionAfterQuestion1 ?? history.CurrentSummary ?? string.Empty);
                sb.AppendLine("Student's key research questions answer (20-70 words):");
                sb.AppendLine(history.Question2Answer ?? string.Empty);
                sb.AppendLine("Assess whether the research questions are clear and aligned with the stated purpose. Suggest ways to strengthen clarity and alignment.Give one example of an improved research question.");
                break;
            case 3:
                sb.AppendLine("Student's revised summary (after Question 2, 150-500 words):");
                sb.AppendLine(history.RevisionAfterQuestion2 ?? history.CurrentSummary ?? string.Empty);
                sb.AppendLine("Student's description of key information (30-100 words):");
                sb.AppendLine(history.Question3Answer ?? string.Empty);
                sb.AppendLine("Comment on how well the key information supports the research questions and whether additional facts/data are needed.");
                break;
            case 4:
                sb.AppendLine("Student's revised summary (after Question 3, 150-500 words):");
                sb.AppendLine(history.RevisionAfterQuestion3 ?? history.CurrentSummary ?? string.Empty);
                sb.AppendLine("Student's conclusions answer (30-90 words):");
                sb.AppendLine(history.Question4Answer ?? string.Empty);
                sb.AppendLine("Do you think the conclusions clearly address the stated research questions and goals? Are there any claims in the conclusion that are not supported by the information presented? Does it leave the reader with a clear understanding of how the study advances knowledge in the field?");
                break;
            case 5:
                sb.AppendLine("Student's revised summary (after Question 4, 150-500 words):");
                sb.AppendLine(history.RevisionAfterQuestion4 ?? history.CurrentSummary ?? string.Empty);
                sb.AppendLine("Student's explanation of broader significance (20-70 words):");
                sb.AppendLine(history.Question5Answer ?? string.Empty);
                sb.AppendLine("Do you think the work of the student advances knowledge beyond what is already known? Does it make a novel contribution to theory, methodology, or practice? Could it inform policy, future research or practice in related areas?");
                break;
        }

        return sb.ToString();
    }

    private async Task<string?> SaveToSupabase(ConversationHistory history, string? userId)
    {
        try
        {
            var isSaveUserData = await _supabaseClient.getIsSaveUserData(userId);

            var data = new CriticalThinkingUserInteraction
            {
                UserId = isSaveUserData ? userId : null,
                StartTime = history.StartTime == default ? DateTime.UtcNow : history.StartTime,
                EndTime = DateTime.UtcNow,
                InitialSummary = isSaveUserData ? history.InitialSummary : null,
                Question1Answer = isSaveUserData ? history.Question1Answer : null,
                RevisionAfterQuestion1 = isSaveUserData ? history.RevisionAfterQuestion1 : null,
                Feedback1 = isSaveUserData ? history.Feedback1 : null,
                Question2Answer = isSaveUserData ? history.Question2Answer : null,
                RevisionAfterQuestion2 = isSaveUserData ? history.RevisionAfterQuestion2 : null,
                Feedback2 = isSaveUserData ? history.Feedback2 : null,
                Question3Answer = isSaveUserData ? history.Question3Answer : null,
                RevisionAfterQuestion3 = isSaveUserData ? history.RevisionAfterQuestion3 : null,
                Feedback3 = isSaveUserData ? history.Feedback3 : null,
                Question4Answer = isSaveUserData ? history.Question4Answer : null,
                RevisionAfterQuestion4 = isSaveUserData ? history.RevisionAfterQuestion4 : null,
                Feedback4 = isSaveUserData ? history.Feedback4 : null,
                Question5Answer = isSaveUserData ? history.Question5Answer : null,
                RevisionAfterQuestion5 = isSaveUserData ? history.RevisionAfterQuestion5 : null,
                Feedback5 = isSaveUserData ? history.Feedback5 : null,
                FinalSummary = isSaveUserData ? history.FinalSummary : null,
                ReflectionAnswer1 = history.ReflectionAnswer1,
                ReflectionAnswer2 = history.ReflectionAnswer2,
                ReflectionAnswer3 = history.ReflectionAnswer3,
                ReflectionOpenResponse = isSaveUserData ? history.ReflectionOpenResponse : null
            };

            var result = await _supabaseClient.client.From<CriticalThinkingUserInteraction>().Insert(data);

            using var doc = JsonDocument.Parse(result.Content);
            return doc.RootElement[0].GetProperty("id").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving critical-thinking session to Supabase");
        }

        return null;
    }

    private async Task SaveToGoogleSheets(ConversationHistory history, string? userId, bool includeFeedback)
    {
        if (string.IsNullOrWhiteSpace(_spreadsheetId))
        {
            _logger.LogInformation("Critical thinking spreadsheet id not configured. Skipping export.");
            return;
        }

        var sheetName = includeFeedback ? _feedbackSheetName : _noFeedbackSheetName;

        if (string.IsNullOrWhiteSpace(sheetName))
        {
            _logger.LogInformation("Critical thinking sheet name not configured. Skipping export.");
            return;
        }

        try
        {
            var ip = GetIp();
            var geo = await GetGeoInfoFromIp(ip);

            var row = new List<object>
            {
                DateTime.UtcNow.ToString("yyyy-MM-dd"),
                DateTime.UtcNow.ToString("HH:mm:ss"),
                history.ParticipantName,
                ip,
                geo.Country,
                geo.Region,
                geo.City,
                history.InitialSummary,
                history.Question1Answer,
                history.RevisionAfterQuestion1,
            };

            if (includeFeedback)
            {
                row.Add(history.Feedback1 ?? string.Empty);
            }

            row.AddRange(new object[]
            {
                history.Question2Answer,
                history.RevisionAfterQuestion2,
            });

            if (includeFeedback)
            {
                row.Add(history.Feedback2 ?? string.Empty);
            }

            row.AddRange(new object[]
            {
                history.Question3Answer,
                history.RevisionAfterQuestion3,
            });

            if (includeFeedback)
            {
                row.Add(history.Feedback3 ?? string.Empty);
            }

            row.AddRange(new object[]
            {
                history.Question4Answer,
                history.RevisionAfterQuestion4,
            });

            if (includeFeedback)
            {
                row.Add(history.Feedback4 ?? string.Empty);
            }

            row.AddRange(new object[]
            {
                history.Question5Answer,
                history.RevisionAfterQuestion5,
            });

            if (includeFeedback)
            {
                row.Add(history.Feedback5 ?? string.Empty);
            }

            row.AddRange(new object[]
            {
                history.FinalSummary,
                history.ReflectionAnswer1,
                history.ReflectionAnswer2,
                history.ReflectionAnswer3,
                history.ReflectionOpenResponse
            });

            await _googleSheetsService.AppendRowAsync(_spreadsheetId, sheetName, row);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving critical-thinking session to Google Sheets");
        }
    }

    private string GetIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',').First().Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    private async Task<(string Country, string Region, string City)> GetGeoInfoFromIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return ("Unknown", "Unknown", "Unknown");
        }

        try
        {
            var geoClient = _httpClientFactory.CreateClient("CustomClient");
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
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error fetching geo info");
        }

        return ("Unknown", "Unknown", "Unknown");
    }

}
