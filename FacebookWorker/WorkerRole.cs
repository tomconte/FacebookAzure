using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using LikerLib;

namespace FacebookWorker
{
    public class WorkerRole : RoleEntryPoint
    {
        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.WriteLine("FacebookWorker entry point called", "Information");

            var account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));

            var queueClient = account.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(FriendLikesService.FRIEND_LIKES_QUEUE);

            while (true)
            {
                Thread.Sleep(10000);
                Trace.WriteLine("Working", "Information");

                var msg = queue.GetMessage(TimeSpan.FromMinutes(5)); // Let's give us five minutes to process
                if (msg != null)
                {
                    var p = msg.AsString.Split('%');
                    Trace.WriteLine("Processing " + p[0], "Information");
                    // TODO: start new thread
                    var service = new FriendLikesService(p[0], p[1]);
                    // TODO: we probably need a way to refresh the data from time to time:
                    // - test existence of the response BLOB
                    // - refresh if blob older than n days/hours
                    if (service.isCached())
                    {
                        Trace.WriteLine("Already in cache, skipping " + p[0], "Information");
                    }
                    else
                    {
                        var watch = new Stopwatch();
                        watch.Start();
                        service.GetFriendsLikes();
                        service.SaveFriendLikes();
                        watch.Stop();
                        Trace.WriteLine("Done with " + p[0] + " time: " + watch.Elapsed, "Information");
                    }
                    queue.DeleteMessage(msg);
                }

            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            var account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));

            var queueClient = account.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(FriendLikesService.FRIEND_LIKES_QUEUE);
            queue.CreateIfNotExist();

            return base.OnStart();
        }
    }
}
