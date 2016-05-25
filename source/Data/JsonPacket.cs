using UnityEngine;
using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SharpRaven.Data {
    public class JsonPacket {
        /// <summary>
        /// Hexadecimal string representing a uuid4 value.
        /// </summary>
        [JsonProperty(PropertyName = "event_id", NullValueHandling = NullValueHandling.Ignore)]
        public string EventID { get; set; }

        /// <summary>
        /// String value representing the project
        /// </summary>
        [JsonProperty(PropertyName = "project", NullValueHandling = NullValueHandling.Ignore)]
        public string Project { get; set; }

        /// <summary>
        /// Function call which was the primary perpetrator of this event.
        /// A map or list of tags for this event.
        /// </summary>
        [JsonProperty(PropertyName = "culprit", NullValueHandling = NullValueHandling.Ignore)]
        public string Culprit { get; set; }

        /// <summary>
        /// The record severity.
        /// Defaults to error.
        /// </summary>
        [JsonProperty(PropertyName = "level", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public ErrorLevel Level { get; set; }

        /// <summary>
        /// Indicates when the logging record was created (in the Sentry client).
        /// Defaults to DateTime.UtcNow()
        /// </summary>
        [JsonProperty(PropertyName = "timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// The name of the logger which created the record.
        /// If missing, defaults to the string root.
        /// 
        /// Ex: "my.logger.name"
        /// </summary>
        [JsonProperty(PropertyName = "logger", NullValueHandling = NullValueHandling.Ignore)]
        public string Logger { get; set; }

        /// <summary>
        /// A string representing the platform the client is submitting from. 
        /// This will be used by the Sentry interface to customize various components in the interface.
        /// </summary>
        [JsonProperty(PropertyName = "platform", NullValueHandling = NullValueHandling.Ignore)]
        public string Platform { get; set; }

        /// <summary>
        /// User-readable representation of this event
        /// </summary>
        [JsonProperty(PropertyName = "message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        /// <summary>
        /// A map or list of tags for this event.
        /// </summary>
        [JsonProperty(PropertyName = "tags", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Tags;

        /// <summary>
        /// A list of relevant modules (libraries) and their versions.
        /// 
        /// Automated to report all modules currently loaded in project.
        /// </summary>
        [JsonProperty(PropertyName = "modules", NullValueHandling = NullValueHandling.Ignore)]
        public List<Module> Modules { get; set; }

        [JsonProperty(PropertyName="sentry.interfaces.Exception", NullValueHandling=NullValueHandling.Ignore)]
        public SentryException Exception { get; set; }

        [JsonProperty(PropertyName = "sentry.interfaces.Stacktrace", NullValueHandling = NullValueHandling.Ignore)]
        public SentryStacktrace StackTrace { get; set; }

        public JsonPacket(UnityLogEvent log) {
            EventID = GenerateGuid();
            TimeStamp = DateTime.UtcNow;
            Logger = "root";
            Project = "default";
            Platform = "csharp";

            Message = log.Message;
            Level = ErrorLevel.error;

            Exception = new SentryException();
            Exception.Type = log.LogType.ToString();
            Exception.Message = log.Message;
            StackTrace = ParseUnityStackTrace(log.StackTrace);

            // Get assemblies.
            /*Modules = new List<Module>();
            foreach (System.Reflection.Module m in Utilities.SystemUtil.GetModules()) {
                Modules.Add(new Module() {
                    Name = m.ScopeName,
                    Version = m.ModuleVersionId.ToString()
                });
            }*/
        }

        private static string GenerateGuid() {
            //return Guid.NewGuid().ToString().Replace("-", String.Empty);
            return Guid.NewGuid().ToString("N");
        }

        private static SentryStacktrace ParseUnityStackTrace(string unityTrace) {
            /*
            Example Unity Trace String:

            ExceptionGenerator.ThrowNestedB () (at Assets/Script/ExceptionGenerator.cs:26)
            ExceptionGenerator.ThrowNestedA () (at Assets/Script/ExceptionGenerator.cs:22)
            ExceptionGenerator.Update () (at Assets/Script/ExceptionGenerator.cs:13)
            RamjetAnvil.StateMachine.StateMachine`1[StartupScreen].Transition (StateId stateId, System.Object[] args)
            */

            var t = new SentryStacktrace();
            var lines = unityTrace.Split(new[] {"\n"}, StringSplitOptions.RemoveEmptyEntries);

            for (int i = lines.Length-1; i >= 0; i--) {
                var parts = lines[i].Split(new[] {" (at ", ".cs:"}, StringSplitOptions.RemoveEmptyEntries);
                var frame = new ExceptionFrame();
                if (parts.Length == 3) {
                    frame.Function = parts[0];
                    frame.Filename = parts[1] + ".cs";
                    frame.LineNumber = Int32.Parse(parts[2].Replace(")", ""));
                    t.Frames.Add(frame);
                }
                else {
                    frame.Function = lines[i];
                    frame.Filename = "unknown";
                    frame.LineNumber = -1;
                }
            }

            return t;
        }

        private static SentryStacktrace ParseExceptionStackTrace(Exception exception) {
            var t = new SentryStacktrace();

            StackTrace trace = new StackTrace(exception, true);

            for (int i = 0; i < trace.FrameCount; i++) {
                var frame = trace.GetFrame(i);

                int lineNo = frame.GetFileLineNumber();

                if (lineNo == 0) {
                    //The pdb files aren't currently available
                    lineNo = frame.GetILOffset();
                }

                var method = frame.GetMethod();
                var frameData = new ExceptionFrame() {
                    Filename = frame.GetFileName(),
                    //Module = (method.DeclaringType != null) ? method.DeclaringType.FullName : null,
                    Function = method.Name,
                    //Source = method.ToString(),
                    LineNumber = lineNo,
                };

                t.Frames.Add(frameData);
            }

            return t;
        }
    }

    public class Module {
        public string Name;
        public string Version;
    }

    public class JsonPacketSerializer {
        private StringWriter stringWriter;
        private JsonTextWriter writer;

        public JsonPacketSerializer() {
            stringWriter = new StringWriter(new StringBuilder(2048), (IFormatProvider)CultureInfo.InvariantCulture);
            writer = new JsonTextWriter(stringWriter);
        }

        public string Serialize(JsonPacket packet, Formatting formatting) {
            stringWriter.GetStringBuilder().Length = 0;
            writer.Formatting = formatting;

            writer.WriteStartObject();
            {
                Write("event_id", packet.EventID);
                Write("project", packet.Project);
                Write("culprit", packet.Culprit);

                writer.WritePropertyName("level");
                writer.WriteValue(packet.Level);

                writer.WritePropertyName("timestamp");
                writer.WriteValue(packet.TimeStamp.ToString("s", CultureInfo.InvariantCulture));

                Write("logger", packet.Logger);
                Write("platform", packet.Platform);
                Write("message", packet.Message);
                Write("tags", packet.Tags);

                Write(packet.Exception);
                Write(packet.StackTrace);

            }
            writer.WriteEndObject();

            return stringWriter.ToString();
        }

        private void Write(string name, string propertyValue) {
            if (string.IsNullOrEmpty(propertyValue)) {
                return;
            }
            writer.WritePropertyName(name);
            writer.WriteValue(propertyValue);
        }

        private void Write(string name, IDictionary<string, string> d) {
            if (d == null) {
                return;
            }

            writer.WritePropertyName(name);
            writer.WriteStartArray();
            foreach (var entry in d) {
                writer.WriteStartArray();
                writer.WriteValue(entry.Key);
                writer.WriteValue(entry.Value);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }

        private void WriteListIfNotNull(string name, IList<Module> list) {
            if (list == null || list.Count == 0) {
                return;
            }

            writer.WritePropertyName(name);
            writer.WriteStartObject();
            for (int i = 0; i < list.Count; i++) {
                writer.WritePropertyName(list[i].Name);
                writer.WriteValue(list[i].Version);
            }
            writer.WriteEndObject();
        }

        private void Write(SentryException exception) {
            writer.WritePropertyName("sentry.interfaces.Exception");
            writer.WriteStartObject();
            {
                Write("type", exception.Type);
                Write("value", exception.Message);
                Write("module", exception.Module);
            }
            writer.WriteEndObject();
        }

        private void Write(SentryStacktrace trace) {
            if (trace == null || trace.Frames.Count == 0) {
                return;
            }

            writer.WritePropertyName("sentry.interfaces.Stacktrace");
            writer.WriteStartObject();
            {
                writer.WritePropertyName("frames");
                writer.WriteStartArray();
                for (int i = 0; i < trace.Frames.Count; i++) {
                    var frame = trace.Frames[i];
                    writer.WriteStartObject();
                    {
                        Write("filename", frame.Filename);
                        Write("function", frame.Function);
                        Write("lineno", frame.LineNumber.ToString());
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
    }
}


/*
Example of serialized JsonPacket

{
  "event_id": "18e110117d1e474fa0f9f63a2d661ccc",
  "project": "79295",
  "culprit": "CaptureTest in PerformDivideByZero",
  "level": "error",
  "timestamp": "\/Date(1464011555794)\/",
  "logger": "C#",
  "platform": "csharp",
  "message": "Division by zero",
  "server_name": "DESKTOP-AEAMDR3",
  "sentry.interfaces.Exception": {
    "type": "DivideByZeroException",
    "value": "Division by zero",
    "module": "Assembly-CSharp"
  },
  "sentry.interfaces.Stacktrace": {
    "frames": [
      {
        "abs_path": null,
        "filename": "E:\\code\\raven_sharp_unity\\Assets\\Script\\CaptureTest.cs",
        "module": "CaptureTest",
        "function": "testWithStacktrace",
        "vars": null,
        "pre_context": null,
        "context_line": "Void testWithStacktrace()",
        "lineno": 57,
        "colno": 0,
        "in_app": false,
        "post_context": null
      },
      {
        "abs_path": null,
        "filename": "E:\\code\\raven_sharp_unity\\Assets\\Script\\CaptureTest.cs",
        "module": "CaptureTest",
        "function": "PerformDivideByZero",
        "vars": null,
        "pre_context": null,
        "context_line": "Void PerformDivideByZero()",
        "lineno": 70,
        "colno": 0,
        "in_app": false,
        "post_context": null
      }
    ]
  }
}
*/
