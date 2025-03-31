using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using JargonProject.Handlers;
using JargonProject.Helpers;
using JargonProject.Models;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Xceed.Document.NET;
using Xceed.Words.NET;
using Font = System.Drawing.Font;

namespace JargonProject.Controllers
{
    public class TextGradingController : Controller
	{
		static Microsoft.Office.Interop.Word.Application winword;
        private readonly SupabaseClient supabaseClient = (SupabaseClient)System.Web.Http.GlobalConfiguration.Configuration.Properties["SupabaseClient"];


        [Table("dejargonizer_user_interactions")]
        public class UserInteraction : BaseModel
        {
            [Column("user_id")]
            public string UserId { get; set; }
            [Column("time")]
            public DateTime Time { get; set; }

            [Column("dictionary")]
            public string Dictionary { get; set; }
            [Column("text")]
            public string Text { get; set; }
            [Column("result_text")]
            public string ResultText { get; set; }
            [Column("jargon")]
            public string Jargon { get; set; }
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

        // GET: TextGrading
        public ActionResult Index()
		{
			var termsConsent = Request.Cookies["terms_consent"];
            var token = Request.Cookies["authToken"]?.Value;
            var userId = supabaseClient.GetUserId(token);

            if (userId == null && (termsConsent == null || string.IsNullOrEmpty(termsConsent.Value) && termsConsent.Value == "false"))
            {
				return Redirect("/");
            }

            return View();
		}


		[HttpPost]
		public JsonResult Grade(string text, string timePriod)
		{
			Language timePeriodLanguage;

			if (Enum.TryParse(timePriod, out timePeriodLanguage))
			{
				TextGrading.Lang = timePeriodLanguage;

				var gradingInfo = TextGrading.AnalyzeSingleText(text);

				return Json(gradingInfo);
			}

			return Json(null);
		}

		[HttpPost]
		public async Task<ActionResult> Index(string ContentTA, HttpPostedFileBase ArticleFU, string timePriodDDL)
		{
			ArticleGradingInfo articleGradingInfo = null;
			bool fileCantBeGraded = false;
			string text = null;

            var token = Request.Cookies["authToken"].Value;
            var userId = supabaseClient.GetUserId(token);
            
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
						text = TextGrading.LoadTextFromFile(ArticleFU.InputStream, extension);
						articleGradingInfo = TextGrading.AnalyzeSingleText(text);
						articleGradingInfo.Name = ArticleFU.FileName.Substring(0, ArticleFU.FileName.LastIndexOf('.'));

						Logger.UpdateNumberOfUses(1);
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
				articleGradingInfo = TextGrading.AnalyzeSingleText(ContentTA);

				Logger.UpdateNumberOfUses(1);
                await SaveToSupabase(articleGradingInfo, userId);
            }


            if (fileCantBeGraded)
            {
				articleGradingInfo = new ArticleGradingInfo();
				articleGradingInfo.Error = "File can not be graded.";
			}

			articleGradingInfo.Lang = TextGrading.Lang;

			return View(articleGradingInfo);
		}

        private async Task SaveToSupabase(ArticleGradingInfo articleGradingInfo, string userId)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var isSaveUserData = await supabaseClient.getIsSaveUserData(userId);

            var text = new StringBuilder();

            foreach (string word in articleGradingInfo.Words)
            {
                string cleanedWord = TextGrading.CleanWord(word).ToLower();
                string type = "commonWord";

                if (articleGradingInfo.RareWords.Contains(cleanedWord))
                {
                    type = "rareWord";
                }
                else if (articleGradingInfo.NormalWords.Contains(cleanedWord))
                {
                    type = "normalWord";
                }

                text.AppendFormat("<span class='{0}'>{1}</span>", type, word == "\r\n" ? "<br />" : word);
            }

            var result = text.ToString();

            var data = new UserInteraction
            {
                UserId = isSaveUserData ? userId : null,
                Time = DateTime.Now,
				
				Dictionary = TextGrading.Lang.ToString(),
				Text = isSaveUserData ? articleGradingInfo.Content : null,
                ResultText = isSaveUserData ? result : null,
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
                await supabaseClient.client.From<UserInteraction>().Insert(data);
                Debug.WriteLine("Data successfully saved to Supabase.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving data to Supabase: {ex.Message}");
            }
        }

        [HttpPost]
		public ActionResult Download()
		{
			try
			{
				ArticleGradingInfo articleGradingInfo = TempData["articleInfo"] as ArticleGradingInfo;

				string name, path;
				name = articleGradingInfo.Name == null ? "Article" : articleGradingInfo.Name;
				path = HttpContext.Server.MapPath($@"~\TempFiles\{name}.docx");

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
				Logger.Instance.WriteLine("An error occurred while trying to download Article {0}", e.Message);
				return RedirectToAction("Error", "HomeController");
			}
		}

		[HttpGet]
		public ActionResult ImageResult(string commonScore, string midScore, string rareScore, string totalScore, string numberOfWords)
		{
			return View("Result", new string[] { commonScore, midScore, rareScore, totalScore, numberOfWords });
		}

		[HttpGet]
		public ActionResult DynamicImage(string commonScore, string midScore, string rareScore, string totalScore, string numberOfWords)
		{
			int Width = 320,
				Height = 500;

			float angle = scoreToDeg(int.Parse(rareScore.Split(new char[] {'%'})[0]));

			using (Bitmap image = new Bitmap(Width, Height))
			{
				using (Graphics g = Graphics.FromImage(image))
				{
					string siteName = "De-Jargonizer.",
						siteURL = "scienceandpublic.com",
						scores =
$@"Common:		{commonScore}

Mid - 
Frequency:		{midScore}

Rare:			{rareScore}

Suitability for 
general audience 
score:			{totalScore}

Number Of Words:	{numberOfWords}";

					Font drawFont = new Font("Century Gothic", 12),
						siteNameFont = new Font("Century Gothic", 14),
						siteURLFont = new Font("Century Gothic", 11);

					Brush siteInfoBrush = Brushes.White,
						  scoresBrush = Brushes.Black;

					float siteNameY = Height - 27,
						  siteURLY = siteNameY + 2.5f,
						  scoresY = 200,
						  siteNameX = 2,
						  siteURLX = Width - g.MeasureString(siteURL, siteURLFont).Width - 2.5f,
						  scoresX = (Width - g.MeasureString(scores, drawFont).Width) / 2,

						  scalaX = 16,
						  scalaY = 10;

					g.Clear(Color.White);

					g.DrawRectangle(
						Pens.Black,
						new Rectangle(5, 5, Width - 10, Height - 40));

					g.FillRectangle(Brushes.Black,
						new Rectangle(0, Height - 30, Width, 30));

					g.DrawString(siteName, siteNameFont, siteInfoBrush, new PointF(siteNameX, siteNameY));
					g.DrawString(siteURL, siteURLFont, siteInfoBrush, new PointF(siteURLX, siteURLY));
					g.DrawString(scores, drawFont, scoresBrush, new PointF(scoresX, scoresY));

					Bitmap s = CreateScala(angle);
					g.DrawImage(s, scalaX, scalaY);
				}

				MemoryStream ms = new MemoryStream();

				image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

				return File(ms.ToArray(), "image/png");
			}
		}

		public ActionResult Scala(float score)
		{
			using (Bitmap image = CreateScala(scoreToDeg(score)))
			{
				MemoryStream ms = new MemoryStream();

				image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

				return File(ms.ToArray(), "image/png");
			}
		}

		private Bitmap CreateScala(float i_Angle)
		{
			Bitmap scala = new Bitmap(
							HttpContext.Server.MapPath("~/Content/Assets/scala.png")
							);

			float scalaWidth = 290,
				  ratio = scalaWidth / scala.Width,
				  scalaHeight = ratio * scala.Height;

			Bitmap image = new Bitmap((int)scalaWidth, (int)scalaHeight);

			using (Graphics g = Graphics.FromImage(image))
			{
				Bitmap scalaPointer = new Bitmap(
					new Bitmap(HttpContext.Server.MapPath("~/Content/Assets/pointer.png"))
				);

				float scalaPointerWidth = ratio * scalaPointer.Width * 0.95f,
					  scalaPointerHeight = ratio * scalaPointer.Height * 0.95f;

				scalaPointer = ScaleImage(scalaPointer, new Size((int)scalaPointerWidth, (int)scalaPointerHeight));

				float sin = (float)Math.Abs(Math.Sin(i_Angle * Math.PI / 180.0)), // this function takes radians
					cos = (float)Math.Abs(Math.Cos(i_Angle * Math.PI / 180.0)), // this one too
					newImgCenterX = sin * scalaPointer.Height + cos * scalaPointer.Width / 2,
					newImgCenterY = sin * scalaPointer.Width + cos * scalaPointer.Height,

					scalaPointerX = (scalaWidth - scalaPointerWidth) / 2,
					scalaPointerY = 43.5f,
					dX,
					dY;

				scalaPointer = RotateImg(
					scalaPointer,
					i_Angle,
					new Point(scalaPointer.Width / 2, scalaPointer.Height),
					out dX,
					out dY
				);

				g.DrawImage(scala, new RectangleF(0, 0, scalaWidth, scalaHeight));
				g.DrawImage(scalaPointer, new PointF(scalaPointerX + dX, scalaPointerY + dY));
			}

			return image;
		}

		private static Bitmap ScaleImage(Bitmap bmp, Size size)
		{
			Bitmap scaledImage = new Bitmap(size.Width, size.Height);

			using (Graphics g = Graphics.FromImage(scaledImage))
			{
				g.DrawImage(bmp, new RectangleF(0, 0, size.Width, size.Height)); //draw the image on the new bitmap
			}

			return scaledImage;
		}

		private static PointF pointAfterRotationAroundOrigin(PointF i_Point, Point origin, float angle)
		{
			double radAngle = angle * Math.PI / 180.0;

			float sin = (float)Math.Sin(radAngle), // this function takes radians
				  cos = (float)Math.Cos(radAngle); // this one too

			PointF point = new PointF(i_Point.X - origin.X, i_Point.Y - origin.Y);

			return new PointF(point.X * cos - point.Y * sin,
								point.X * sin + point.Y * cos);
		}

		private static Bitmap RotateImg(Bitmap bmp, float angle, Point origin, out float deltaX, out float deltaY)
		{
			angle = angle % 360;

			if (angle > 180)
			{
				angle -= 360;
			}

			double radAngle = angle * Math.PI / 180.0;

			float sin = (float)Math.Abs(Math.Sin(radAngle)), // this function takes radians
				  cos = (float)Math.Abs(Math.Cos(radAngle)), // this one too
				  newImgWidth = sin * bmp.Height + cos * bmp.Width,
				  newImgHeight = sin * bmp.Width + cos * bmp.Height;

			PointF[] boundPs = {
				new PointF(0, 0),                 // topLeft
				new PointF(0, bmp.Height),		  // bottomLeft
				new PointF(bmp.Width, 0),		  // topRight
				new PointF(bmp.Width, bmp.Height) // bottomRight
			},
					 relBoundPs = {
				new PointF(boundPs[0].X - origin.X, boundPs[0].Y - origin.Y),
				new PointF(boundPs[1].X - origin.X, boundPs[1].Y - origin.Y),
				new PointF(boundPs[2].X - origin.X, boundPs[2].Y - origin.Y),
				new PointF(boundPs[3].X - origin.X, boundPs[3].Y - origin.Y)
			},

					 afterRotationBoundPs = {
				pointAfterRotationAroundOrigin(boundPs[0], origin, angle),
				pointAfterRotationAroundOrigin(boundPs[1], origin, angle),
				pointAfterRotationAroundOrigin(boundPs[2], origin, angle),
				pointAfterRotationAroundOrigin(boundPs[3], origin, angle)
			};

			int minIndexX = afterRotationBoundPs.Select((item, i) => new { item, i }).Where(o => o.item.X == afterRotationBoundPs.Min(p => p.X)).First().i,
				minIndexY = afterRotationBoundPs.Select((item, i) => new { item, i }).Where(o => o.item.Y == afterRotationBoundPs.Min(p => p.Y)).First().i;

			deltaX = afterRotationBoundPs[minIndexX].X - relBoundPs[0].X;
			deltaY = afterRotationBoundPs[minIndexY].Y - relBoundPs[0].Y;

			Matrix mat = new Matrix();
			mat.Translate(-deltaX, -deltaY);
			mat.RotateAt(angle, origin);

			Bitmap newImg = new Bitmap((int)newImgWidth, (int)newImgHeight);
			Graphics g = Graphics.FromImage(newImg);
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
			g.Transform = mat;
			g.DrawImageUnscaled(bmp, 0, 0); // draw the image at 0, 0
			g.Dispose();

			return newImg;
		}

		private static float scoreToDeg(float score)
		{
			float deg = 0;

			if(score <= 0)
			{
				deg = -1;
			}
			else if(score >= 10)
			{
				deg = 1;
			}
			else
			{
				deg = (score - 5) / 5;
			}

			return deg * 85;
		}

		private DocX CreateDocX(ArticleGradingInfo i_ArticleGradingInfo, string i_Path)
		{
			string templatePath = HttpContext.Server.MapPath(@"~\Content\Assets\templateEng.docx");

			using (DocX doc = DocX.Load(templatePath))
			{
				string statStr = string.Format("Common: {0}%, {1}  \nNormal: {2}%, {3} \nRare: {4}%, {5} \nScore: {6} \nNumber Of Words: {7}\n\n\n"
	, Math.Round((double)i_ArticleGradingInfo.CommonWords.Count / i_ArticleGradingInfo.CleanedWords.Count * 100), i_ArticleGradingInfo.CommonWords.Count
	, Math.Round((double)i_ArticleGradingInfo.NormalWords.Count / i_ArticleGradingInfo.CleanedWords.Count * 100), i_ArticleGradingInfo.NormalWords.Count
	, Math.Round((double)i_ArticleGradingInfo.RareWords.Count / i_ArticleGradingInfo.CleanedWords.Count * 100), i_ArticleGradingInfo.RareWords.Count
	, Math.Round(100 - ((i_ArticleGradingInfo.NormalWords.Count * 0.5f + i_ArticleGradingInfo.RareWords.Count) * 100 / i_ArticleGradingInfo.CleanedWords.Count)).ToString()
	, i_ArticleGradingInfo.CleanedWords.Count.ToString());

				doc.Paragraphs[0].ReplaceText("statVal", statStr);

				Paragraph wordParagraph = doc.Paragraphs[1];

				foreach (string word in i_ArticleGradingInfo.Words)
				{
					string cleanedWord = TextGrading.CleanWord(word).ToLower();

					wordParagraph.Append(word);
					wordParagraph.Font("Arial");
					wordParagraph.FontSize(12D);
					wordParagraph.Bold();

					if (i_ArticleGradingInfo.RareWords.Contains(cleanedWord))
					{
						wordParagraph.Color(System.Drawing.Color.Red);
					}
					else if (i_ArticleGradingInfo.NormalWords.Contains(cleanedWord))
					{
						wordParagraph.Color(Color.Orange);
					}
					else
					{
						wordParagraph.Color(System.Drawing.Color.Black);
					}

					wordParagraph.Alignment = Alignment.left;
					wordParagraph.Direction = Direction.LeftToRight;
				}

				doc.SaveAs(i_Path);

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