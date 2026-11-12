using IIGO.Data;
using IIGO.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IIGO.Api
{
    /// <summary>
    /// Kept for existing callers. New code should use GET /api/websites, which returns the same list.
    /// </summary>
    [Route("api/[controller]")]
    public class IISApiController(ILogger<IISApiController> logger, IISService IISService) : ApiControllerBase(logger)
    {
        [HttpGet("sites")]
        public ActionResult<List<IISSiteInfo>> GetIISSites()
        {
            try
            {
                return IISService.GetSites().Select(WebsitesController.ToSiteInfo).ToList();
            }
            catch (Exception ex)
            {
                return Failed(ex, "loading IIS sites");
            }
        }
    }
}
