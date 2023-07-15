using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Mvc;
using JargonProject.Handlers;
using JargonProject.Helpers;
using JargonProject.Models;

namespace JargonProject.Controllers
{
    public class GroupGradingController : BaseController
    {
        // GET: FolderGrading
        public ActionResult Index()
        {
            return View(new GroupGradingInfo{ GroupGradingStatus = eGroupGradingStatus.PreSubmit });
        }

        [HttpPost]
        public ActionResult Index(IEnumerable<HttpPostedFileBase> ArticlesGroupFU)
        {
	        TextGrading.Lang = Language.English2016_2019;
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

                Logger.UpdateNumberOfUses(articlesGradingInfo.Count);

                TempData["CSVFile"] = csv.ToString();
                TempData.Keep("CSVFile");

                groupInfo = new GroupGradingInfo { GroupGradingStatus = eGroupGradingStatus.FileGenerated};
            }
            else
            {
                groupInfo = new GroupGradingInfo { GroupGradingStatus = eGroupGradingStatus.ErrorOccurred };
            }

            return View(groupInfo);
        }

        public ActionResult DownloadArticlesFile()
        {
            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(TempData["CSVFile"].ToString());
                return File(bytes, "text/csv", "Results.csv");
            }
            catch (Exception e)
            {
                Logger.Instance.WriteLine("An error occurred while trying to download GroupArticles {0}", e.Message);
                return View(new GroupGradingInfo { GroupGradingStatus = eGroupGradingStatus.PreSubmit });
            }
        }

        private List<ArticleGradingInfo> getArticlesGradingInfoList(IEnumerable<HttpPostedFileBase> i_Articles)
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
                            text = TextGrading.LoadTextFromFile(article.InputStream, extension);

                            currentArticleInfo = TextGrading.AnalyzeSingleText(text);
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