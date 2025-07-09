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

        public string HtmlResult { 
            get
            {
                var text = new StringBuilder();

                foreach (string word in Words)
                {
                    string cleanedWord = TextGrading.CleanWord(word).ToLower();
                    string type = "commonWord";

                    if (RareWords.Contains(cleanedWord))
                    {
                        type = "rareWord";
                    }
                    else if (NormalWords.Contains(cleanedWord))
                    {
                        type = "normalWord";
                    }

                    text.AppendFormat("<span class='{0}'>{1}</span>", type, word == "\r\n" ? "<br />" : word);
                }

                return text.ToString();
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