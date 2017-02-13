using Novacode;
using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using JargonProject.Handlers;
using JargonProject.Helpers;
using JargonProject.Models;

namespace JargonProject.Controllers
{
    public class TextGradingController : BaseController
    {
        // GET: TextGrading
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Index(string ContentTA, HttpPostedFileBase ArticleFU)
        {
            ArticleGradingInfo articleGradingInfo = null;
            bool fileCantBeGraded = false;
            string text = null;

            if (ArticleFU != null)
            {
                if (ArticleFU.VerifyArticleType())
                {
                    try
                    {
                        string extension = ArticleFU.FileName.ToLower().Substring(ArticleFU.FileName.LastIndexOf('.') + 1);
                        text = TextGrading.LoadTextFromFile(ArticleFU.InputStream, extension);
                        articleGradingInfo = TextGrading.AnalyzeSingleText(text);
                        articleGradingInfo.Name = ArticleFU.FileName.Substring(0, ArticleFU.FileName.LastIndexOf('.'));

                        Logger.UpdateNumberOfUses(1);
                    }
                    catch
                    {
                        fileCantBeGraded = true;
                    }
                }
                else
                {
                    fileCantBeGraded = true;
                }
            }
            else
            {
                articleGradingInfo = TextGrading.AnalyzeSingleText(ContentTA);
                Logger.UpdateNumberOfUses(1);
            }

            
            if (fileCantBeGraded)
            {
                articleGradingInfo = new ArticleGradingInfo();
                articleGradingInfo.Error = "File can not be graded.";
            }

            return View(articleGradingInfo);
        }

        [HttpPost]
        public ActionResult Download()
        {
            try
            {
                ArticleGradingInfo articleGradingInfo = TempData["articleInfo"] as ArticleGradingInfo;

                string name, path;
                name = articleGradingInfo.Name == null ? "Article" : articleGradingInfo.Name;
                path = HttpContext.Server.MapPath(string.Format(@"~\{0}.docx", name));

                DocX doc = CreateDocX(articleGradingInfo, path);

                FileStream docFile = new FileStream(path, FileMode.Open, FileAccess.Read);
                MemoryStream docMemory = new MemoryStream();
                docFile.Seek(0, SeekOrigin.Begin);
                docFile.CopyTo(docMemory);
                docFile.Close();

                System.IO.File.Delete(path);

                return File(docMemory.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", string.Format("{0} - Result.docx", name));
            }
            catch (Exception e)
            {
                Logger.Instance.WriteLine("An error occurred while trying to download Article {0}", e.Message);
                return RedirectToAction("Error", "HomeController");
            }
        }

        private DocX CreateDocX(ArticleGradingInfo i_ArticleGradingInfo, string i_Path)
        {
            using (DocX doc = DocX.Create(i_Path))
            {
                Paragraph stat = doc.InsertParagraph("Result:").Font(new System.Drawing.FontFamily("Arial")).FontSize(15D).Bold().UnderlineStyle(UnderlineStyle.singleLine);
                stat.AppendLine(string.Format("Common: {0}%, {1}  \nNormal: {2}%, {3} \nRare: {4}%, {5} \nScore: {6} \nNumber Of Words: {7}\n\n\n"
                    , Math.Round((double)i_ArticleGradingInfo.CommonWords.Count / i_ArticleGradingInfo.CleanedWords.Count * 100), i_ArticleGradingInfo.CommonWords.Count
                    , Math.Round((double)i_ArticleGradingInfo.NormalWords.Count / i_ArticleGradingInfo.CleanedWords.Count * 100), i_ArticleGradingInfo.NormalWords.Count
                    , Math.Round((double)i_ArticleGradingInfo.RareWords.Count / i_ArticleGradingInfo.CleanedWords.Count * 100), i_ArticleGradingInfo.RareWords.Count
                    , Math.Round(100 - ((i_ArticleGradingInfo.NormalWords.Count * 0.5f + i_ArticleGradingInfo.RareWords.Count) * 100 / i_ArticleGradingInfo.CleanedWords.Count)).ToString()
                    , i_ArticleGradingInfo.CleanedWords.Count.ToString()));

                stat.FontSize(13D);

                Paragraph wordParagraph = doc.InsertParagraph();

                foreach (string word in i_ArticleGradingInfo.Words)
                {
                    string cleanedWord = TextGrading.CleanWord(word).ToLower();

                    wordParagraph.Append(word);
                    wordParagraph.Font(new System.Drawing.FontFamily("Arial"));
                    wordParagraph.FontSize(12D);
                    wordParagraph.Bold();

                    if (i_ArticleGradingInfo.RareWords.Contains(cleanedWord))
                    {
                        wordParagraph.Color(System.Drawing.Color.Red);
                    }
                    else if (i_ArticleGradingInfo.NormalWords.Contains(cleanedWord))
                    {
                        wordParagraph.Color(System.Drawing.Color.Orange);
                    }
                    else
                    {
                        wordParagraph.Color(System.Drawing.Color.Black);
                    }
                }

                doc.Save();

                return doc;
            }
        }
    }
}