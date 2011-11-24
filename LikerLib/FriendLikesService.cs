using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Facebook;
using Facebook.Web;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using Newtonsoft.Json;
using System.Data.Services.Client;

namespace LikerLib
{
    public class FriendLikesService
    {
        public static string FRIEND_LIKES_TABLE = "friendlikes";
        public static string FRIEND_LIKES_BLOB_CONTAINER = "friendlikes";
        public static string FRIEND_LIKES_QUEUE = "likesqueue";
        public String AccessToken { get; set; }
        public String UserId { get; set; }
        public Dictionary<string, FriendLike> FriendLikes { get; set; }
        private CloudStorageAccount account;
        private CloudTableClient tableClient;
        private CloudBlobClient blobClient;
        private CloudBlobContainer blobContainer;

        public FriendLikesService(string id)
        {
            this.UserId = id;

            account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));

            // Table Client
            tableClient = new CloudTableClient(account.TableEndpoint.ToString(), account.Credentials);
            tableClient.CreateTableIfNotExist(FRIEND_LIKES_TABLE);

            // Blob Client
            blobClient = new CloudBlobClient(account.BlobEndpoint.ToString(), account.Credentials);
            blobContainer = blobClient.GetContainerReference(FRIEND_LIKES_BLOB_CONTAINER);
            blobContainer.CreateIfNotExist();
            blobContainer.SetPermissions(new BlobContainerPermissions() { PublicAccess = BlobContainerPublicAccessType.Blob });
        }

        public FriendLikesService(string id, String token)
            : this(id)
        {
            this.AccessToken = token;
        }

        public void GetFriendsLikes()
        {
            // Find all friend likes & store in Dictionary
            FriendLikes = new Dictionary<string, FriendLike>();

            // Let's use the Facebook Graph API to gather all the info
            var fb = new FacebookClient(AccessToken);
            // TODO: if the user logged out of Facebook in the meantime, he access token could be invalid and we will receive an exception!
            dynamic friends = fb.Get("me/friends");
            foreach (var f in friends.data)
            {
                dynamic likes = fb.Get(f.id + "/likes"); // TODO: exception handling
                foreach (var l in likes.data)
                {
                    if (FriendLikes.ContainsKey(l.id))
                    {
                        ++FriendLikes[l.id].Nb;
                    }
                    else
                    {
                        // Clean up the category field
                        var category = l.category.ToLower().Replace('/', '-').Replace(' ', '-');
                        FriendLikes[l.id] = new FriendLike(UserId, l.id, l.name, category, l.picture);
                    }
                }
            }
        }

        public void SaveFriendLikes()
        {
            // Persist friend likes to Table Storage

            var context = new TableServiceContext(tableClient.BaseUri.ToString(), tableClient.Credentials);

            // Using the Upsert pattern in the August 2011 API / November 2011 SDK (1.6)
            var n = 0;
            foreach (var k in FriendLikes.Keys)
            {
                context.AttachTo(FRIEND_LIKES_TABLE, FriendLikes[k]);
                context.UpdateObject(FriendLikes[k]);
                if (n++ % 100 == 0)
                {
                    context.SaveChangesWithRetries(SaveChangesOptions.Batch|SaveChangesOptions.ReplaceOnUpdate);
                }
            }

            // Create the pre-calculated JSON Blob

            var blob = blobContainer.GetBlobReference(UserId);
            var likes = GetOrderedFriendLikes();
            var json = JsonConvert.SerializeObject(likes);
            blob.UploadText("dataCallback(" + json + ")");
        }

        public bool isCached()
        {
            var blob = blobContainer.GetBlobReference(UserId);

            try
            {
                blob.FetchAttributes();
            }
            catch (StorageClientException ex)
            {
                return false;
            }

            return true;
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
                if (!String.IsNullOrEmpty(v))
                {
                    if (categories.ContainsKey(v))
                    {
                        categories[v]++;
                    }
                    else
                    {
                        categories[v] = 1;
                    }
                }
            }

            return categories;
        }

        public void QueueLike()
        {
            var queueClient = account.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(FRIEND_LIKES_QUEUE);
            queue.CreateIfNotExist();
            var msg = new CloudQueueMessage(UserId + "%" + AccessToken);
            queue.AddMessage(msg);
        }
    }
}
