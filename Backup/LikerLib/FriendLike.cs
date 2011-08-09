using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;

namespace LikerLib
{
    public class FriendLike : TableServiceEntity
    {   
        /* RowKey == PageId */
        /* PartitionKey = UserId */

        public FriendLike(string userId, string pageId, String name, String category, String picture)
            : base(userId, pageId)
        {
            this.Nb = 1;
            this.Id = pageId;
            this.Name = name;
            this.Category = category;
            this.Picture = picture;
        }

        public FriendLike()
            : base()
        {
        }

        public int Nb { get; set; }
        public string Id { get; set; }
        public String Name { get; set; }
        public String Category { get; set; }
        public String Picture { get; set; }
    }
}
