using System.Collections.Generic;

using Newtonsoft.Json;

namespace SharpRaven.Data {
    public class SentryStacktrace {
        [JsonProperty(PropertyName = "frames")]
        public List<ExceptionFrame> Frames;

        public SentryStacktrace() {
            Frames = new List<ExceptionFrame>();
        }
    }
}
