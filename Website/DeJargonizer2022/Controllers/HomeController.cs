using System;
using System.IO;
using System.Web.Mvc;

namespace JargonProject.Controllers
{
    public class HomeController : BaseController
    {
        public ActionResult ServeApp()
        {
            var path = Server.MapPath("~/Content/dist/index.html");
            return File(path, "text/html");
        }

        public ActionResult Error()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Error(string aspxerrorpath)
        {
            if (!string.IsNullOrWhiteSpace(aspxerrorpath))
                return RedirectToAction("Error");

            return View();
        }

        public ActionResult About()
        {
            return View("About");
        }

        public ActionResult Instructions()
        {
            return View("Instructions");
        }

        public ActionResult Examples()
        {
            return View("Examples");
        }

        public ActionResult Development()
        {
            return View("Development");
        }

        public ActionResult Developers()
        {
            return View("Developers");
        }

        public ActionResult HowToCite()
        {
            return View("How to cite");
        }

        public ActionResult ContactUs()
        {
            return View("Contact Us");
        }

		public ActionResult BuildYourOwnDeJargonizer()
        {
			FileStream explanation = new FileStream(HttpContext.Server.MapPath(@"~\Content\Assets\How to build a De-Jargonizer.docx"), FileMode.Open, FileAccess.Read);

			return File(explanation, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "How to build a De-Jargonizer.docx");
		}
	}
}