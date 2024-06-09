using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace DeJargonizer2022
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("Content/dist/{*pathInfo}");

            routes.MapMvcAttributeRoutes();

            routes.MapRoute(
                name: "DeJargonizer",
                url: "de-jargonizer/{controller}/{action}/{id}",
                defaults: new { controller = "TextGrading", action = "Index", id = UrlParameter.Optional }
            );

            // Setup a catch-all route to serve the React application from the dist/index.html on other routes
            routes.MapRoute(
                name: "SPA-Fallback",
                url: "{*url}",
                defaults: new { controller = "Home", action = "ServeApp" }
            );
        }
    }
}
