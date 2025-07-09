using System.Text;
using JargonProject.Handlers;
using JargonProject.Helpers;
using JargonProject.Models;
using JargonProject.Services;
using Microsoft.AspNetCore.Mvc;

namespace JargonProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GroupGradingController : ControllerBase
    {
        private readonly ILogger<TextGradingController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly UsageCounter _usageCounter;

        public GroupGradingController(ILogger<TextGradingController> logger, IWebHostEnvironment env, UsageCounter usageCounter)
        {
            _logger = logger;
            _env = env;
            _usageCounter = usageCounter;
        }

        [HttpPost]
        public ActionResult Index(IEnumerable<IFormFile> ArticlesGroupFU)
        {
            try
            {
                TextGrading.Lang = Language.English2021_2024;
                List<ArticleGradingInfo> articlesGradingInfo = getArticlesGradingInfoList(ArticlesGroupFU);

                GroupGradingInfo groupInfo;

                if (articlesGradingInfo.Count > 0)
                {
                    string common, normal, rare, score, line, numOfWords;

                    StringBuilder csv = new StringBuilder();
                    csv.AppendLine("File Name, Common,, Mid-Frequency,, Rare,, Score, Number Of Words");

                    foreach (ArticleGradingInfo currentArticleInfo in articlesGradingInfo)
                    {
                        common = string.Format("{0}%, {1}", Math.Round((double)currentArticleInfo.CommonWords.Count / currentArticleInfo.CleanedWords.Count * 100), currentArticleInfo.CommonWords.Count);
                        normal = string.Format("{0}%, {1}", Math.Round((double)currentArticleInfo.NormalWords.Count / currentArticleInfo.CleanedWords.Count * 100), currentArticleInfo.NormalWords.Count);
                        rare = string.Format("{0}%, {1}", Math.Round((double)currentArticleInfo.RareWords.Count / currentArticleInfo.CleanedWords.Count * 100), currentArticleInfo.RareWords.Count);
                        score = string.Format("{0}%, {1}", Math.Round(100 - ((currentArticleInfo.NormalWords.Count * 0.5f + currentArticleInfo.RareWords.Count) * 100 / currentArticleInfo.CleanedWords.Count)), currentArticleInfo.CleanedWords.Count);
                        score = string.Format("{0}", Math.Round(100 - ((currentArticleInfo.NormalWords.Count * 0.5f + currentArticleInfo.RareWords.Count) * 100 / currentArticleInfo.CleanedWords.Count)));
                        numOfWords = string.Format("{0}", currentArticleInfo.CleanedWords.Count.ToString());

                        line = string.Format("{0}, {1}, {2}, {3}, {4}, {5}", currentArticleInfo.Name, common, normal, rare, score, numOfWords);
                        csv.AppendLine(line);
                    }

                    _usageCounter.UpdateNumberOfUses(articlesGradingInfo.Count);

                    byte[] bytes = Encoding.ASCII.GetBytes(csv.ToString());
                    return File(bytes, "text/csv", "Results.csv");
                }
                else
                {
                    return Ok("No files were found");
                }
            }
            catch (Exception e)
            {
                _logger.LogError("An error occurred while trying to download GroupArticles {0}", e.Message);
                return Problem("An error occurred while trying to download the file");
            }
        }

        [HttpGet("/DownloadArticlesFile")]
        public ActionResult DownloadArticlesFile([FromBody] string csvFile)
        {
            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(csvFile);
                return File(bytes, "text/csv", "Results.csv");
            }
            catch (Exception e)
            {
                _logger.LogError("An error occurred while trying to download GroupArticles {0}", e.Message);
                return Problem("An error occurred while trying to download the file");
            }
        }

        private List<ArticleGradingInfo> getArticlesGradingInfoList(IEnumerable<IFormFile> i_Articles)
        {
            List<ArticleGradingInfo> articlesGradingInfo = new List<ArticleGradingInfo>();
            string text = null, name;

            if (i_Articles != null)
            {
                foreach (var article in i_Articles)
                {
                    ArticleGradingInfo currentArticleInfo = null;

                    if (article.VerifyArticleType())
                    {
                        try
                        {
                            string extension = article.FileName.ToLower().Substring(article.FileName.LastIndexOf('.') + 1);
                            name = article.FileName.ToLower().Substring(0, article.FileName.LastIndexOf('.'));
                            text = TextGrading.LoadTextFromFile(article.OpenReadStream(), extension);

                            currentArticleInfo = TextGrading.AnalyzeSingleText(text, _env);
                            currentArticleInfo.Name = name + extension;
                        }
                        catch
                        {
                            articlesGradingInfo.Clear();
                            break;
                        }
                    }
                    else
                    {
                        articlesGradingInfo.Clear();
                        break;
                    }

                    if (!string.IsNullOrEmpty(text) && currentArticleInfo.Error == null)
                    {
                        articlesGradingInfo.Add(currentArticleInfo);
                    }
                }
            }

            return articlesGradingInfo;
        }
    }
}