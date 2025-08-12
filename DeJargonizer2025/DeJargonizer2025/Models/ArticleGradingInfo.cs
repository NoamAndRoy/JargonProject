using System.Net;
using System.Text;
using JargonProject.Handlers;

namespace JargonProject.Models
{
    public class ArticleGradingInfo
    {
        public List<string> CleanedWords { get; set; }

        public List<string> CommonWords { get; set; }

        public List<string> NormalWords { get; set; }

        public List<string> RareWords { get; set; }
        public Dictionary<string, List<(string, WordType)>> RareWordsSyns { get; set; }

        public string[] Words { get; set; }

        public string Content { get; set; }

        public int Score { get; set; }

        public string Error { get; set; }

        public string Name { get; set; }

        public Language Lang { get; set; }

        private string? _htmlResult;

        public string HtmlResult
        {
            get
            {
                // Return cached value if already built
                if (_htmlResult != null) return _htmlResult;

                // Null-safe guards
                if (Words == null || Words.Length == 0)
                    return _htmlResult = string.Empty;

                var rare = (RareWords ?? new List<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var normal = (NormalWords ?? new List<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);

                var sb = new StringBuilder(Words.Length * 16);

                foreach (var word in Words)
                {
                    // classify using a cleaned version; case-insensitive sets handle casing
                    var cleaned = TextGrading.CleanWord(word);

                    string css = "commonWord";
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        if (rare.Contains(cleaned)) css = "rareWord";
                        else if (normal.Contains(cleaned)) css = "normalWord";
                    }

                    // Encode the original token for safety
                    var safe = WebUtility.HtmlEncode(word);

                    // Render newline tokens as <br />
                    if (word == "\r\n")
                        sb.Append("<span class='").Append(css).Append("'><br /></span>");
                    else
                        sb.Append("<span class='").Append(css).Append("'>").Append(safe).Append("</span>");
                }

                return _htmlResult = sb.ToString();
            }
        }
    }

    public class ArticleGradingInfoDTO
    {
        public List<string> CleanedWords { get; set; }
        public List<string> NormalWords { get; set; }
        public List<string> RareWords { get; set; }
        public List<string> Words { get; set; }
        public int Score { get; set; }
        public string? Name { get; set; }
    }
}