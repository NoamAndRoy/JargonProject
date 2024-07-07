using CsvHelper;
using CsvHelper.Configuration;
using JargonProject.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xceed.Words.NET;
using System.Net.Http;

namespace JargonProject.Handlers
{
    public enum Language
    {
        Hebrew2012_2017,
        Hebrew2012_2015,
        English2012_2015,
        English2013_2016,
        English2014_2017,
        English2015_2018,
        English2016_2019,
        English2017_2020,
        English2018_2021,
        English2019_2022,
        English2020_2023,
    }

    public enum WordType
    {
        Rare = 0,
        Normal = 1,
        Common = 2,
    }

    public static class TextGrading
    {
        private static Dictionary<string, int> s_Words;
        private static List<string> s_Names;

        private static readonly List<string> sr_SplitOptions;

        private static int m_Common;
        private static int m_Normal;
        private static bool m_WordsLoaded;
        private static bool m_NamesLoaded;
        private static Language lang;

        public static Language Lang
        {
            get => lang; set
            {
                m_WordsLoaded = false;
                lang = value;
            }
        }

        static TextGrading()
        {
            s_Words = new Dictionary<string, int>();
            s_Names = new List<string>();

            sr_SplitOptions = new List<string>(char.MaxValue);

            for (int i = char.MinValue; i <= char.MaxValue; i++)
            {
                if (!char.IsLetter((char)i) && (char)i != '\'' && (char)i != '\r' && (char)i != '\n')
                    sr_SplitOptions.Add(((char)i).ToString());
            }

            sr_SplitOptions.Add(Environment.NewLine);

            m_WordsLoaded = false;
            m_NamesLoaded = false;
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
                    m_WordsLoaded = true;
                }

                if (!m_NamesLoaded)
                {
                    loadNames();
                    m_NamesLoaded = true;
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

            articleGradingInfo.CommonWords = getWordsInRange(cleanedWords, m_Common, int.MaxValue, includeNames: true);
            articleGradingInfo.NormalWords = getWordsInRange(cleanedWords, m_Normal, m_Common - 1);
            articleGradingInfo.RareWords = getWordsInRange(cleanedWords, 0, m_Normal - 1);

            articleGradingInfo.RareWordsSyns = articleGradingInfo.RareWords.Distinct().ToDictionary(x => x, x => GetSyns(x, WordType.Rare));

            foreach (string splitOption in sr_SplitOptions)
            {
                i_Article = i_Article.Replace(splitOption, "\0" + splitOption + "\0");
            }

            articleGradingInfo.CleanedWords = cleanedWords;
            articleGradingInfo.Words = i_Article.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);

            articleGradingInfo.Score = CalculateScore(articleGradingInfo.CommonWords.Count, articleGradingInfo.NormalWords.Count, articleGradingInfo.RareWords.Count, articleGradingInfo.CleanedWords.Count);

            return articleGradingInfo;
        }

        private static List<(string, WordType)> GetSyns(string word, WordType wordType)
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
            httpClientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => { return true; };

            var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.ConnectionClose = false;

            var url = $"https://dictionaryapi.com/api/v3/references/thesaurus/json/{word}?key=facc2603-e738-4395-a57e-98fbf25602c4";
            var syns = new List<(string, WordType)>();

            try
            {
                var response = client.GetStringAsync(url).Result;
                var data = (JArray)JsonConvert.DeserializeObject(response);

                if (data.Children<JObject>().Count() == 0) return syns;

                foreach (JObject item in data.Children())
                {
                    if (item["meta"] != null && (string)item["meta"]["id"] == word && item["meta"]["syns"].Count() > 0)
                    {
                        List<List<string>> synsLists = item["meta"]["syns"].ToObject<List<List<string>>>();
                        var optionalSyns = synsLists.SelectMany(x => x)
                                                    .Select(w => (w, TextGrading.AnalyzeWord(w)))
                                                    .Where(p => p.Item2 > wordType)
                                                    .ToList();

                        syns.AddRange(optionalSyns);
                    }
                }

                return syns;
            }
            catch (HttpRequestException e)
            {
                return new List<(string, WordType)>();
            }
        }

        public static WordType AnalyzeWord(string word)
        {
            if (s_Words.ContainsKey(word.ToLower()))
            {
                int wordValue = s_Words[word.ToLower()];

                if (m_Common <= wordValue)
                {
                    return WordType.Common;
                }
                else if (m_Normal <= wordValue && wordValue <= m_Common - 1)
                {
                    return WordType.Normal;
                }
            }

            return WordType.Rare;
        }

        private static List<string> getWordsInRange(List<string> i_Words, int i_MinValue, int i_MaxValue, bool includeNames = false)
        {
            List<string> wordsInRange = new List<string>();

            foreach (string word in i_Words)
            {
                if (s_Names.Contains(word))
                {
                    if (includeNames)
                    {
                        wordsInRange.Add(word);
                    }
                }
                else if (s_Words.ContainsKey(word.ToLower()))
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

        public static int CalculateScore(int i_AmountOfCommonWord, int i_AmountOfNormalWord, int i_AmountOfRareWord, int i_AmountOfCleanWords)
        {
            double score = i_AmountOfNormalWord * 0.5f + i_AmountOfRareWord * 1;
            score *= 100f / i_AmountOfCleanWords;
            score = Math.Round(100 - score);

            return (int)score;
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
            string instancesMatrixRelativePath;

            switch (Lang)
            {
                case Language.Hebrew2012_2017:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\Hebrew2012-2017.csv";
                    m_Common = 100;
                    m_Normal = 5;
                    break;
                case Language.Hebrew2012_2015:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\Hebrew2012-2015.csv";
                    m_Common = 100;
                    m_Normal = 5;
                    break;
                case Language.English2014_2017:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\2024DataUKUS2014-2017.csv";
                    m_Common = 1000;
                    m_Normal = 50;
                    break;
                case Language.English2013_2016:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\2024DataUKUS2013-2016.csv";
                    m_Common = 1000;
                    m_Normal = 50;
                    break;
                case Language.English2012_2015:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\2024DataUKUS2012-2015.csv";
                    m_Common = 1000;
                    m_Normal = 50;
                    break;
                case Language.English2015_2018:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\2024DataUKUS2015-2018.csv";
                    m_Common = 1000;
                    m_Normal = 50;
                    break;
                case Language.English2016_2019:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\2024DataUKUS2016-2019.csv";
                    m_Common = 1000;
                    m_Normal = 50;
                    break;                
                case Language.English2017_2020:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\2024DataUKUS2017-2020.csv";
                    m_Common = 1000;
                    m_Normal = 50;
                    break;
                case Language.English2018_2021:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\2024DataUKUS2018-2021.csv";
                    m_Common = 1000;
                    m_Normal = 50;
                    break;
                case Language.English2019_2022:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\2024DataUKUS2019-2022.csv";
                    m_Common = 1000;
                    m_Normal = 50;
                    break;
                case Language.English2020_2023:
                default:
                    instancesMatrixRelativePath = @"~\InstanceMatrices\2024DataUKUS2020-2023.csv";
                    m_Common = 1000;
                    m_Normal = 50;
                    break;
            }

            string instancesMatrixFullPath;

            if (HttpContext.Current == null)
            {
                instancesMatrixFullPath = Path.Combine(Environment.CurrentDirectory, instancesMatrixRelativePath.Substring(2));
            }
            else
            {
                instancesMatrixFullPath = HttpContext.Current.Server.MapPath(instancesMatrixRelativePath);
            }

            if (string.IsNullOrEmpty(instancesMatrixFullPath)) return;

            TextReader data = new StreamReader(new FileStream(instancesMatrixFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var csv = new CsvReader(data, new CsvConfiguration(CultureInfo.CurrentCulture) { HasHeaderRecord = false });

            s_Words.Clear();

            while (csv.Read())
            {
                s_Words.Add(csv[0], int.Parse(csv[1]));
            }
            data.Close();
        }

        private static void loadNames()
        {
            string namesPath;

            if (HttpContext.Current ==  null)
            {
                namesPath = Path.Combine(Environment.CurrentDirectory, @"InstanceMatrices\names.csv");
            }
            else
            {
                namesPath = HttpContext.Current.Server.MapPath(@"~\InstanceMatrices\names.csv");
            }

            if (string.IsNullOrEmpty(namesPath)) return;

            TextReader data = new StreamReader(new FileStream(namesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var csv = new CsvReader(data, new CsvConfiguration(CultureInfo.CurrentCulture) { HasHeaderRecord = false });

            s_Names.Clear();

            while (csv.Read())
            {
                s_Names.Add(csv[0]);
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

            /*if(Lang == Language.Hebrew)
            {
                article = DecodeFromUtf8(article);
            }*/

            return article;
        }

        public static string DecodeFromUtf8(string utf8String)
        {
            // copy the string as UTF-8 bytes.
            byte[] utf8Bytes = new byte[utf8String.Length];
            for (int i = 0; i < utf8String.Length; ++i)
            {
                //Debug.Assert( 0 <= utf8String[i] && utf8String[i] <= 255, "the char must be in byte's range");
                utf8Bytes[i] = (byte)utf8String[i];
            }

            return Encoding.UTF8.GetString(utf8Bytes, 0, utf8Bytes.Length);
        }

    }
}