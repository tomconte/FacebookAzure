using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Facebook;
using Facebook.Web;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace LikerLib
{
    public class FriendLikesService
    {
        private const string FRIENDS_LIKES_TABLE = "FriendsLikes";
        private const string HAVE_FRIENDS_LIKES_TABLE = "HaveFriendsLikes";
        public String AccessToken { get; set; }
        public String UserId { get; set; }
        public Dictionary<string, FriendLike> FriendLikes { get; set; }
        private CloudStorageAccount account;
        private CloudTableClient tableClient;
        private TableServiceContext context;

        public FriendLikesService(string id, String token)
        {
            this.AccessToken = token;
            this.UserId = id;
            account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            tableClient = new CloudTableClient(account.TableEndpoint.ToString(), account.Credentials);
            tableClient.CreateTableIfNotExist(FRIENDS_LIKES_TABLE); // MOVE THIS OUT
            tableClient.CreateTableIfNotExist(HAVE_FRIENDS_LIKES_TABLE); // MOVE THIS OUT
            context = tableClient.GetDataServiceContext();
        }

        // Lightweight constructor, used by the JSON controller only
        public FriendLikesService(string id)
        {
            this.UserId = id;
            account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            tableClient = new CloudTableClient(account.TableEndpoint.ToString(), account.Credentials);
            tableClient.CreateTableIfNotExist(FRIENDS_LIKES_TABLE);
            tableClient.CreateTableIfNotExist(HAVE_FRIENDS_LIKES_TABLE); // MOVE THIS OUT
            context = tableClient.GetDataServiceContext();
        }

        public bool HaveCachedFriendsLikes()
        {
            var query = tableClient.GetDataServiceContext().CreateQuery<HaveFriendLike>(HAVE_FRIENDS_LIKES_TABLE).Where(l => l.RowKey == UserId);
            if (query.ToList().Count > 0)
                return true;
            else
                return false;
        }

        public void GetFriendsLikes()
        {
            // Find all friend likes & store in Dictionary

            // If we have previously cached the friend likes for this user, use them
            // TODO: we probably need a way to refresh the data from time to time
            var isCachedQuery = tableClient.GetDataServiceContext().CreateQuery<HaveFriendLike>(HAVE_FRIENDS_LIKES_TABLE).Where(l => l.RowKey == UserId);

            if (isCachedQuery.ToList().Count > 0)
            {
                var query = tableClient.GetDataServiceContext().CreateQuery<FriendLike>(FRIENDS_LIKES_TABLE).Where(l => l.PartitionKey == UserId);
                FriendLikes = query.ToDictionary(l => l.RowKey);
            }
            else
            {
                FriendLikes = new Dictionary<string, FriendLike>();

                // Let's use the Facebook Graph API to gather all the info
                var fb = new FacebookClient(AccessToken);
                dynamic friends = fb.Get("me/friends");
                foreach (var f in friends.data)
                {
                    dynamic likes = fb.Get(f.id + "/likes");
                    foreach (var l in likes.data)
                    {
                        if (FriendLikes.ContainsKey(l.id))
                        {
                            ++FriendLikes[l.id].Nb;
                        }
                        else
                        {
                            FriendLikes[l.id] = new FriendLike(UserId, l.id, l.name, l.category, l.picture);
                        }
                    }
                }
            }
        }

        public void SaveFriendLikes()
        {
            // Persist friend likes to Table Storage

            foreach (var k in FriendLikes.Keys)
            {
                context.AddObject(FRIENDS_LIKES_TABLE, FriendLikes[k]);
            }
            context.SaveChanges();

            // Now that we have cached the user's friends' likes, raise the flag

            context.AddObject(HAVE_FRIENDS_LIKES_TABLE, new HaveFriendLike(UserId));
            context.SaveChanges();
        }

        public List<FriendLike> GetOrderedFriendLikes()
        {
            var orderedLikes = new List<FriendLike>();

            // Sort friend likes by descending number of likes
            // Strip everything that does not have at least 2 likes

            foreach (var l in FriendLikes.Where(k => k.Value.Nb > 1).OrderByDescending(k => k.Value.Nb))
            {
                orderedLikes.Add(l.Value);
            }

            return orderedLikes;
        }

        public void QueueLike()
        {
            var queueClient = account.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference("likesqueue");
            queue.CreateIfNotExist(); // MOVE THIS OUT
            var msg = new CloudQueueMessage(UserId + "%" + AccessToken);
            queue.AddMessage(msg);
        }
    }
}
