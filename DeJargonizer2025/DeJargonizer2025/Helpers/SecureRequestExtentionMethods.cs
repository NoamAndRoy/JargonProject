using System.Text.Encodings.Web;

namespace JargonProject.Helpers
{
    public static class SecureRequestExtensionMethods
    {
        // Secure GET parameter extraction
        public static string SecureGet(this HttpRequest request, string param)
        {
            return HtmlEncoder.Default.Encode(request.Query[param]);
        }

        // Secure POST parameter extraction
        public static string SecurePost(this HttpRequest request, string param)
        {
            return HtmlEncoder.Default.Encode(request.Form[param]);
        }

        // AntiXss encoding for strings
        public static string AntiXss(this string input)
        {
            return HtmlEncoder.Default.Encode(input);
        }

        // File type verification for uploaded articles
        public static bool VerifyArticleType(this IFormFile file)
        {
            if (file == null || file.Length <= 0)
                return false;

            List<string> acceptedMimeTypes = new()
            {
                "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "text/plain",
                "text/html"
            };

            var fileName = file.FileName?.ToLower() ?? "";

            if (acceptedMimeTypes.Contains(file.ContentType) &&
                (fileName.EndsWith(".txt") || fileName.EndsWith(".docx") || fileName.EndsWith(".htm") || fileName.EndsWith(".html")))
            {
                return true;
            }

            return false;
        }
    }
}