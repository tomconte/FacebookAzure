using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;

namespace LikerLib
{
    public class FriendLikeState : TableServiceEntity
    {
        public string Status { get; set; }

        public FriendLikeState(string userId, string status)
            : base("0", userId)
        {
            this.Status = status;
        }

        public FriendLikeState()
            : base()
        {
        }
    }
}
