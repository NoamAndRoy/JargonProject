using LumenWorks.Framework.IO.Csv;
using Novacode;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using JargonProject.Models;

namespace JargonProject.Handlers
{
    public static class TextGrading
    {
        private static Dictionary<string, int> s_Words;

        private static readonly List<string> sr_SplitOptions;

        private static int m_Common;
        private static int m_Normal;
        private static bool m_WordsLoaded;

        static TextGrading()
        {
            s_Words = new Dictionary<string, int>();

            sr_SplitOptions = new List<string>(char.MaxValue);

            for (int i = char.MinValue; i <= char.MaxValue; i++)
            {
                if (!char.IsLetter((char)i) && (char)i != '\'' && (char)i != '\r' && (char)i != '\n')
                    sr_SplitOptions.Add(((char)i).ToString());
            }

            sr_SplitOptions.Add(Environment.NewLine);

            m_WordsLoaded = false;
        }

        public static ArticleGradingInfo AnalyzeSingleText(string i_Text)
        {
            ArticleGradingInfo articleGradingInfo;

            if (string.IsNullOrWhiteSpace(i_Text))
            {
                articleGradingInfo = new ArticleGradingInfo();
                articleGradingInfo.Error = "Article is empty.";
            }
            else
            {
                if (!m_WordsLoaded)
                {
                    loadWords();
                }

                articleGradingInfo = analyzeArticle(i_Text);
            }

            return articleGradingInfo;
        }

        private static ArticleGradingInfo analyzeArticle(string i_Article)
        {
            List<string> cleanedWords = getCleanedWords(i_Article);

            ArticleGradingInfo articleGradingInfo = new ArticleGradingInfo();
            articleGradingInfo.Content = i_Article;

            articleGradingInfo.CommonWords = getWordsInRange(cleanedWords, m_Common, int.MaxValue);
            articleGradingInfo.NormalWords = getWordsInRange(cleanedWords, m_Normal, m_Common - 1);
            articleGradingInfo.RareWords = getWordsInRange(cleanedWords, 0, m_Normal - 1);

            foreach (string splitOption in sr_SplitOptions)
            {
                i_Article = i_Article.Replace(splitOption, "\0" + splitOption + "\0");
            }

            articleGradingInfo.CleanedWords = cleanedWords;
            articleGradingInfo.Words = i_Article.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            
            articleGradingInfo.Score = cleanedWords.Count;

            return articleGradingInfo;
        }

        private static List<string> getWordsInRange(List<string> i_Words, int i_MinValue, int i_MaxValue)
        {
            List<string> wordsInRange = new List<string>();

            foreach (string word in i_Words)
            {
                if (s_Words.ContainsKey(word.ToLower()))
                {
                    int wordValue = s_Words[word.ToLower()];
                    if (wordValue >= i_MinValue && wordValue <= i_MaxValue)
                    {
                        wordsInRange.Add(word.ToLower());
                    }
                }
                else if (i_MinValue <= 0)
                {
                    wordsInRange.Add(word.ToLower());
                }
            }

            return wordsInRange;
        }

        private static List<string> getCleanedWords(string i_Text)
        {
            i_Text = i_Text.Replace("\'s", "");
            List<string> words = i_Text.Split(new string[] { " ", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();

            List<string> wordsToDelete = new List<string>();

            foreach (string word in words)
            {
                if (word.Contains("@") || word.ToLower().Contains("www"))
                {
                    wordsToDelete.Add(word);
                }
            }

            foreach (string wordToDelete in wordsToDelete)
            {
                words.Remove(wordToDelete);
            }

            StringBuilder textWithoutSpecielCases = new StringBuilder();
            foreach (string word in words)
            {
                textWithoutSpecielCases.Append(word + " ");
            }

            words = textWithoutSpecielCases.ToString().Split(sr_SplitOptions.ToArray(), StringSplitOptions.RemoveEmptyEntries).ToList();


            List<int> indexesToRemove = new List<int>();
            for (int i = 0; i < words.Count; i++)
            {
                words[i] = CleanWord(words[i]);
                if (string.IsNullOrWhiteSpace(words[i]))
                {
                    indexesToRemove.Add(i);
                }
            }

            for (int i = indexesToRemove.Count - 1; i >= 0; i--)
            {
                words.RemoveAt(indexesToRemove[i]);
            }

            return words;
        }

        public static string CalculateScore(int i_AmountOfCommonWord, int i_AmountOfNormalWord, int i_AmountOfRareWord, int i_TotalAmount)
        {
            double score = i_AmountOfNormalWord * 0.5f + i_AmountOfRareWord * 1;
            score *= 100f / i_TotalAmount;

            return string.Format("{0}% - {1}", Math.Round(100 - score), i_TotalAmount);
        }

        public static string CleanWord(string i_Word)
        {
            StringBuilder cleanedWord = new StringBuilder();

            for (int i = 0; i < i_Word.Length; i++)
            {
                if (char.IsLetter(i_Word[i]) || (i_Word[i] == '\'' && i < i_Word.Length - 1 && i > 0))
                {
                    cleanedWord.Append(i_Word[i]);
                }
            }

            return cleanedWord.ToString();
        }

        private static void loadWords()
        {
            string instancesMatrixFile = HttpContext.Current.Server.MapPath(@"~\InstanceMatrices\DataUKUS.csv");
            m_Common = 1000;
            m_Normal = 50;

            if (string.IsNullOrEmpty(instancesMatrixFile)) return;
            TextReader data = new StreamReader(new FileStream(instancesMatrixFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var csv = new CsvReader(data, false);

            s_Words.Clear();

            while (csv.ReadNextRecord())
            {
                s_Words.Add(csv[0], int.Parse(csv[1]));
            }
            data.Close();
        }

        public static string LoadTextFromFile(Stream i_Stream, string i_Extension)
        {
            string article = string.Empty;

            if (i_Extension.Equals("txt") || i_Extension.Equals("html") || i_Extension.Equals("htm"))
            {
                TextReader data = new StreamReader(i_Stream);
                article = data.ReadToEnd();
                data.Close();
            }
            else if (i_Extension.Equals("docx"))
            {
                var document = DocX.Load(i_Stream);
                var sb = new StringBuilder();
                foreach (var p in document.Paragraphs)
                {
                    sb.AppendLine(p.Text);
                }

                article = sb.ToString();
            }


            return article;
        }
    }
}