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

            ViewBag.id = me.id;
            ViewBag.name = me.name;
            ViewBag.firstname = me.first_name;
            ViewBag.hometown = me.hometown.name;

            FriendLikesService service = new FriendLikesService(me.id, fb.AccessToken);

            var state = service.GetState();

            if (state == "cached")
            {
                // We can deal with this directly
                service.GetFriendsLikes(); // FAST!
                ViewBag.friendLikes = service.GetOrderedFriendLikes();
            }
            else if (state == "inprogress")
            {
                return View("PleaseWait");
            }
            else
            {
                // Need to send this to the Worker Role
                //service.GetFriendsLikes(); // SLOW!
                service.QueueLike();
                return View("PleaseWait");
            }

            return View();
        }
    }
}
