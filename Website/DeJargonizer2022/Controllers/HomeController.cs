using Google.Apis.AnalyticsReporting.v4.Data;
using Google.Apis.AnalyticsReporting.v4;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using System.Collections.Generic;
using System;
using System.IO;
using System.Web.Mvc;

namespace JargonProject.Controllers
{
    public class HomeController : BaseController
    {
        async public void getActiveUsersByCountry()
        {
            // Replace with your view (profile) ID from Google Analytics
            string viewId = "YOUR_VIEW_ID";
            // Path to the service account credentials
            string serviceAccountKeyFilePath = "path_to_your_service_account_key.json";

            // Create credentials using the service account file
            GoogleCredential credential;
            using (var stream = new FileStream(serviceAccountKeyFilePath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(AnalyticsReportingService.Scope.AnalyticsReadonly);
            }

            // Create the Analytics Reporting Service
            var reportingService = new AnalyticsReportingService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Analytics Reporting API Sample",
            });

            // Create the request to get active users by country
            var dateRange = new DateRange { StartDate = "30daysAgo", EndDate = "today" };
            var metric = new Metric { Expression = "ga:activeUsers", Alias = "Active Users" };
            var dimension = new Dimension { Name = "ga:country" };

            var reportRequest = new ReportRequest
            {
                ViewId = viewId,
                DateRanges = new List<DateRange> { dateRange },
                Metrics = new List<Metric> { metric },
                Dimensions = new List<Dimension> { dimension }
            };

            var getReportsRequest = new GetReportsRequest
            {
                ReportRequests = new List<ReportRequest> { reportRequest }
            };

            // Send the request
            var response = await reportingService.Reports.BatchGet(getReportsRequest).ExecuteAsync();

            // Process the response
            foreach (var report in response.Reports)
            {
                foreach (var row in report.Data.Rows)
                {
                    string country = row.Dimensions[0];
                    string activeUsers = row.Metrics[0].Values[0];
                    Console.WriteLine($"{country}: {activeUsers} active users");
                }
            }
        }

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