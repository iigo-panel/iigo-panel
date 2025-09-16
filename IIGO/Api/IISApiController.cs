using IIGO.Components;
using IIGO.Data;
using IIGO.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IIGO.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class IISApiController(ILogger<IISApiController> logger, IServiceProvider serviceProvider) : ControllerBase
    {
        readonly ApplicationDbContext _context = serviceProvider.GetService<ApplicationDbContext>()!;

        [HttpGet("sites")]
        public async Task<object> GetIISSites()
        {
            var providedKey = Request.Headers["X-API-KEY"].FirstOrDefault();
            if (String.IsNullOrWhiteSpace(providedKey))
            {
                return Results.Unauthorized();
            }
            var apiKey = _context.ConfigSetting.FirstOrDefault(x => x.SettingName == "ApiKey");
            // TODO: check api key against database
            try
            {
                var sites = await IISService.GetSites();
                return sites.Cast<dynamic>().ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading IIS sites");
                return Results.Problem(statusCode: 500, detail: "Error loading IIS sites");
            }
        }
    }
}
