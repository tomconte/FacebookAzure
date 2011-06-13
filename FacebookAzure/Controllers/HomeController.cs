using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Facebook;
using Facebook.Web.Mvc;
using Facebook.Web;
using LikerLib;

namespace FacebookAzure.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        [CanvasAuthorize(Permissions="user_about_me,user_likes,friends_likes")]
        public ActionResult Index()
        {
            var fb = new FacebookWebClient();
            dynamic me = fb.Get("me");
            dynamic likes = fb.Get("me/likes");

            ViewBag.name = me.name;
            ViewBag.firstname = me.first_name;
            ViewBag.hometown = me.hometown.name;

            ViewBag.likes = likes.data;

            FriendLikesService service = new FriendLikesService(me.id, fb.AccessToken);
            service.GetFriendsLikes();

            return View();
        }
    }
}
