using UnityEngine;
using System.Collections;
using System;
using SharpRaven.Data;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using SharpRaven.Utilities;
using SharpRaven.Logging;

namespace SharpRaven
{
    /* Todo: 
     * - Handle log messages from non-Unity threads.
     * - Depending on Unity's WWW for sending means doing the sends on Unity thread, which is not ideal. Of course,
     * old Mono makes it very hard to use WebRequest and such so we live with it for now.
     */
    public class RavenClient
    {
        /// <summary>
        /// The DSN currently being used to log exceptions.
        /// </summary>
        public Dsn Dsn { get; set; }

        /// <summary>
        /// Interface for providing a 'log scrubber' that removes 
        /// sensitive information from exceptions sent to sentry.
        /// </summary>
        public IScrubber LogScrubber { get; set; }

        /// <summary>
        /// Enable Gzip Compression?
        /// Defaults to true.
        /// </summary>
        public bool Compression { get; set; }

        /// <summary>
        /// Logger. Default is "root"
        /// </summary>
        public string Logger { get; set; }

        private readonly Dictionary<string, string> _postHeader;
        private readonly UTF8Encoding _encoding;
        private readonly JsonPacketSerializer _packetSerializer;
        private readonly WWWPool _wwwPool;
        private readonly MonoBehaviour _routineRunner;

        public RavenClient(string dsn, MonoBehaviour routineRunner) : this(new Dsn(dsn), routineRunner) { }

        public RavenClient(Dsn dsn, MonoBehaviour routineRunner)
        {
            Dsn = dsn;
            _routineRunner = routineRunner;
            Compression = true;
            Logger = "root";

            _postHeader = new Dictionary<string, string>();
            _encoding = new UTF8Encoding();
            _packetSerializer = new JsonPacketSerializer();
            _wwwPool = new WWWPool(16);
        }
        
        public JsonPacket CreatePacket(UnityLogEvent log) {
            JsonPacket packet = new JsonPacket(log);
            packet.Project = Dsn.ProjectID;
            packet.Level = GetErrorLevel(log.LogType);

            return packet;
        }

        private static ErrorLevel GetErrorLevel(LogType logType) {
            switch (logType) {
                case LogType.Error:
                    return ErrorLevel.error;
                case LogType.Assert:
                    return ErrorLevel.error;
                case LogType.Warning:
                    return ErrorLevel.warning;
                case LogType.Log:
                    return ErrorLevel.info;
                case LogType.Exception:
                    return ErrorLevel.error;
                default:
                    throw new ArgumentOutOfRangeException("logType", logType, null);
            }
        }

        public void Send(JsonPacket packet) {
            if (_wwwPool.Count == 0) {
                //Debug.LogWarning("Skipping GetSentry exception upload, too many sends...\n" + packet.Exception);
                return;
            }

            //Debug.Log("Sending to GetSentry: " + packet.Exception.Message);

            packet.Logger = Logger;

            string authHeader = PacketBuilder.CreateAuthenticationHeader(Dsn);

            _postHeader.Clear();
            _postHeader.Add("ContentType", "application/json");
            _postHeader.Add("User-Agent", "RavenSharp/1.0");
            _postHeader.Add("X-Sentry-Auth", authHeader);
            string[] headers = FlattenedHeadersFrom(_postHeader);

            string data = _packetSerializer.Serialize(packet, Formatting.None);

            _routineRunner.StartCoroutine(SendAsync(Dsn, data, headers));
        }

        private IEnumerator SendAsync(Dsn dsn, string data, string[] headers) {
            var www = _wwwPool.Take();
            www.InitWWW(dsn.SentryURI, _encoding.GetBytes(data), headers);

            while (!www.isDone) {
                yield return null;
            }

//            if (!string.IsNullOrEmpty(www.error)) {
//                Debug.LogError("Failed to send error to Sentry: " + www.error);
//            }
//            else {
//                Debug.Log("Sentry response: " + www.text);
//            }

            _wwwPool.Return(www);
        }

        private static string[] _flattenedHeaders;

        private static string[] FlattenedHeadersFrom(Dictionary<string, string> headers) {
            if (headers == null) {
                return null;
            }

            if (_flattenedHeaders == null) {
                _flattenedHeaders = new string[headers.Count*2];
            }

            int i = 0;
            using (Dictionary<string, string>.Enumerator enumerator = headers.GetEnumerator()) {
                while (enumerator.MoveNext()) {
                    var current = enumerator.Current;
                    _flattenedHeaders[i] = current.Key;
                    _flattenedHeaders[i + 1] = current.Value;
                }
            }
            return _flattenedHeaders;
        }
    }

    public class WWWPool {
        private readonly Queue<WWW> _pool;

        public WWWPool(int size) {
            _pool = new Queue<WWW>(size);

            for (int i = 0; i < size; i++) {
                var item = new WWW("");
                _pool.Enqueue(item);
            }
        }

        public WWW Take() {
            if (_pool.Count == 0) {
                throw new Exception("Pool is empty");
            }

            return _pool.Dequeue();
        }

        public void Return(WWW item) {
            _pool.Enqueue(item);
        }

        public int Count {
            get { return _pool.Count; }
        }
    }

    public class UnityLogEvent {
        public string Message;
        public string StackTrace;
        public LogType LogType;
    }
}
