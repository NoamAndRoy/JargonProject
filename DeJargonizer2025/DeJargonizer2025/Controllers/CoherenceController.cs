using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
public class CoherenceController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SupabaseClient _supabaseClient;
    private readonly ILogger<CoherenceController> _logger;
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

        public string InitialText { get; set; }
        public string CurrentText { get; set; }

        public string Question1Choice { get; set; }
        public string Question1Details { get; set; }
        public string Feedback1 { get; set; }
        public string RevisionAfterQuestion1 { get; set; }

        public string Question2Choice { get; set; }
        public string Question2Details { get; set; }
        public string Feedback2 { get; set; }
        public string RevisionAfterQuestion2 { get; set; }

        public string Question3Choice { get; set; }
        public string Question3Details { get; set; }
        public string Feedback3 { get; set; }
        public string RevisionAfterQuestion3 { get; set; }

        public string Question4Choice { get; set; }
        public string Question4Details { get; set; }
        public string Feedback4 { get; set; }
        public string RevisionAfterQuestion4 { get; set; }

        public string Question5Choice { get; set; }
        public string Question5Details { get; set; }
        public string Feedback5 { get; set; }
        public string RevisionAfterQuestion5 { get; set; }

        public string FinalText { get; set; }

        public string ReflectionAnswer1 { get; set; }
        public string ReflectionAnswer2 { get; set; }
        public string ReflectionAnswer3 { get; set; }
        public string ReflectionAnswer4 { get; set; }
        public string ReflectionOpenResponse { get; set; }

        public string TaskId { get; set; }
        public bool FeedbackEnabled { get; set; }
    }

    public class Message
    {
        public string Text { get; set; }
        public bool IsStudent { get; set; }
        public string InputType { get; set; }
    }

    public class BotMessage
    {
        public string Text { get; set; }
        public string InputType { get; set; } = "text";
        public List<string> TableHeaders { get; set; }
        public List<string>? ExampleRow { get; set; }
        public int? MinRows { get; set; }
    }

    [Table("coherence_user_interactions")]
    public class CoherenceUserInteraction : BaseModel
    {
        [Column("user_id")]
        public string? UserId { get; set; }

        [Column("start_time")]
        public DateTime StartTime { get; set; }

        [Column("end_time")]
        public DateTime EndTime { get; set; }

        [Column("initial_text")]
        public string? InitialText { get; set; }

        [Column("current_text")]
        public string? CurrentText { get; set; }

        [Column("question1_choice")]
        public string? Question1Choice { get; set; }

        [Column("question1_details")]
        public string? Question1Details { get; set; }

        [Column("revision_after_question1")]
        public string? RevisionAfterQuestion1 { get; set; }

        [Column("feedback1")]
        public string? Feedback1 { get; set; }

        [Column("question2_choice")]
        public string? Question2Choice { get; set; }

        [Column("question2_details")]
        public string? Question2Details { get; set; }

        [Column("revision_after_question2")]
        public string? RevisionAfterQuestion2 { get; set; }

        [Column("feedback2")]
        public string? Feedback2 { get; set; }

        [Column("question3_choice")]
        public string? Question3Choice { get; set; }

        [Column("question3_details")]
        public string? Question3Details { get; set; }

        [Column("revision_after_question3")]
        public string? RevisionAfterQuestion3 { get; set; }

        [Column("feedback3")]
        public string? Feedback3 { get; set; }

        [Column("question4_choice")]
        public string? Question4Choice { get; set; }

        [Column("question4_details")]
        public string? Question4Details { get; set; }

        [Column("revision_after_question4")]
        public string? RevisionAfterQuestion4 { get; set; }

        [Column("feedback4")]
        public string? Feedback4 { get; set; }

        [Column("question5_choice")]
        public string? Question5Choice { get; set; }

        [Column("question5_details")]
        public string? Question5Details { get; set; }

        [Column("revision_after_question5")]
        public string? RevisionAfterQuestion5 { get; set; }

        [Column("feedback5")]
        public string? Feedback5 { get; set; }

        [Column("final_text")]
        public string? FinalText { get; set; }

        [Column("reflection_answer1")]
        public string? ReflectionAnswer1 { get; set; }

        [Column("reflection_answer2")]
        public string? ReflectionAnswer2 { get; set; }

        [Column("reflection_answer3")]
        public string? ReflectionAnswer3 { get; set; }

        [Column("reflection_answer4")]
        public string? ReflectionAnswer4 { get; set; }

        [Column("reflection_open_response")]
        public string? ReflectionOpenResponse { get; set; }
    }

    public CoherenceController(
        IHttpClientFactory httpClientFactory,
        SupabaseClient supabaseClient,
        ILogger<CoherenceController> logger,
        IConfiguration configuration,
        GoogleSheetsService googleSheetsService,
        GPTApiClient gptApiClient)
    {
        _httpClientFactory = httpClientFactory;
        _supabaseClient = supabaseClient;
        _logger = logger;
        _googleSheetsService = googleSheetsService;
        _gptApiClient = gptApiClient;

        _spreadsheetId = configuration["CoherenceChatbot:SpreadsheetId"]
            ?? Environment.GetEnvironmentVariable("COHERENCE_SPREADSHEET_ID")
            ?? string.Empty;

        _feedbackSheetName = configuration["CoherenceChatbot:SheetNameWithFeedback"]
            ?? Environment.GetEnvironmentVariable("COHERENCE_SHEET_NAME_WITH_FEEDBACK")
            ?? string.Empty;

        _noFeedbackSheetName = configuration["CoherenceChatbot:SheetNameWithoutFeedback"]
            ?? Environment.GetEnvironmentVariable("COHERENCE_SHEET_NAME_NO_FEEDBACK")
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

            history.Messages.AddRange(responseMessages.Select(m => new Message
            {
                Text = m.Text,
                IsStudent = false,
                InputType = m.InputType
            }));

            return Ok(new { Messages = responseMessages, UpdatedHistory = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing coherence conversation");
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

            history.Messages.AddRange(responseMessages.Select(m => new Message
            {
                Text = m.Text,
                IsStudent = false,
                InputType = m.InputType
            }));

            return Ok(new { Messages = responseMessages, UpdatedHistory = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing coherence conversation without feedback");
            return StatusCode(500, "An error occurred while processing your request.");
        }
    }

    private async Task<List<BotMessage>> DetermineResponse(ConversationHistory history, string? userId, bool includeFeedback)
    {
        history.FeedbackEnabled = includeFeedback;
        var lastStudentMessage = history.Messages.LastOrDefault(m => m.IsStudent)?.Text?.Trim();

        var greetingsInstructions = new List<BotMessage>
        {
            new BotMessage
            {
                Text = "Please write or paste your text, and I’ll guide you by asking questions that help you track and refine coherence. "
            },
            new BotMessage
            {
                Text = "If any question feels unclear, you can ask me for clarification or examples. "
            },
            new BotMessage
            {
                Text = "Please choose to write or paste: (1) an abstract or summary of your current work/article in 150-500-words or (2) a longer text from part of the article that includes 2-3 consecutive paragraphs from your paper."
            }
        };

        switch (history.CurrentStage)
        {
            case 0:
                history.CurrentStage = history.isResearch ? 1 : 2;

                var greetingMessages = new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Hi, I’m your academic writing assistant. My main focus is to help you improve coherence (the logical flow and clear connection of ideas in writing) in your writing."
                    }
                };

                if (history.isResearch)
                {
                    greetingMessages.Add(new BotMessage
                    {
                        Text = "Please provide your full name so we can save your data for the course/ research study."
                    });
                }
                else
                {
                    history.StartTime = DateTime.UtcNow;
                    greetingMessages.AddRange(greetingsInstructions);
                }

                return greetingMessages;

            case 1:
                if (!history.isResearch)
                {
                    history.StartTime = DateTime.UtcNow;
                    history.CurrentStage = 2;
                    return greetingsInstructions;
                }

                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please provide the name you would like us to record." } };
                }

                history.ParticipantName = lastStudentMessage;
                history.StartTime = DateTime.UtcNow;
                history.CurrentStage = 2;
                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Thank you. Please write or paste your text (150-500 words). You may submit an abstract/summary or 2-3 consecutive paragraphs from your paper."
                    }
                };

            case 2:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please share 150-500 words before we begin." } };
                }

                var initialValidation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (initialValidation != null)
                {
                    return new List<BotMessage> { new BotMessage { Text = initialValidation } };
                }

                history.InitialText = lastStudentMessage;
                history.CurrentText = lastStudentMessage;
                history.CurrentStage = 3;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Question 1: Please re-read your text - have you repeated key words/terms in a paragraph or across paragraphs to maintain topic focus?",
                    },
                    BuildClosedOptionsMessage()
                };

            case 3:
                if (!IsValidClosedChoice(lastStudentMessage))
                {
                    return new List<BotMessage> { BuildClosedOptionsMessage("Please reply with Yes, No, or Not sure.") };
                }

                history.Question1Choice = NormalizeClosedChoice(lastStudentMessage);
                history.CurrentStage = 4;

                if (history.Question1Choice == "Yes")
                {
                    return new List<BotMessage>
                    {
                        new BotMessage
                        {
                            Text = "Please give an example, including the word/s that are repeated and how many times repeated words occur. Copy-paste the shared or related words in these paragraphs or from your abstract, and list additional keywords that may also need to be repeated to improve coherence.",
                        },
                        new BotMessage
                        {
                            Text = "Fill in at least two rows.",
                            InputType = "table",
                            TableHeaders = new List<string> { "Repeated key word/term", "# times repeated", "Additional keywords to repeat" },
                            ExampleRow = new List<string> { "research question", "3", "participants, survey" },
                            MinRows = 2
                        }
                    };
                }

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Think of 1-2 keywords in your text that could be repeated to create a more coherent text and list them here.",
                    },
                    new BotMessage
                    {
                        Text = "Fill in at least two rows.",
                        InputType = "table",
                        TableHeaders = new List<string> { "Keywords that can be repeated" },
                        ExampleRow = new List<string> { "data analysis" },
                        MinRows = 2
                    }
                };

            case 4:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please complete the table before moving on." } };
                }

                history.Question1Details = FormatTable(lastStudentMessage);

                if (includeFeedback)
                {
                    history.Feedback1 = await GenerateFeedbackAsync(1, history);
                }
                else
                {
                    history.Feedback1 = null;
                }

                history.CurrentStage = 5;

                var revisionIntro1 = includeFeedback
                    ? "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant."
                    : "Please write a revised version, incorporating your own ideas and what you observed while completing the table.";

                var responses1 = new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = revisionIntro1,
                    }
                };

                if (includeFeedback && !string.IsNullOrWhiteSpace(history.Feedback1))
                {
                    responses1.Add(new BotMessage { Text = history.Feedback1 });
                }

                responses1.Add(new BotMessage
                {
                    Text = "Please provide your revised text (150-500 words).",
                });

                return responses1;

            case 5:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please provide a 150-500 word revision." } };
                }

                var revision1Validation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (revision1Validation != null)
                {
                    return new List<BotMessage> { new BotMessage { Text = revision1Validation } };
                }

                history.RevisionAfterQuestion1 = lastStudentMessage;
                history.CurrentText = lastStudentMessage;
                history.CurrentStage = 6;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Question 2: Have you used adverbs (words that modify or qualify other verbs) to give precise information, offering clarity and elaboration?"
                    },
                    BuildClosedOptionsMessage()
                };

            case 6:
                if (!IsValidClosedChoice(lastStudentMessage))
                {
                    return new List<BotMessage> { BuildClosedOptionsMessage("Please reply with Yes, No, or Not sure.") };
                }

                history.Question2Choice = NormalizeClosedChoice(lastStudentMessage);
                history.CurrentStage = 7;

                if (history.Question2Choice == "Yes")
                {
                    return new List<BotMessage>
                    {
                        new BotMessage
                        {
                            Text = "Point out 2-3 examples where you've used adverbs .",
                        },
                        new BotMessage
                        {
                            Text = "Fill in at least two rows.",
                            InputType = "table",
                            TableHeaders = new List<string> { "Adverb", "Modified word" },
                            ExampleRow = new List<string> { "carefully", "analyzed" },
                            MinRows = 2
                        }
                    };
                }

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Look at all of your verbs and list potential adverbs that could improve your text.",
                    },
                    new BotMessage
                    {
                        Text = "Fill in at least two rows.",
                        InputType = "table",
                        TableHeaders = new List<string> { "Adverb", "Modified word" },
                        ExampleRow = new List<string> { "carefully", "analyzed" },
                        MinRows = 2
                    }
                };

            case 7:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please complete the table before moving on." } };
                }

                history.Question2Details = FormatTable(lastStudentMessage);

                if (includeFeedback)
                {
                    history.Feedback2 = await GenerateFeedbackAsync(2, history);
                }
                else
                {
                    history.Feedback2 = null;
                }

                history.CurrentStage = 8;

                var revisionIntro2 = includeFeedback
                    ? "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and the suggestions provided here if they are relevant."
                    : "Please write a revised version, incorporating your own ideas and what you noticed while analyzing your adverb use.";

                var responses2 = new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = revisionIntro2,
                    }
                };

                if (includeFeedback && !string.IsNullOrWhiteSpace(history.Feedback2))
                {
                    responses2.Add(new BotMessage { Text = history.Feedback2 });
                }

                responses2.Add(new BotMessage
                {
                    Text = "Please provide your revised text (150-500 words)."
                });

                return responses2;

            case 8:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please provide a 150-500 word revision." } };
                }

                var revision2Validation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (revision2Validation != null)
                {
                    return new List<BotMessage> { new BotMessage { Text = revision2Validation } };
                }

                history.RevisionAfterQuestion2 = lastStudentMessage;
                history.CurrentText = lastStudentMessage;
                history.CurrentStage = 9;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Did you use pronouns (words that replace nouns such as, he, it, their, our, this, that) to refer back to previously mentioned ideas  without repeating the exact words?"
                    },
                    BuildClosedOptionsMessage()
                };

            case 9:
                if (!IsValidClosedChoice(lastStudentMessage))
                {
                    return new List<BotMessage> { BuildClosedOptionsMessage("Please reply with Yes, No, or Not sure.") };
                }

                history.Question3Choice = NormalizeClosedChoice(lastStudentMessage);
                history.CurrentStage = 10;

                if (history.Question3Choice == "Yes")
                {
                    return new List<BotMessage>
                    {
                        new BotMessage
                        {
                            Text = "Mention 2-3 pronouns from your text and explain the specific idea or noun they refer to.",
                        },
                        new BotMessage
                        {
                            Text = "Fill in at least two rows.",
                            InputType = "table",
                            TableHeaders = new List<string> { "Pronoun", "Word/term referred to" },
                            ExampleRow = new List<string> { "these", "20 samples" },
                            MinRows = 2
                        }
                    };
                }

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Please list the keywords (nouns) you have repeated and which pronoun could replace them in some sentences.",
                    },
                    new BotMessage
                    {
                        Text = "Fill in at least two rows.",
                        InputType = "table",
                        TableHeaders = new List<string> { "Pronoun", "Word/term referred to" },
                        ExampleRow = new List<string> { "these", "20 samples" },
                        MinRows = 2
                    }
                };

            case 10:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please complete the table before moving on." } };
                }

                history.Question3Details = FormatTable(lastStudentMessage);

                if (includeFeedback)
                {
                    history.Feedback3 = await GenerateFeedbackAsync(3, history);
                }
                else
                {
                    history.Feedback3 = null;
                }

                history.CurrentStage = 11;

                var revisionIntro3 = includeFeedback
                    ? "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and the suggestions provided here if they are relevant."
                    : "Please write a revised version, incorporating your own ideas and what you noticed while reviewing your pronoun usage.";

                var responses3 = new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = revisionIntro3
                    }
                };

                if (includeFeedback && !string.IsNullOrWhiteSpace(history.Feedback3))
                {
                    responses3.Add(new BotMessage { Text = history.Feedback3 });
                }

                responses3.Add(new BotMessage
                {
                    Text = "Please provide your revised text (150-500 words)."
                });

                return responses3;

            case 11:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please provide a 150-500 word revision." } };
                }

                var revision3Validation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (revision3Validation != null)
                {
                    return new List<BotMessage> { new BotMessage { Text = revision3Validation } };
                }

                history.RevisionAfterQuestion3 = lastStudentMessage;
                history.CurrentText = lastStudentMessage;
                history.CurrentStage = 12;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Question 4: Did you use a variety of verbs with similar meanings to describe similar actions or processes throughout the text?"
                    },
                    BuildClosedOptionsMessage()
                };

            case 12:
                if (!IsValidClosedChoice(lastStudentMessage))
                {
                    return new List<BotMessage> { BuildClosedOptionsMessage("Please reply with Yes, No, or Not sure.") };
                }

                history.Question4Choice = NormalizeClosedChoice(lastStudentMessage);
                history.CurrentStage = 13;

                if (history.Question4Choice == "Yes")
                {
                    return new List<BotMessage>
                    {
                        new BotMessage
                        {
                            Text = "List at least three pairs of verbs used in different sentences or paragraphs that are synonyms or closely related in meaning.",
                        },
                        new BotMessage
                        {
                            Text = "Fill in at least three rows.",
                            InputType = "table",
                            TableHeaders = new List<string> { "Verb", "Verb with a similar meaning" },
                            ExampleRow = new List<string> { "demonstrate", "illustrate" },
                            MinRows = 3
                        }
                    };
                }

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Please list 3-4 different verbs you have used in your text and suggest verbs with a similar meaning that could replace them.",

                    },
                    new BotMessage
                    {
                        Text = "Fill in at least three rows.",
                        InputType = "table",
                        TableHeaders = new List<string> { "Verb", "Verb with a similar meaning" },
                        ExampleRow = new List<string> { "demonstrate", "illustrate" },
                        MinRows = 3
                    }
                };

            case 13:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please complete the table before moving on." } };
                }

                history.Question4Details = FormatTable(lastStudentMessage);

                if (includeFeedback)
                {
                    history.Feedback4 = await GenerateFeedbackAsync(4, history);
                }
                else
                {
                    history.Feedback4 = null;
                }

                history.CurrentStage = 14;

                var revisionIntro4 = includeFeedback
                    ? "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and the suggestions provided here if they are relevant."
                    : "Please write a revised version, incorporating your own ideas and what you learned while mapping the verbs you use.";

                var responses4 = new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = revisionIntro4
                    }
                };

                if (includeFeedback && !string.IsNullOrWhiteSpace(history.Feedback4))
                {
                    responses4.Add(new BotMessage { Text = history.Feedback4 });
                }

                responses4.Add(new BotMessage
                {
                    Text = "Please provide your revised text (150-500 words)."
                });

                return responses4;

            case 14:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please provide a 150-500 word revision." } };
                }

                var revision4Validation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (revision4Validation != null)
                {
                    return new List<BotMessage> { new BotMessage { Text = revision4Validation } };
                }

                history.RevisionAfterQuestion4 = lastStudentMessage;
                history.CurrentText = lastStudentMessage;
                history.CurrentStage = 15;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Question 5: Have you used connectors/transition words to guide your reader between points (e.g., Therefore, In addition, However)?"
                    },
                    BuildClosedOptionsMessage()
                };

            case 15:
                if (!IsValidClosedChoice(lastStudentMessage))
                {
                    return new List<BotMessage> { BuildClosedOptionsMessage("Please reply with Yes, No, or Not sure.") };
                }

                history.Question5Choice = NormalizeClosedChoice(lastStudentMessage);
                history.CurrentStage = 16;

                if (history.Question5Choice == "Yes")
                {
                    return new List<BotMessage>
                    {
                        new BotMessage
                        {
                            Text = "Mention 2-3 connectors used between sentences or paragraphs and show the connection they create.",
                        },
                        new BotMessage
                        {
                            Text = "Fill in at least two rows.",
                            InputType = "table",
                            TableHeaders = new List<string> { "Connector", "Between sentences or paragraphs?" },
                            ExampleRow = new List<string> { "Therefore", "paragraphs" },
                            MinRows = 2
                        }
                    };
                }

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "Please list suitable connectors between paragraphs or sentences that could be added to show addition, examples, contradiction, sequence, etc., and indicate how you would use them.",

                    },
                    new BotMessage
                    {
                        Text = "Fill in at least two rows.",
                        InputType = "table",
                        TableHeaders = new List<string> { "Connector", "Between sentences or paragraphs?" },
                        ExampleRow = new List<string> { "Therefore", "sentences" },
                        MinRows = 2
                    }
                };

            case 16:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please complete the table before moving on." } };
                }

                history.Question5Details = FormatTable(lastStudentMessage);

                if (includeFeedback)
                {
                    history.Feedback5 = await GenerateFeedbackAsync(5, history);
                }
                else
                {
                    history.Feedback5 = null;
                }

                history.CurrentStage = 17;

                var revisionIntro5 = includeFeedback
                    ? "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and the suggestions provided here if they are relevant."
                    : "Please write a revised version, incorporating your own ideas and what you noticed about your connectors.";

                var responses5 = new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = revisionIntro5
                    }
                };

                if (includeFeedback && !string.IsNullOrWhiteSpace(history.Feedback5))
                {
                    responses5.Add(new BotMessage { Text = history.Feedback5 });
                }

                responses5.Add(new BotMessage
                {
                    Text = "Please provide your revised text (150-500 words)."
                });

                return responses5;

            case 17:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please provide a 150-500 word revision." } };
                }

                var revision5Validation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (revision5Validation != null)
                {
                    return new List<BotMessage> { new BotMessage { Text = revision5Validation } };
                }

                history.RevisionAfterQuestion5 = lastStudentMessage;
                history.CurrentText = lastStudentMessage;
                history.CurrentStage = 18;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "After answering all 5 questions, please revise your summary/paragraphs and write/copy the final revised version below. (150-500 words)"
                    }
                };

            case 18:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please provide the final 150-500 word version." } };
                }

                var finalValidation = ValidateWordCount(lastStudentMessage, 150, 500);
                if (finalValidation != null)
                {
                    return new List<BotMessage> { new BotMessage { Text = finalValidation } };
                }

                history.FinalText = lastStudentMessage;
                history.CurrentText = lastStudentMessage;
                history.CurrentStage = 19;

                return new List<BotMessage>
                {
                    new BotMessage { Text = "Thank you! We hope you learned about improving coherence in your academic texts!" },
                    new BotMessage { Text = "The final step is to answer four short, close-ended reflection questions about the chatbot use and one optional open question. For each statement, please select an option from a 5-point Likert scale where 1 = strongly disagree and 5 = strongly agree." },
                    BuildLikertOptionsMessage("Reflection 1: The chatbot was friendly and easy to interact with.")
                };

            case 19:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please reply with a single number from 1 (strongly disagree) to 5 (strongly agree)." } };
                }

                history.ReflectionAnswer1 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 20;
                return new List<BotMessage>
                {
                    BuildLikertOptionsMessage("Reflection 2: I found the chatbot challenging in a way that stimulated my writing skills.")
                };

            case 20:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please reply with a single number from 1 to 5." } };
                }

                history.ReflectionAnswer2 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 21;
                return new List<BotMessage>
                {
                    BuildLikertOptionsMessage("Reflection 3: The chatbot was useful for improving the quality of my writing.")
                };

            case 21:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please reply with a single number from 1 to 5." } };
                }

                history.ReflectionAnswer3 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 22;
                return new List<BotMessage>
                {
                    BuildLikertOptionsMessage("Reflection 4: The chatbot made the task more difficult than it needed to be.")
                };

            case 22:
                if (!IsValidLikertChoice(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please reply with a single number from 1 to 5." } };
                }

                history.ReflectionAnswer4 = NormalizeLikertChoice(lastStudentMessage);
                history.CurrentStage = 23;
                return new List<BotMessage>
                {
                    new BotMessage { Text = "How did you feel while using the chatbot during your writing task? How did it help you, if it did? What did you learn about coherence and improving your academic writing?" }
                };

            case 23:
                history.ReflectionOpenResponse = lastStudentMessage ?? string.Empty;
                history.CurrentStage = 24;

                if (history.isResearch)
                {
                    await SaveToGoogleSheets(history, userId, includeFeedback);
                }
                else
                {
                    history.TaskId = await SaveToSupabase(history, userId);
                }

                return new List<BotMessage>
                {
                    new BotMessage { Text = "Thank you for sharing your reflections. Your responses have been recorded." }
                };

            default:
                return new List<BotMessage>
                {
                    new BotMessage { Text = "If you need further assistance, feel free to start a new conversation." }
                };
        }
    }

    private BotMessage BuildClosedOptionsMessage(string prefix = null)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(prefix))
        {
            builder.Append(prefix).Append(" ");
        }

        builder.Append("Please type one of the following options:" +
                       "<div class='chat-option'>(1) Yes</div>" +
                       "<div class='chat-option'>(2) No</div>" +
                       "<div class='chat-option'>(3) Not sure</div>");

        return new BotMessage { Text = builder.ToString() };
    }


    private BotMessage BuildLikertOptionsMessage(string prefix = null)
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

        return new BotMessage { Text = builder.ToString() };
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

    private bool IsValidClosedChoice(string lastUserText)
    {
        return new List<string> { "1", "2", "3" }.Any(r => r == lastUserText.Trim());
    }

    private string NormalizeClosedChoice(string lastStudentMessage)
    {
        return lastStudentMessage switch
        {
            "1" => "Yes",
            "2" => "No",
            "3" => "Not sure",
        };
    }

    private bool IsValidLikertChoice(string input)
    {
        return new List<string> { "1", "2", "3", "4", "5" }.Any(r => r == input.Trim());
    }

    private string NormalizeLikertChoice(string input)
    {
        return input?.Trim().ToLowerInvariant() switch
        {
            "1" => "Strongly disagree",
            "2" => "Disagree",
            "3" => "Neutral",
            "4" => "Agree",
            "5" => "Strongly agree",
            _ => string.Empty
        };
    }

    private string FormatTable(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var trimmed = input.Trim();

        if (!trimmed.Contains("<table", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        try
        {
            var csvBuilder = new StringBuilder();
            var tableMatches = Regex.Matches(trimmed, "<table.*?>(.*?)</table>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match tableMatch in tableMatches)
            {
                var rows = Regex.Matches(tableMatch.Value, "<tr.*?>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match row in rows)
                {
                    var cells = Regex.Matches(row.Groups[1].Value, "<(td|th).*?>(.*?)</\\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    if (cells.Count == 0)
                    {
                        continue;
                    }

                    var values = new List<string>();

                    foreach (Match cell in cells)
                    {
                        var cellContent = cell.Groups[2].Value;
                        cellContent = Regex.Replace(cellContent, "<\\s*br\\s*/?>", "\n", RegexOptions.IgnoreCase);
                        cellContent = Regex.Replace(cellContent, "<.*?>", string.Empty, RegexOptions.Singleline);
                        cellContent = WebUtility.HtmlDecode(cellContent);
                        cellContent = cellContent.Replace('\r', ' ').Replace('\n', ' ').Trim();

                        if (cellContent.Contains(',') || cellContent.Contains('"'))
                        {
                            cellContent = $"\"{cellContent.Replace("\"", "\"\"")}\"";
                        }

                        values.Add(cellContent);
                    }

                    if (values.Count > 0)
                    {
                        csvBuilder.AppendLine(string.Join(",", values));
                    }
                }
            }

            var result = csvBuilder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? trimmed : result;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to format table input.");
            return trimmed;
        }
    }

    private async Task<string> GenerateFeedbackAsync(int questionNumber, ConversationHistory history)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("You are providing concise feedback (2-3 sentences) to help a graduate student strengthen coherence in their academic writing.");
        promptBuilder.AppendLine("Always address the student as \"you\". Do not rewrite their text; offer suggestions and observations only.");

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Student's current text:");
        promptBuilder.AppendLine(history.CurrentText ?? string.Empty);

        switch (questionNumber)
        {
            case 1:
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Question focus: repeating key words/terms.");
                promptBuilder.AppendLine($"Student answer: {history.Question1Choice}");
                promptBuilder.AppendLine("Details provided:");
                promptBuilder.AppendLine(history.Question1Details ?? string.Empty);
                promptBuilder.AppendLine("Evaluate whether the student is repeating key terms to maintain topic focus and suggest improvements if needed.");
                break;
            case 2:
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Question focus: using adverbs for precision.");
                promptBuilder.AppendLine($"Student answer: {history.Question2Choice}");
                promptBuilder.AppendLine("Details provided:");
                promptBuilder.AppendLine(history.Question2Details ?? string.Empty);
                promptBuilder.AppendLine("Comment on whether adverbs are used effectively and recommend refinements.");
                break;
            case 3:
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Question focus: using pronouns to avoid repetition.");
                promptBuilder.AppendLine($"Student answer: {history.Question3Choice}");
                promptBuilder.AppendLine("Details provided:");
                promptBuilder.AppendLine(history.Question3Details ?? string.Empty);
                promptBuilder.AppendLine("Assess if pronouns clearly refer back to earlier ideas and guide on improving references. Please give an example of a pronoun or multiple entities that should be double checked by the student");
                break;
            case 4:
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Question focus: variety of verbs with similar meanings.");
                promptBuilder.AppendLine($"Student answer: {history.Question4Choice}");
                promptBuilder.AppendLine("Details provided:");
                promptBuilder.AppendLine(history.Question4Details ?? string.Empty);
                promptBuilder.AppendLine("Consider whether verb variety supports coherence and offer suggestions.");
                break;
            case 5:
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Question focus: connectors/transition words.");
                promptBuilder.AppendLine($"Student answer: {history.Question5Choice}");
                promptBuilder.AppendLine("Details provided:");
                promptBuilder.AppendLine(history.Question5Details ?? string.Empty);
                promptBuilder.AppendLine("Evaluate the use of connectors and suggest improvements to guide the reader between points.");
                break;
        }

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Provide only 2-3 sentences of feedback.");

        return await _gptApiClient.RephraseText(promptBuilder.ToString());
    }

    private async Task<string?> SaveToSupabase(ConversationHistory history, string? userId)
    {
        try
        {
            var isSaveUserData = await _supabaseClient.getIsSaveUserData(userId);

            var data = new CoherenceUserInteraction
            {
                UserId = isSaveUserData ? userId : null,
                StartTime = history.StartTime == default ? DateTime.UtcNow : history.StartTime,
                EndTime = DateTime.UtcNow,
                InitialText = isSaveUserData ? history.InitialText : null,
                CurrentText = isSaveUserData ? history.CurrentText : null,
                Question1Choice = history.Question1Choice,
                Question1Details = isSaveUserData ? history.Question1Details : null,
                RevisionAfterQuestion1 = isSaveUserData ? history.RevisionAfterQuestion1 : null,
                Feedback1 = isSaveUserData ? history.Feedback1 : null,
                Question2Choice = history.Question2Choice,
                Question2Details = isSaveUserData ? history.Question2Details : null,
                RevisionAfterQuestion2 = isSaveUserData ? history.RevisionAfterQuestion2 : null,
                Feedback2 = isSaveUserData ? history.Feedback2 : null,
                Question3Choice = history.Question3Choice,
                Question3Details = isSaveUserData ? history.Question3Details : null,
                RevisionAfterQuestion3 = isSaveUserData ? history.RevisionAfterQuestion3 : null,
                Feedback3 = isSaveUserData ? history.Feedback3 : null,
                Question4Choice = history.Question4Choice,
                Question4Details = isSaveUserData ? history.Question4Details : null,
                RevisionAfterQuestion4 = isSaveUserData ? history.RevisionAfterQuestion4 : null,
                Feedback4 = isSaveUserData ? history.Feedback4 : null,
                Question5Choice = history.Question5Choice,
                Question5Details = isSaveUserData ? history.Question5Details : null,
                RevisionAfterQuestion5 = isSaveUserData ? history.RevisionAfterQuestion5 : null,
                Feedback5 = isSaveUserData ? history.Feedback5 : null,
                FinalText = isSaveUserData ? history.FinalText : null,
                ReflectionAnswer1 = isSaveUserData ? history.ReflectionAnswer1 : null,
                ReflectionAnswer2 = isSaveUserData ? history.ReflectionAnswer2 : null,
                ReflectionAnswer3 = isSaveUserData ? history.ReflectionAnswer3 : null,
                ReflectionAnswer4 = isSaveUserData ? history.ReflectionAnswer4 : null,
                ReflectionOpenResponse = isSaveUserData ? history.ReflectionOpenResponse : null
            };

            var result = await _supabaseClient.client.From<CoherenceUserInteraction>().Insert(data);

            using var doc = JsonDocument.Parse(result.Content);
            return doc.RootElement[0].GetProperty("id").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving coherence session to Supabase");
        }

        return null;
    }

    private async Task SaveToGoogleSheets(ConversationHistory history, string? userId, bool includeFeedback)
    {
        if (string.IsNullOrWhiteSpace(_spreadsheetId))
        {
            _logger.LogInformation("Coherence spreadsheet id not configured. Skipping export.");
            return;
        }

        var sheetName = includeFeedback ? _feedbackSheetName : _noFeedbackSheetName;

        if (string.IsNullOrWhiteSpace(sheetName))
        {
            _logger.LogInformation("Coherence sheet name not configured. Skipping export.");
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
                history.InitialText,
                history.Question1Choice,
                history.Question1Details,
                history.RevisionAfterQuestion1,
            };

            if (includeFeedback)
            {
                row.Add(history.Feedback1 ?? string.Empty);
            }

            row.AddRange(new object[]
            {
                history.Question2Choice,
                history.Question2Details,
                history.RevisionAfterQuestion2,
            });

            if (includeFeedback)
            {
                row.Add(history.Feedback2 ?? string.Empty);
            }

            row.AddRange(new object[]
            {
                history.Question3Choice,
                history.Question3Details,
                history.RevisionAfterQuestion3,
            });

            if (includeFeedback)
            {
                row.Add(history.Feedback3 ?? string.Empty);
            }

            row.AddRange(new object[]
            {
                history.Question4Choice,
                history.Question4Details,
                history.RevisionAfterQuestion4,
            });

            if (includeFeedback)
            {
                row.Add(history.Feedback4 ?? string.Empty);
            }

            row.AddRange(new object[]
            {
                history.Question5Choice,
                history.Question5Details,
                history.RevisionAfterQuestion5,
            });

            if (includeFeedback)
            {
                row.Add(history.Feedback5 ?? string.Empty);
            }

            row.AddRange(new object[]
            {
                history.FinalText,
                history.ReflectionAnswer1,
                history.ReflectionAnswer2,
                history.ReflectionAnswer3,
                history.ReflectionAnswer4,
                history.ReflectionOpenResponse
            });

            await _googleSheetsService.AppendRowAsync(_spreadsheetId, sheetName, row);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving coherence session to Google Sheets");
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
