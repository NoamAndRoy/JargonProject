using System.Web.Http.Cors;
using System.Web.Http;

public static class WebApiConfig
{
    public static void Register(HttpConfiguration config)
    {
        // Enable CORS
        var cors = new EnableCorsAttribute("http://localhost:5174,http://localhost:5173", "GET,POST,PUT,DELETE", "*")
        {
            SupportsCredentials = true
        };
        config.EnableCors(cors);

        config.MapHttpAttributeRoutes();

        config.Routes.MapHttpRoute(
            name: "DefaultApi",
            routeTemplate: "api/{controller}/{id}",
            defaults: new { id = RouteParameter.Optional }
        );
    }
}
