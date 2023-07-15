using System.Collections.Generic;
using System.Web;
using System.Web.Security.AntiXss;

namespace JargonProject.Helpers
{
    public static class SecureRequestExtentionMethods
	{
        public static string SecureGet(this HttpRequestBase i_Request, string i_Param)
        {
            return AntiXssEncoder.HtmlEncode(i_Request.QueryString[i_Param], false);
        }

        public static string SecurePost(this HttpRequestBase i_Request, string i_Param)
        {
            return AntiXssEncoder.HtmlEncode(i_Request.Form[i_Param], false);
        }

        public static string AntiXss(this string i_String)
        {
            return AntiXssEncoder.HtmlEncode(i_String, false);
        }

        public static bool VerifyArticleType(this HttpPostedFileBase i_Article)
        {
            List<string> acceptedFiles = new List<string>
            {
                "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "text/plain",
                "text/html"
            };

            if (i_Article.ContentLength > 0 && acceptedFiles.Contains(i_Article.ContentType) &&
                (i_Article.FileName.ToLower().EndsWith(".txt") || i_Article.FileName.ToLower().EndsWith(".docx") || i_Article.FileName.ToLower().EndsWith(".htm") || i_Article.FileName.ToLower().EndsWith(".html")))
            {
                return true;
            }

            return false;
        }
    }
}