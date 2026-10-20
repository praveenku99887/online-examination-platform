using Ascend.Api.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ascend.Api.Controllers;

[Route("api/health")]
public class HealthController : ApiControllerBase
{
    public HealthController(IOptions<Services.JwtOptions>? jwt = null) : base(jwt) { }

    [HttpGet]
    public async Task<IActionResult> Health()
    {
        try
        {
            await using var conn = await Db.OpenConnectionAsync();
            await conn.ExecuteAsync("select 1");
            return Json(new { ok = true });
        }
        catch
        {
            return StatusCode(500, new { ok = false });
        }
    }
}
