using System.Net;
using System.Numerics;
using JargonProject.Handlers;
using JargonProject.Helpers;
using JargonProject.Models;
using JargonProject.Services;
using Microsoft.AspNetCore.Mvc;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Xceed.Document.NET;
using Xceed.Words.NET;
using Color = Xceed.Drawing.Color;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace JargonProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TextGradingController : ControllerBase
    {
        private readonly SupabaseClient _supabaseClient;
        private readonly ILogger<TextGradingController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly UsageCounter _usageCounter;


        static Microsoft.Office.Interop.Word.Application winword;

        public TextGradingController(SupabaseClient supabaseClient, ILogger<TextGradingController> logger, IWebHostEnvironment env, UsageCounter usageCounter)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
            _env = env;
            _usageCounter = usageCounter;
        }

        [Table("dejargonizer_user_interactions")]
        public class UserInteraction : BaseModel
        {
            [Column("user_id")]
            public string? UserId { get; set; }
            [Column("time")]
            public DateTime Time { get; set; }

            [Column("dictionary")]
            public string? Dictionary { get; set; }
            [Column("text")]
            public string? Text { get; set; }
            [Column("result_text")]
            public string? ResultText { get; set; }
            [Column("jargon")]
            public string? Jargon { get; set; }
            [Column("total_words")]
            public int TotalWords { get; set; }
            [Column("rare_words")]
            public int RareWords { get; set; }
            [Column("rare_words_percentage")]
            public double RareWordsPercentage { get; set; }
            [Column("mid_range_words")]
            public int MidRangeWords { get; set; }
            [Column("mid_range_words_percentage")]
            public double MidRangeWordsPercentage { get; set; }
            [Column("jargon_score")]
            public double JargonScore { get; set; }
        }

        //[HttpPost("/Grade")]
        //public JsonResult Grade(string text, string timePriod)
        //{
        //    Language timePeriodLanguage;

        //    if (Enum.TryParse(timePriod, out timePeriodLanguage))
        //    {
        //        TextGrading.Lang = timePeriodLanguage;

        //        var gradingInfo = TextGrading.AnalyzeSingleText(text, _env);

        //        return Json(gradingInfo);
        //    }

        //    return Json(null);
        //}

        [HttpPost("Grade")]
        public async Task<ActionResult> Index([FromForm] string? ContentTA, IFormFile? ArticleFU, [FromForm] string timePriodDDL)
        {
            ArticleGradingInfo? articleGradingInfo = null;
            bool fileCantBeGraded = false;
            string? text = null;

            var userId = await HttpContext.TryGetUserIdAsync();

            Language timePeriodLanguage;
            Enum.TryParse(timePriodDDL, out timePeriodLanguage);
            TextGrading.Lang = timePeriodLanguage;

            if (ArticleFU != null)
            {
                if (ArticleFU.VerifyArticleType())
                {
                    try
                    {
                        string extension = ArticleFU.FileName.ToLower().Substring(ArticleFU.FileName.LastIndexOf('.') + 1);
                        text = TextGrading.LoadTextFromFile(ArticleFU.OpenReadStream(), extension);
                        articleGradingInfo = TextGrading.AnalyzeSingleText(text, _env);
                        articleGradingInfo.Name = ArticleFU.FileName.Substring(0, ArticleFU.FileName.LastIndexOf('.'));

                        _usageCounter.UpdateNumberOfUses(1);
                        await SaveToSupabase(articleGradingInfo, userId);

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
                articleGradingInfo = TextGrading.AnalyzeSingleText(ContentTA, _env);

                _usageCounter.UpdateNumberOfUses(1);
                await SaveToSupabase(articleGradingInfo, userId);
            }


            if (fileCantBeGraded)
            {
                articleGradingInfo = new ArticleGradingInfo();
                articleGradingInfo.Error = "File can not be graded.";
            }

            articleGradingInfo.Lang = TextGrading.Lang;

            return Ok(articleGradingInfo);
        }

        private async Task SaveToSupabase(ArticleGradingInfo articleGradingInfo, string? userId)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var isSaveUserData = await _supabaseClient.getIsSaveUserData(userId);

            var data = new UserInteraction
            {
                UserId = isSaveUserData ? userId : null,
                Time = DateTime.Now,

                Dictionary = TextGrading.Lang.ToString(),
                Text = isSaveUserData ? articleGradingInfo.Content : null,
                ResultText = isSaveUserData ? articleGradingInfo.HtmlResult : null,
                Jargon = string.Join(", ", articleGradingInfo.RareWordsSyns.Keys),
                TotalWords = articleGradingInfo.CleanedWords.Count,
                RareWords = articleGradingInfo.RareWords.Count,
                RareWordsPercentage = articleGradingInfo.RareWords.Count / (double)articleGradingInfo.CleanedWords.Count,
                MidRangeWords = articleGradingInfo.NormalWords.Count,
                MidRangeWordsPercentage = articleGradingInfo.NormalWords.Count / (double)articleGradingInfo.CleanedWords.Count,
                JargonScore = articleGradingInfo.Score,
            };

            try
            {
                await _supabaseClient.client.From<UserInteraction>().Insert(data);
            }
            catch (Exception ex)
            {
            }
        }

        [HttpPost("BuildYourOwnDeJargonizer")]
        public ActionResult BuildYourOwnDeJargonizer()
        {
            string path = Path.Combine(_env.ContentRootPath, "Content", "Assets", "How to build a De-Jargonizer.docx");
            FileStream explanation = new FileStream(path, FileMode.Open, FileAccess.Read);

            return File(explanation, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "How to build a De-Jargonizer.docx");
        }

        [HttpPost("Download")]
        public ActionResult Download([FromBody] ArticleGradingInfoDTO articleGradingInfo)
        {
            try
            {
                string name = string.IsNullOrEmpty(articleGradingInfo.Name) ? "Article" : articleGradingInfo.Name;
                string path = Path.Combine(_env.ContentRootPath, "TempFiles", $"{name}.docx");

                CreateDocX(articleGradingInfo, path);

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
                _logger.LogError("An error occurred while trying to download Article {0}", e.Message);
                return RedirectToAction("Error", "HomeController");
            }
        }


        [HttpGet("Scala")]
        public IActionResult Scala(float score)
        {
            using (Image<Rgba32> image = CreateScala(scoreToDeg(score)))
            {
                using var ms = new MemoryStream();
                image.SaveAsPng(ms);
                return File(ms.ToArray(), "image/png");
            }
        }
        private Image<Rgba32> CreateScala(float i_Angle)
        {
            using Image<Rgba32> scala = SixLabors.ImageSharp.Image.Load<Rgba32>(
                Path.Combine(_env.ContentRootPath, "Content", "Assets", "scala.png"));

            float scalaWidth = 290,
                  ratio = scalaWidth / scala.Width,
                  scalaHeight = ratio * scala.Height;

            Image<Rgba32> image = new((int)scalaWidth, (int)scalaHeight);

            image.Mutate(ctx =>
            {
                ctx.DrawImage(scala.Clone(x => x.Resize((int)scalaWidth, (int)scalaHeight)),
                    1);
                using Image<Rgba32> pointer = SixLabors.ImageSharp.Image.Load<Rgba32>(
                    Path.Combine(_env.ContentRootPath, "Content", "Assets", "pointer.png"));

                float pW = ratio * pointer.Width * 0.95f,
                      pH = ratio * pointer.Height * 0.95f;

                pointer.Mutate(p => p.Resize((int)pW, (int)pH, KnownResamplers.Bicubic));

                Point origin = new(pointer.Width / 2, pointer.Height);
                Image<Rgba32> rotated = RotateImg(pointer, i_Angle, origin,
                                                  out float dX, out float dY);

                float x = (scalaWidth - pW) / 2f + dX;
                float y = 43.5f + dY;


                ctx.DrawImage(rotated, new Point((int)x, (int)y), 1f);
            });

            return image;
        }

        private static Image<Rgba32> RotateImg(Image<Rgba32> bmp, float angle, PointF origin, out float deltaX, out float deltaY)
        {
            angle %= 360;
            if (angle > 180) angle -= 360;

            double rad = angle * Math.PI / 180.0;
            float sin = (float)Math.Abs(Math.Sin(rad));
            float cos = (float)Math.Abs(Math.Cos(rad));

            float newImgWidth = sin * bmp.Height + cos * bmp.Width;
            float newImgHeight = sin * bmp.Width + cos * bmp.Height;

            PointF[] bounds = {
        new(0, 0), new(0, bmp.Height),
        new(bmp.Width, 0), new(bmp.Width, bmp.Height)
    };

            PointF[] rel = bounds.Select(p => new PointF(p.X - origin.X, p.Y - origin.Y)).ToArray();
            PointF[] rot = bounds.Select(p => PointAfterRotationAroundOrigin(p, origin, angle)).ToArray();

            int ixMin = rot.Select((p, i) => (p, i)).OrderBy(t => t.p.X).First().i;
            int iyMin = rot.Select((p, i) => (p, i)).OrderBy(t => t.p.Y).First().i;

            deltaX = rot[ixMin].X - rel[0].X;
            deltaY = rot[iyMin].Y - rel[0].Y;

            var builder = new AffineTransformBuilder(TransformSpace.Pixel)           // GDI works in pixel-centre space
                .PrependTranslation(new Vector2(-deltaX - 0.5f, -deltaY - 0.5f))     // first: move into view + half-pixel fix
                .PrependRotationDegrees(angle, origin);                              // second: rotate about bottom-centre

            using Image<Rgba32> rotated = bmp.Clone(ctx =>
                ctx.Transform(builder, KnownResamplers.Triangle));                   // bilinear == HighQualityBilinear

            Image<Rgba32> canvas =
                new((int)MathF.Ceiling(newImgWidth), (int)MathF.Ceiling(newImgHeight), SixLabors.ImageSharp.Color.Transparent);

            var gfx = new GraphicsOptions
            {
                AlphaCompositionMode = PixelAlphaCompositionMode.Src,                // mimic DrawImageUnscaled
                ColorBlendingMode = PixelColorBlendingMode.Normal,
                Antialias = false,
                BlendPercentage = 1f
            };

            canvas.Mutate(c => c.DrawImage(rotated, new Point(0, 0), gfx));
            return canvas;
        }

        private static PointF PointAfterRotationAroundOrigin(
        PointF point, PointF origin, float angle)
        {
            double rad = angle * Math.PI / 180.0;
            float sin = (float)Math.Sin(rad);
            float cos = (float)Math.Cos(rad);

            float relX = point.X - origin.X;
            float relY = point.Y - origin.Y;

            return new PointF(
                relX * cos - relY * sin,
                relX * sin + relY * cos);
        }
        private static float scoreToDeg(float score)
        {
            float deg = 0;

            if (score <= 0)
            {
                deg = -1;
            }
            else if (score >= 10)
            {
                deg = 1;
            }
            else
            {
                deg = (score - 5) / 5;
            }

            return deg * 85;
        }

        private DocX CreateDocX(ArticleGradingInfoDTO articleGradingInfoDto, string path)
        {
            string templatePath = Path.Combine(_env.ContentRootPath, "Content", "Assets", "templateEng.docx");

            using (DocX doc = DocX.Load(templatePath))
            {
                var totalWords = articleGradingInfoDto.CleanedWords.Count;
                var rareWords = articleGradingInfoDto.RareWords.Count;
                var midRangeWords = articleGradingInfoDto.NormalWords.Count;
                var commonWords = totalWords - rareWords - midRangeWords;

                string statStr = string.Format("Common: {0}%, {1}  \nNormal: {2}%, {3} \nRare: {4}%, {5} \nScore: {6} \nNumber Of Words: {7}\n\n\n"
    , Math.Round((double)commonWords / totalWords * 100), commonWords
    , Math.Round((double)midRangeWords / totalWords * 100), midRangeWords
    , Math.Round((double)rareWords / totalWords * 100), rareWords
    , Math.Round(100 - ((midRangeWords * 0.5f + rareWords) * 100 / totalWords)).ToString()
    , totalWords.ToString());

                doc.Paragraphs[0].ReplaceText("statVal", statStr);

                Paragraph wordParagraph = doc.Paragraphs[1];

                foreach (string word in articleGradingInfoDto.Words)
                {
                    string cleanedWord = TextGrading.CleanWord(word).ToLower();

                    wordParagraph.Append(word);
                    wordParagraph.Font("Arial");
                    wordParagraph.FontSize(12D);
                    wordParagraph.Bold();

                    if (articleGradingInfoDto.RareWords.Contains(cleanedWord))
                    {
                        wordParagraph.Color(Color.Red);
                    }
                    else if (articleGradingInfoDto.NormalWords.Contains(cleanedWord))
                    {
                        wordParagraph.Color(Color.Orange);
                    }
                    else
                    {
                        wordParagraph.Color(Color.Black);
                    }

                    wordParagraph.Alignment = Alignment.left;
                    wordParagraph.Direction = Direction.LeftToRight;
                }

                doc.SaveAs(path);

                return doc;
            }
        }

        private void CreateDocument(ArticleGradingInfo i_ArticleGradingInfo, string i_Path)
        {
            object missing = System.Reflection.Missing.Value;

            winword.ShowAnimation = false;
            winword.Visible = false;

            Microsoft.Office.Interop.Word.Document document = winword.Documents.Add(ref missing, ref missing, ref missing, ref missing);

            try
            {
                string statStr = string.Format("Common: {0}%, {1}  \nNormal: {2}%, {3} \nRare: {4}%, {5} \nScore: {6} \nNumber Of Words: {7}\n"
                        , Math.Round((double)i_ArticleGradingInfo.CommonWords.Count / i_ArticleGradingInfo.CleanedWords.Count * 100), i_ArticleGradingInfo.CommonWords.Count
                        , Math.Round((double)i_ArticleGradingInfo.NormalWords.Count / i_ArticleGradingInfo.CleanedWords.Count * 100), i_ArticleGradingInfo.NormalWords.Count
                        , Math.Round((double)i_ArticleGradingInfo.RareWords.Count / i_ArticleGradingInfo.CleanedWords.Count * 100), i_ArticleGradingInfo.RareWords.Count
                        , Math.Round(100 - ((i_ArticleGradingInfo.NormalWords.Count * 0.5f + i_ArticleGradingInfo.RareWords.Count) * 100 / i_ArticleGradingInfo.CleanedWords.Count)).ToString()
                        , i_ArticleGradingInfo.CleanedWords.Count.ToString());

                document.Paragraphs.SpaceBefore = 0;
                document.Paragraphs.SpaceAfter = 0;

                Microsoft.Office.Interop.Word.Paragraph headline = document.Content.Paragraphs.Add(ref missing);
                headline.Range.Text = "Result:";
                headline.Range.Font.Name = "Arial Narrow";
                headline.Range.Font.Size = 15;
                headline.Range.Font.Bold = 1;
                headline.Range.Font.Underline = Microsoft.Office.Interop.Word.WdUnderline.wdUnderlineSingle;
                headline.Alignment = Microsoft.Office.Interop.Word.WdParagraphAlignment.wdAlignParagraphRight;
                headline.ReadingOrder = Microsoft.Office.Interop.Word.WdReadingOrder.wdReadingOrderLtr;
                headline.Range.InsertParagraphAfter();

                Microsoft.Office.Interop.Word.Paragraph stat = document.Content.Paragraphs.Add(ref missing);
                stat.Range.Font.Name = "Arial Narrow";
                stat.Range.Font.Size = 13;
                stat.Range.Font.Bold = 0;
                stat.Range.Font.Underline = Microsoft.Office.Interop.Word.WdUnderline.wdUnderlineNone;
                stat.SpaceBefore = 0;
                stat.SpaceAfter = 0;
                stat.Alignment = Microsoft.Office.Interop.Word.WdParagraphAlignment.wdAlignParagraphLeft;
                stat.ReadingOrder = Microsoft.Office.Interop.Word.WdReadingOrder.wdReadingOrderLtr;
                stat.Range.Text = statStr;
                stat.Range.InsertParagraphAfter();

                Microsoft.Office.Interop.Word.Paragraph text = stat.Range.Paragraphs.Add(ref missing);
                text.Alignment = Microsoft.Office.Interop.Word.WdParagraphAlignment.wdAlignParagraphLeft;
                text.ReadingOrder = Microsoft.Office.Interop.Word.WdReadingOrder.wdReadingOrderLtr;
                text.SpaceAfter = 0;
                text.SpaceBefore = 0;

                Microsoft.Office.Interop.Word.Section word = text.Range.Sections.Add(ref missing);

                /*string article = string.Join("", i_ArticleGradingInfo.Words);
				text.Range.Text = article;
				text.Range.InsertParagraph();*/

                for (int i = i_ArticleGradingInfo.Words.Length - 1; i >= 0; i--)
                {
                    word = text.Range.Sections.Add(ref missing);
                    string cleanedWord = TextGrading.CleanWord(i_ArticleGradingInfo.Words[i]).ToLower();

                    if (i_ArticleGradingInfo.RareWords.Contains(cleanedWord))
                    {
                        word.Range.Font.Color = Microsoft.Office.Interop.Word.WdColor.wdColorRed;
                    }
                    else if (i_ArticleGradingInfo.NormalWords.Contains(cleanedWord))
                    {
                        word.Range.Font.Color = Microsoft.Office.Interop.Word.WdColor.wdColorOrange;
                    }
                    else
                    {
                        word.Range.Font.Color = Microsoft.Office.Interop.Word.WdColor.wdColorBlack;
                    }

                    word.Range.Font.Name = "Arial Narrow";
                    word.Range.Font.Size = 12;
                    word.Range.Font.Bold = 1;
                    word.Range.Font.Underline = Microsoft.Office.Interop.Word.WdUnderline.wdUnderlineNone;
                    word.Range.Text = i_ArticleGradingInfo.Words[i];
                }
                word.Range.InsertParagraphBefore();

                RemoveAllSectionBreaks(document);

                //Save the document
                object filename = i_Path;
                document.SaveAs2(ref filename);
            }
            catch (Exception e) { }
            finally
            {
                document.Close(ref missing, ref missing, ref missing);
                document = null;
            }
        }

        private void RemoveAllSectionBreaks(Microsoft.Office.Interop.Word.Document doc)
        {
            Microsoft.Office.Interop.Word.Sections sections = doc.Sections;
            foreach (Microsoft.Office.Interop.Word.Section section in sections)
            {
                section.Range.Select();
                Microsoft.Office.Interop.Word.Selection selection = doc.Application.Selection;
                object unit = Microsoft.Office.Interop.Word.WdUnits.wdCharacter;
                object count = 1;
                object extend = Microsoft.Office.Interop.Word.WdMovementType.wdExtend;
                object missing = System.Reflection.Missing.Value;

                selection.MoveRight(ref unit, ref count, ref missing);
                selection.MoveLeft(ref unit, ref count, ref extend);
                selection.Delete(ref unit, ref count);
            }
        }
    }
}