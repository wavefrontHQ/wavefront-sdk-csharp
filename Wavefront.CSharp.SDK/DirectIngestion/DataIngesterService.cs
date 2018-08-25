using System;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace Wavefront.CSharp.SDK.DirectIngestion
{
    /// <summary>
    /// Data ingester service that reports entities to Wavefront.
    /// </summary>
    public class DataIngesterService : IDataIngesterAPI
    {
        private static readonly int ConnectTimeout = 30000;
        private static readonly int ReadTimeout = 10000;

        private readonly string token;
        private readonly Uri uri;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="T:Wavefront.CSharp.SDK.DirectIngestion.DataIngesterService"/> class.
        /// </summary>
        /// <param name="server">A Wavefront server URL of the form "https://clusterName.wavefront.com".</param>
        /// <param name="token">A valid API token with direct ingestion permissions.</param>
        public DataIngesterService(string server, string token)
        {
            this.token = token;
            uri = new Uri(server);
        }

        /// <see cref="IDataIngesterAPI.Report(string, Stream)"/>
        public int Report(string format, Stream inputStream)
        {
            HttpWebResponse response = null;
            try
            {
                var uriBuilder = new UriBuilder(uri.Scheme, uri.Host, uri.Port, "report", "?f=" + format);
                var requestUri = uriBuilder.Uri;

                var request = WebRequest.CreateHttp(requestUri);
                request.Method = "POST";
                request.ContentType = "application/octet-stream";
                request.Headers.Add(HttpRequestHeader.ContentEncoding, "gzip");
                request.Headers.Add(HttpRequestHeader.Authorization, "Bearer " + token);
                request.Timeout = ConnectTimeout;
                request.ReadWriteTimeout = ReadTimeout;

                using (var outputStream = request.GetRequestStream())
                {
                    using (var gZipStream = new GZipStream(outputStream, CompressionMode.Compress, true))
                    {
                        byte[] buffer = new byte[4096];
                        while (inputStream.Read(buffer, 0, buffer.Length) > 0)
                        {
                            gZipStream.Write(buffer, 0, buffer.Length);
                        }
                        gZipStream.Flush();
                    }
                    response = (HttpWebResponse)request.GetResponse();
                }
            }
            catch (IOException)
            {
            }

            int statusCode = (int)HttpStatusCode.BadRequest;
            if (response != null)
            {
                statusCode = (int)response.StatusCode;
                response.Close();
            }
            return statusCode;
        }
    }
}
