using Newtonsoft.Json;

namespace SharpRaven.Data {
    public class ExceptionFrame {
        [JsonProperty(PropertyName = "filename")]
        public string Filename;

        [JsonProperty(PropertyName = "function")]
        public string Function;

        [JsonProperty(PropertyName = "lineno")]
        public int LineNumber;
    }
}