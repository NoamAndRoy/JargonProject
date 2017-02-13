using System.Collections.Generic;
using JargonProject.Handlers;
using JargonProject.Helpers;

namespace JargonProject.Models
{
    public class ArticleGradingInfo
    {
        public List<string> CleanedWords { get; set; }

        public List<string> CommonWords { get; set; }

        public List<string> NormalWords { get; set; }

        public List<string> RareWords { get; set; }

        public string[] Words { get; set; }

        public string Content { get; set; }

        public int Score { get; set; }

        public string Error { get; set; }

        public string Name { get; set; }
    }
}