using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace DeJargonizer2022
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            var supabaseClient = new SupabaseClient();
            supabaseClient.Init().GetAwaiter().GetResult(); // synchronous block just once at startup
            GlobalConfiguration.Configuration.Properties["SupabaseClient"] = supabaseClient;
        }
    }
}
