using System;
using System.Text;
using System.Web.Mvc;
using JargonProject.Handlers;

namespace JargonProject
{
    public class HandlerError : HandleErrorAttribute
    {
        public override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.ExceptionHandled || !filterContext.HttpContext.IsCustomErrorEnabled)
            {
                return;
            }

            base.OnException(filterContext);


            StringBuilder errorRoute = new StringBuilder();
            foreach (var route in filterContext.RouteData.Values)
            {
                errorRoute.AppendFormat("{0}: {1}, ", route.Key, route.Value);
            }

            errorRoute.Remove(errorRoute.Length - 3, 1);
            Logger.Instance.WriteLine("An error occurred in {0} - {1}", errorRoute.ToString(), filterContext.Exception.Message);

            filterContext.Result = new RedirectResult("~/Home/Error");
        }
    }
}