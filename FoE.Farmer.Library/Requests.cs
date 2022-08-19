using FoE.Farmer.Library.Events;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Timers;

namespace FoE.Farmer.Library
{
    public class Requests
    {
        public static event PayloadSendRequestHandler PayloadSendRequest;
        public delegate void PayloadSendRequestHandler(Requests m, Events.PayloadRequestEventArgs e);

        public static string UserName = "nms10266@uzrip.com";
        public static string Password = "nms10266@uzrip.com";
        public static string WorldName = null;

        public const string BaseAddress = "{0}.forgeofempires.com";
        public const string AddressTemplate = "https://{0}/game/json?h={1}";
        public static readonly HttpClient Client = new HttpClient();
        public const string Secret = "G3pXENczVOKHVX1rgFMz4M8P7cu992SQ3gIStXl2PcVh7MtxInYWmZ8SZ2OwAjjKhQAFq/pxMrqgcAc3R+iDTg==";
        //public static string Timestamp = "1518000888"; // flash

        private const string GameVersion = "1.235";

        public static Dictionary<string, string> TemplateRequestHeader { get; set; } = new Dictionary<string, string>();
        //nms10266@uzrip.com

        private static string Server { get; set; } = "cz5";
        public static string UserKey { get; set; } = "----";
        // Cookies
        public static string SID { get; set; } = "9P5Tup3ajM04nLdxKgkVT0GO6j4VTHq5p95Yq5Ub";
        public static string _GA { get; set; } = "";
        public static string _GID { get; set; } = "";
        public static string StartupMicrotime { get; set; } = "";
        public static string MetricsUvId { get; set; } = "";
        public static string IgLastSite { get; set; } = "";

        public string RequestsAddress => string.Format(AddressTemplate, Domain, UserKey);
        public static string Domain { get; set; } = "cz.forgeofempires.com";

        private readonly Queue<(Payload, Action<JObject>)> payloads = new Queue<(Payload, Action<JObject>)>();
        private Timer requestSendTimer = new Timer(400);

        public Requests()
        {
            var random = new Random();
            requestSendTimer.Interval = random.Next(400, 600);
            requestSendTimer.Elapsed += (e, s) => SendPayload();
            //requestSendTimer.Start();
        }

        public void AddPayload(Payload payload, Action<JObject> callback = null)
        {
            payloads.Enqueue((payload, callback));
            if (payloads.Count == 1) requestSendTimer.Start();
        }

        private void SendPayload()
        {
            if (payloads.Count == 0) return;
            if (string.IsNullOrWhiteSpace(UserKey) || string.IsNullOrWhiteSpace(SID)) return;
            if (payloads.Count == 1) requestSendTimer.Stop();

            var item = payloads.Dequeue();
            if (item.Item1.TaskSource.Task.IsCanceled) return;


            PayloadSendRequest?.Invoke(this, new PayloadRequestEventArgs { Payload = item.Item1 });
        }

        public static string BuildSignature(string data)
        {
            return MD5(UserKey + Secret + data).Substring(0, 10);
        }

        private static string MD5(string data)
        {
            // byte array representation of that string
            var encodedData = new UTF8Encoding().GetBytes(data);

            // need MD5 to calculate the hash
            var hash = ((HashAlgorithm)CryptoConfig.CreateFromName("MD5")).ComputeHash(encodedData);

            // string representation (similar to UNIX format)
            return BitConverter.ToString(hash)
                // without dashes
                .Replace("-", string.Empty)
                // make lowercase
                .ToLower();
        }
    }
}