using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using LikerLib;

namespace FacebookAzure.Controllers
{
    public class LikesController : Controller
    {
        //
        // GET: /Likes/

        [HttpGet]
        [OutputCache(NoStore=true, Duration=0, VaryByParam="*")]
        public ActionResult IsCached(string id)
        {
            var service = new FriendLikesService(id);
            return Json(service.GetState().Equals("cached"), JsonRequestBehavior.AllowGet);
        }
    }
}
