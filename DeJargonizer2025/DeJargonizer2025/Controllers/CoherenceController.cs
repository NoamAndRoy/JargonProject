using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using JargonProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[ApiController]
[Route("api/[controller]")]
public class CoherenceController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SupabaseClient _supabaseClient;
    private readonly ILogger<CoherenceController> _logger;
    private readonly GoogleSheetsService _googleSheetsService;
    private readonly string _apiUrl;
    private readonly string _apiKey;
    private readonly string _spreadsheetId;
    private readonly string _sheetName;

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
        public int? MinRows { get; set; }
    }

    public CoherenceController(
        IHttpClientFactory httpClientFactory,
        SupabaseClient supabaseClient,
        ILogger<CoherenceController> logger,
        IConfiguration configuration,
        GoogleSheetsService googleSheetsService)
    {
        _httpClientFactory = httpClientFactory;
        _supabaseClient = supabaseClient;
        _logger = logger;
        _googleSheetsService = googleSheetsService;

        _spreadsheetId = configuration["CoherenceChatbot:SpreadsheetId"]
            ?? Environment.GetEnvironmentVariable("COHERENCE_SPREADSHEET_ID")
            ?? string.Empty;

        _sheetName = configuration["CoherenceChatbot:SheetName"]
            ?? Environment.GetEnvironmentVariable("COHERENCE_SHEET_NAME")
            ?? "results";

        _apiUrl = configuration["CoherenceChatbot:OpenAiUrl"]
            ?? Environment.GetEnvironmentVariable("COHERENCE_OPENAI_URL")
            ?? "https://api.openai.com/v1/engines/gpt-3.5-turbo-instruct/completions";

        _apiKey = configuration["CoherenceChatbot:OpenAiKey"]
            ?? Environment.GetEnvironmentVariable("COHERENCE_OPENAI_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? string.Empty;
    }

    [HttpPost]
    public async Task<IActionResult> ProcessConversation([FromBody] ConversationHistory history)
    {
        try
        {
            history.Messages ??= new List<Message>();

            var userId = await HttpContext.TryGetUserIdAsync();

            var responseMessages = await DetermineResponse(history, userId);

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

    private async Task<List<BotMessage>> DetermineResponse(ConversationHistory history, string? userId)
    {
        var lastStudentMessage = history.Messages.LastOrDefault(m => m.IsStudent)?.Text?.Trim();

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
                        Text = "Please provide your full name so we can save your data for the research study."
                    });
                }
                else
                {
                    history.StartTime = DateTime.UtcNow;
                    greetingMessages.Add(new BotMessage
                    {
                        Text = "Please write or paste your text (150-500 words). You may submit an abstract/summary or 2-3 consecutive paragraphs from your paper."
                    });
                }

                return greetingMessages;

            case 1:
                if (!history.isResearch)
                {
                    history.StartTime = DateTime.UtcNow;
                    history.CurrentStage = 2;
                    return new List<BotMessage>
                    {
                        new BotMessage
                        {
                            Text = "Please write or paste your text (150-500 words). You may submit an abstract/summary or 2-3 consecutive paragraphs from your paper."
                        }
                    };
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
                        Text = "Question 1: Have you repeated key words/terms in a paragraph or across paragraphs to maintain topic focus?",
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

                if (history.Question1Choice == "yes")
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
                        MinRows = 2
                    }
                };

            case 4:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please complete the table before moving on." } };
                }

                history.Question1Details = lastStudentMessage;
                history.Feedback1 = await GenerateFeedbackAsync(1, history);
                history.CurrentStage = 5;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and suggestions given here from ChatGPT if they are relevant.",
                    },
                    new BotMessage { Text = history.Feedback1 },
                    new BotMessage
                    {
                        Text = "Please provide your revised text (150-500 words).",
                    }
                };

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

                if (history.Question2Choice == "yes")
                {
                    return new List<BotMessage>
                    {
                        new BotMessage
                        {
                            Text = "Point out 2-3 examples where you've used adverbs and explain how they help clarify your ideas.",
                        },
                        new BotMessage
                        {
                            Text = "Fill in at least two rows.",
                            InputType = "table",
                            TableHeaders = new List<string> { "Adverb", "Modified word", "How do adverbs help clarify" },
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
                        TableHeaders = new List<string> { "Adverb", "Modified word", "How do adverbs help clarify" },
                        MinRows = 2
                    }
                };

            case 7:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please complete the table before moving on." } };
                }

                history.Question2Details = lastStudentMessage;
                history.Feedback2 = await GenerateFeedbackAsync(2, history);
                history.CurrentStage = 8;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and the suggestions provided here if they are relevant.",
                    },
                    new BotMessage { Text = history.Feedback2 },
                    new BotMessage
                    {
                        Text = "Please provide your revised text (150-500 words)."
                    }
                };

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
                        Text = "Question 3: Did you use pronouns to refer back to previously mentioned ideas without repeating the exact words?"
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

                if (history.Question3Choice == "yes")
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
                            TableHeaders = new List<string> { "Word/term referred to", "Pronoun" },
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
                        TableHeaders = new List<string> { "Word/term referred to", "Pronoun" },
                        MinRows = 2
                    }
                };

            case 10:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please complete the table before moving on." } };
                }

                history.Question3Details = lastStudentMessage;
                history.Feedback3 = await GenerateFeedbackAsync(3, history);
                history.CurrentStage = 11;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and the suggestions provided here if they are relevant."
                    },
                    new BotMessage { Text = history.Feedback3 },
                    new BotMessage
                    {
                        Text = "Please provide your revised text (150-500 words)."
                    }
                };

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

                if (history.Question4Choice == "yes")
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
                        MinRows = 3
                    }
                };

            case 13:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please complete the table before moving on." } };
                }

                history.Question4Details = lastStudentMessage;
                history.Feedback4 = await GenerateFeedbackAsync(4, history);
                history.CurrentStage = 14;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and the suggestions provided here if they are relevant."
                    },
                    new BotMessage { Text = history.Feedback4 },
                    new BotMessage
                    {
                        Text = "Please provide your revised text (150-500 words)."
                    }
                };

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

                if (history.Question5Choice == "yes")
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
                            TableHeaders = new List<string> { "Connector", "Use" },
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
                        TableHeaders = new List<string> { "Connector", "Use" },
                        MinRows = 2
                    }
                };

            case 16:
                if (string.IsNullOrWhiteSpace(lastStudentMessage))
                {
                    return new List<BotMessage> { new BotMessage { Text = "Please complete the table before moving on." } };
                }

                history.Question5Details = lastStudentMessage;
                history.Feedback5 = await GenerateFeedbackAsync(5, history);
                history.CurrentStage = 17;

                return new List<BotMessage>
                {
                    new BotMessage
                    {
                        Text = "I have some suggestions to improve your text. Please write a revised version, incorporating your own ideas and the suggestions provided here if they are relevant."
                    },
                    new BotMessage { Text = history.Feedback5 },
                    new BotMessage
                    {
                        Text = "Please provide your revised text (150-500 words)."
                    }
                };

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
                    new BotMessage { Text = "Reflection 1: The chatbot was friendly and easy to interact with. Answer: choose one 1- strongly disagree  2- disagree 3- Neutral 4- agree 5- strongly agree" }
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
                    new BotMessage { Text = "Reflection 2: I found the chatbot challenging in a way that stimulated my writing skills. Answer: choose one 1- strongly disagree  2- disagree 3- Neutral 4- agree 5- strongly agree" }
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
                    new BotMessage { Text = "Reflection 3: The chatbot was useful for improving the quality of my writing. Answer: choose one 1- strongly disagree  2- disagree 3- Neutral 4- agree 5- strongly agree" }
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
                    new BotMessage { Text = "Reflection 4: The chatbot made the task more difficult than it needed to be. Answer: choose one 1- strongly disagree  2- disagree 3- Neutral 4- agree 5- strongly agree" }
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
                    await SaveToGoogleSheets(history, userId);
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
                       "<div class='chat-option'>Yes</div>" +
                       "<div class='chat-option'>No</div>" +
                       "<div class='chat-option'>Not sure</div>");

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

    private bool IsValidClosedChoice(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = NormalizeClosedChoice(input);
        return normalized is "yes" or "no" or "not sure";
    }

    private string NormalizeClosedChoice(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var trimmed = input.Trim().ToLowerInvariant();
        return trimmed switch
        {
            "y" => "yes",
            "yes" => "yes",
            "n" => "no",
            "no" => "no",
            "not sure" => "not sure",
            "notsure" => "not sure",
            "ns" => "not sure",
            _ => trimmed
        };
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
            "one" => "1",
            "two" => "2",
            "three" => "3",
            "four" => "4",
            "five" => "5",
            "1" => "1",
            "2" => "2",
            "3" => "3",
            "4" => "4",
            "5" => "5",
            _ => string.Empty
        };
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
                promptBuilder.AppendLine("Assess if pronouns clearly refer back to earlier ideas and guide on improving references.");
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

        var request = BuildOpenAiRequest(promptBuilder.ToString());
        var httpClient = _httpClientFactory.CreateClient("CustomClient");
        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);
        return json["choices"]?[0]?["text"]?.ToString().Trim() ?? string.Empty;
    }

    private HttpRequestMessage BuildOpenAiRequest(string prompt)
    {
        var requestBody = new
        {
            prompt,
            max_tokens = 250,
            temperature = 0.7,
            top_p = 1.0,
            frequency_penalty = 0,
            presence_penalty = 0
        };

        var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
        return request;
    }

    private async Task SaveToGoogleSheets(ConversationHistory history, string? userId)
    {
        if (string.IsNullOrWhiteSpace(_spreadsheetId))
        {
            _logger.LogInformation("Coherence spreadsheet id not configured. Skipping export.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_sheetName))
        {
            _logger.LogInformation("Coherence sheet name not configured. Skipping export.");
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(userId) || !await _supabaseClient.getIsSaveUserData(userId))
            {
                return;
            }

            var ip = GetIp();
            var geo = await GetGeoInfoFromIp(ip);

            var row = new List<object>
            {
                DateTime.UtcNow.ToString("yyyy-MM-dd"),
                DateTime.UtcNow.ToString("HH:mm:ss"),
                history.isResearch ? "Research" : "Regular",
                userId,
                history.ParticipantName,
                ip,
                geo.Country,
                geo.Region,
                geo.City,
                history.InitialText,
                history.Question1Choice,
                history.Question1Details,
                history.RevisionAfterQuestion1,
                history.Question2Choice,
                history.Question2Details,
                history.RevisionAfterQuestion2,
                history.Question3Choice,
                history.Question3Details,
                history.RevisionAfterQuestion3,
                history.Question4Choice,
                history.Question4Details,
                history.RevisionAfterQuestion4,
                history.Question5Choice,
                history.Question5Details,
                history.RevisionAfterQuestion5,
                history.FinalText,
                history.ReflectionAnswer1,
                history.ReflectionAnswer2,
                history.ReflectionAnswer3,
                history.ReflectionAnswer4,
                history.ReflectionOpenResponse
            };

            await _googleSheetsService.AppendRowAsync(_spreadsheetId, _sheetName, row);
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
