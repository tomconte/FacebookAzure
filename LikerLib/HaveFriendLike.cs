using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;

namespace LikerLib
{
    public class HaveFriendLike : TableServiceEntity
    {
        public bool IsCached { get; set; }

        public HaveFriendLike(string userId)
            : base("0", userId)
        {
            this.IsCached = true;
        }

        public HaveFriendLike()
            : base()
        {
        }
    }
}
