using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;
using Microsoft.WindowsAzure;
using System.Net;
using System.Data.Services.Client;

namespace LikerLib
{
    public class TableServiceContextV2 : TableServiceContext
    {
        private const string StorageVersionHeader = "x-ms-version";
        private const string August2011Version = "2011-08-18";

        public TableServiceContextV2(string baseAddress, StorageCredentials credentials) :
            base(baseAddress, credentials)
        {
            this.SendingRequest += SendingRequestWithNewVersion;
        }

        private void SendingRequestWithNewVersion(object sender, SendingRequestEventArgs e)
        {
            HttpWebRequest request = e.Request as HttpWebRequest;

            // Apply the new storage version as a header value
            request.Headers[StorageVersionHeader] = August2011Version;
        }
    }
}
