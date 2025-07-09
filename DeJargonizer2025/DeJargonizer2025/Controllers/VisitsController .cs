using JargonProject.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class VisitsController : ControllerBase
{
    private readonly UsageCounter _usageCounter;

    public VisitsController(UsageCounter usageCounter)
    {
        _usageCounter = usageCounter;
    }

    [HttpGet("/GetVisits")]
    public IActionResult GetVisits()
    {
        ulong visitsCount = _usageCounter.ReadAmountOfUses();

        return Ok(visitsCount);
    }
}