using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SharpRaven.Utilities {
    public static class PacketBuilder
    {
        private static StringBuilder _builder;
        private static FieldInfo _stringField;

        public static string CreateAuthenticationHeader(Dsn dsn) {
            if (_builder == null) {
                _builder = new StringBuilder(2048);
            }

            //_builder.Remove(0, _builder.Length);
            _builder.Length = 0;

            _builder.Append("Sentry sentry_version=2.0");

            _builder.Append(", sentry_timestamp=");
            _builder.Append((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds);

            _builder.Append(", sentry_key=");
            _builder.Append(dsn.PublicKey);

            _builder.Append(", sentry_secret=");
            _builder.Append(dsn.PrivateKey);

            _builder.Append(", sentry_client=SharpRaven/1.0");

            return _builder.ToString();
            //return GarbageFreeString(_builder);
                
//            string header = String.Empty;
//            header += "Sentry sentry_version=2.0";
//            header += ", sentry_timestamp=" + (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
//            header += ", sentry_key=" + dsn.PublicKey;
//            header += ", sentry_secret=" + dsn.PrivateKey;
//            header += ", sentry_client=SharpRaven/1.0";
//
//            return header;
        }

        public static string GarbageFreeString(StringBuilder sb) {
            if (_stringField == null) {
                _stringField = sb.GetType().GetField(
                    "_str",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
            }

            string str = (string)_stringField.GetValue(sb);

            //Optional: clear out the string
            //for (int i = 0; i &lt; sb.Capacity; i++) {
            //	sb.Append(" ");
            //}
            return str;
        }
    }
}
