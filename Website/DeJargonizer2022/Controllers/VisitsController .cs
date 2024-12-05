using JargonProject.Handlers;
using System.Web.Http;

public class VisitsController : ApiController
{
    [HttpGet]
    public IHttpActionResult GetVisits()
    {
        ulong visitsCount = Logger.ReadAmountOfUses();

        return Ok(visitsCount);
    }
}