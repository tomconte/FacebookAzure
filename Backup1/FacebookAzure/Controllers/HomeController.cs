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

            FriendLikesService service = new FriendLikesService(me.id, fb.AccessToken);

            if (service.isCached())
            {
                //// The data is cached, we can retrieve it and use it server-side...
                //// The ViewBag is used by the default Index view
                //service.GetFriendsLikes();
                //ViewBag.friendLikes = service.GetOrderedFriendLikes();
                //var cats = service.GetCategories().OrderByDescending(k => k.Value).ToList();
                //var max = cats.Max(k => k.Value);
                //var min = cats.Min(k => k.Value);
                //ViewBag.categories = cats;
                //ViewBag.max = max;
                //ViewBag.min = min;

                // Return the IndexJS view to use the cached data on the client
                return View("IndexJS");
            }
            else
            {
                // Need to send this to the Worker Role
                service.QueueLike();
                return View("PleaseWait");
            }

            return View();
        }
    }
}
