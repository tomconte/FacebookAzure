using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;

namespace LikerLib
{
    public class FriendLikesContext : TableServiceContext
    {
        private const string FriendLikesTable = "FriendLikes";

        public FriendLikesContext(string baseAddress, StorageCredentials credentials)
            : base(baseAddress, credentials)
        {
        }

        public IQueryable<FriendLike> FriendLikes
        {
            get
            {
                return this.CreateQuery<FriendLike>(FriendLikesTable);
            }
        }

        public void AddFriendLike(FriendLike l)
        {
            this.AddObject(FriendLikesTable, l);
        }
    }
}
