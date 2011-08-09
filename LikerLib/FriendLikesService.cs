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

namespace LikerLib
{
    public class FriendLikesService
    {
        private const string FRIEND_LIKES_TABLE = "friendlikes";
        private const string FRIEND_LIKES_BLOB_CONTAINER = "friendlikes";
        public String AccessToken { get; set; }
        public String UserId { get; set; }
        public Dictionary<string, FriendLike> FriendLikes { get; set; }
        private CloudStorageAccount account;
        private CloudTableClient tableClient;
        private CloudBlobClient blobClient;
        private CloudBlobContainer blobContainer;

        public FriendLikesService(string id, String token)
        {
            this.AccessToken = token;
            this.UserId = id;
            account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            // Create the Table client
            tableClient = new CloudTableClient(account.TableEndpoint.ToString(), account.Credentials);

            // Create the Blob Container
            blobClient = new CloudBlobClient(account.BlobEndpoint.ToString(), account.Credentials); 
            blobContainer = blobClient.GetContainerReference(FRIEND_LIKES_BLOB_CONTAINER);
            blobContainer.CreateIfNotExist(); // TODO: MOVE THIS OUT?
            blobContainer.SetPermissions(new BlobContainerPermissions() { PublicAccess = BlobContainerPublicAccessType.Blob });
        }

        // Lightweight (no access token) constructor, used by the JSON controller only
        public FriendLikesService(string id)
        {
            this.UserId = id;
            account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            tableClient = new CloudTableClient(account.TableEndpoint.ToString(), account.Credentials);

            // Create the Blob Container
            blobClient = new CloudBlobClient(account.BlobEndpoint.ToString(), account.Credentials);
            blobContainer = blobClient.GetContainerReference(FRIEND_LIKES_BLOB_CONTAINER);
            blobContainer.CreateIfNotExist(); // TODO: MOVE THIS OUT?
            blobContainer.SetPermissions(new BlobContainerPermissions() { PublicAccess = BlobContainerPublicAccessType.Blob });
        }

        public void GetFriendsLikes()
        {
            // Find all friend likes & store in Dictionary

            // Create the table if necessary
            tableClient.CreateTableIfNotExist(FRIEND_LIKES_TABLE); // MOVE THIS OUT?

            // If we have previously cached the friend likes for this user, use them
            // TODO: we probably need a way to refresh the data from time to time
            // TODO: test existence of the response BLOB
            // TODO: refresh if blob older than n days/hours?

            if (isCached())
            {
                var query = tableClient.
                    GetDataServiceContext().
                    CreateQuery<FriendLike>(FRIEND_LIKES_TABLE);

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
                            // Clean up the category field
                            var category = l.category.ToLower().Replace('/', '-').Replace(' ', '-');
                            FriendLikes[l.id] = new FriendLike(UserId, l.id, l.name, category, l.picture);
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
                context.AddObject(FRIEND_LIKES_TABLE, FriendLikes[k]);
            }

            // TODO: for refreshes, need to handle updating the entities (or replacing)

            context.SaveChangesWithRetries();

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
            var queue = queueClient.GetQueueReference("likesqueue");
            queue.CreateIfNotExist(); // MOVE THIS OUT
            var msg = new CloudQueueMessage(UserId + "%" + AccessToken);
            queue.AddMessage(msg);
        }
    }
}
