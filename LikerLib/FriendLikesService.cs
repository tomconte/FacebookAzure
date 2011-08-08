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
        private const string FRIENDS_LIKES_STATE_TABLE = "FriendsLikesState";
        public String AccessToken { get; set; }
        public String UserId { get; set; }
        public Dictionary<string, FriendLike> FriendLikes { get; set; }
        private CloudStorageAccount account;
        private CloudTableClient tableClient;

        public FriendLikesService(string id, String token)
        {
            this.AccessToken = token;
            this.UserId = id;
            account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            tableClient = new CloudTableClient(account.TableEndpoint.ToString(), account.Credentials);
            tableClient.CreateTableIfNotExist(FRIENDS_LIKES_TABLE); // MOVE THIS OUT
            tableClient.CreateTableIfNotExist(FRIENDS_LIKES_STATE_TABLE); // MOVE THIS OUT
        }

        // Lightweight (no access token) constructor, used by the JSON controller only
        public FriendLikesService(string id)
        {
            this.UserId = id;
            account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            tableClient = new CloudTableClient(account.TableEndpoint.ToString(), account.Credentials);
            tableClient.CreateTableIfNotExist(FRIENDS_LIKES_TABLE); // MOVE THIS OUT
            tableClient.CreateTableIfNotExist(FRIENDS_LIKES_STATE_TABLE); // MOVE THIS OUT
        }

        public string GetState()
        {
            FriendLikeState friendLikeState;

            var q = tableClient.GetDataServiceContext().
                CreateQuery<FriendLikeState>(FRIENDS_LIKES_STATE_TABLE).
                AsTableServiceQuery<FriendLikeState>();

            if (q.FirstOrDefault() == null)
            {
                friendLikeState = new FriendLikeState(UserId, "unknown");
            }
            else
            {
                var a = q.Where(l => l.RowKey == UserId && l.PartitionKey == "0").ToArray();
                if (a.Count() == 0)
                    friendLikeState = new FriendLikeState(UserId, "unknown");
                else
                    friendLikeState = a[0];
            }
            
            return friendLikeState.Status;
        }

        public void SetState(string state)
        {
            FriendLikeState friendLikeState;

            var context = tableClient.GetDataServiceContext();

            var q = context.
                CreateQuery<FriendLikeState>(FRIENDS_LIKES_STATE_TABLE).
                AsTableServiceQuery<FriendLikeState>();

            if (q.FirstOrDefault() == null)
            {
                friendLikeState = new FriendLikeState(UserId, state);
                context.AddObject(FRIENDS_LIKES_STATE_TABLE, friendLikeState);
            }
            else
            {
                var a = q.Where(l => l.RowKey == UserId && l.PartitionKey == "0").ToArray();
                if (a.Count() == 0)
                {
                    friendLikeState = new FriendLikeState(UserId, state);
                    context.AddObject(FRIENDS_LIKES_STATE_TABLE, friendLikeState);
                }
                else
                {
                    // BUG: does not update the state
                    a[0].Status = state;
                    friendLikeState = a[0]; // Keep reference to object for context save?
                }
            }

            // Save to Table Storage

            context.SaveChanges();
        }

        public void GetFriendsLikes()
        {
            // Find all friend likes & store in Dictionary

            // If we have previously cached the friend likes for this user, use them
            // TODO: we probably need a way to refresh the data from time to time
            var state = GetState();
            //tableClient.GetDataServiceContext().CreateQuery<FriendLikeState>(FRIENDS_LIKES_STATE_TABLE).Where(l => l.RowKey == UserId);

            if (state == "cached")
            {
                var query = tableClient.
                    GetDataServiceContext().
                    CreateQuery<FriendLike>(FRIENDS_LIKES_TABLE);

                var likes = (from l in query where l.PartitionKey == UserId select l).AsTableServiceQuery();

                FriendLikes = likes.ToDictionary(l => l.RowKey);
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

            var context = tableClient.GetDataServiceContext();

            foreach (var k in FriendLikes.Keys)
            {
                context.AddObject(FRIENDS_LIKES_TABLE, FriendLikes[k]);
            }

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

        public Dictionary<string, int> GetCategories()
        {
            var categories = new Dictionary<string, int>();

            foreach (var k in FriendLikes)
            {
                var v = k.Value.Category;
                if (categories.ContainsKey(v))
                {
                    categories[v]++;
                }
                else
                {
                    categories[v] = 1;
                }
            }

            return categories;
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
