using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Contracts;
using System;
using Server.Database;

namespace Server.Controllers
{
    [Route("/[controller]")]
    [ApiController]
    public class CompareController : ControllerBase
    {
        private IImagesDB db;
        private CancellationTokenSource cts;
        public CompareController(IImagesDB db)
        {
            this.db = db;
            this.cts = new CancellationTokenSource();
        }

        [HttpGet("{firstId}/{secondId}")]
        public async Task<ActionResult<double[]>> PostCompare(string firstId, string secondId)
        {
            try
            {
                var res = await db.CompareImages(firstId, secondId, cts.Token);
                return new double[2] { res.distance, res.similarity }; 
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }
    }
}
