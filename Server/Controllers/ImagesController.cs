using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Contracts;
using System;
using Server.Database;

namespace Server.Controllers
{
    [Route("/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        private IImagesDB db;
        private CancellationTokenSource cts;
        public ImagesController(IImagesDB db)
        {
            this.db = db;
            this.cts = new CancellationTokenSource();
        }

        [HttpPost]
        public async Task<ActionResult<string[]>> PostImages(Photo[] images)
        {
            try
            {
                return await db.AddImages(images, cts.Token);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult<Photo[]>> GetImages()
        {
            try
            {
                return (await db.GetImages(cts.Token)).ToArray();
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpDelete]
        public async Task<ActionResult<string>> DeleteImages()
        {
            try
            {
                await db.DeleteImages(cts.Token);
            }
            catch (Exception e)
            {
                return StatusCode(500, e.Message);
            }
            return StatusCode(204);
        }
    }
}
