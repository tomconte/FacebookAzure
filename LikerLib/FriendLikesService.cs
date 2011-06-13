using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure;
using Facebook.Web;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace LikerLib
{
    public class FriendLikesService
    {
        public String AccessToken { get; set; }
        public String UserId { get; set; }

        public FriendLikesService(string id, String token)
        {
            this.AccessToken = token;
            this.UserId = id;
        }

        public void GetFriendsLikes()
        {
            var account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            CloudTableClient tableClient = new CloudTableClient(account.TableEndpoint.ToString(), account.Credentials);
            tableClient.CreateTableIfNotExist("FriendsLikes");
            TableServiceContext ctx = tableClient.GetDataServiceContext();

            var friendLikes = new Dictionary<string, FriendLike>();

            var fb = new FacebookWebClient(AccessToken);
            dynamic friends = fb.Get("me/friends");
            foreach (var f in friends.data)
            {
                dynamic likes = fb.Get(f.id + "/likes");
                foreach (var l in likes.data)
                {
                    if (friendLikes.ContainsKey(l.id))
                    {
                        ++friendLikes[l.id].Nb;
                    }
                    else
                    {
                        friendLikes[l.id] = new FriendLike(UserId, l.id, l.name, l.category, l.picture);
                    }
                }
            }

            foreach (var k in friendLikes.Keys)
            {
                ctx.AddObject("FriendsLikes", friendLikes[k]);
            }
            ctx.SaveChanges();
        }
    }
}
