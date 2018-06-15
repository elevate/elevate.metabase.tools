using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace metabase_exporter
{

    /// <summary>
    /// Functions to build Newtonsoft <see cref="JObject"/>s, properties, etc.
    /// Constructors in Newtonsoft are too lax, allowing many runtime exceptions.
    /// </summary>
    public static class JObj
    {
        /// <summary>
        /// Builds a <see cref="JObject"/>
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        public static JObject Obj(IEnumerable<JProperty> properties)
        {
            return new JObject(properties.ToArray());
        }

        /// <summary>
        /// Builds a <see cref="JProperty"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static JProperty Prop(string key, JObject value)
        {
            return new JProperty(key, value);
        }

        /// <summary>
        /// Builds a <see cref="JProperty"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static JProperty Prop(string key, IEnumerable<string> value)
        {
            return new JProperty(key, value);
        }

        public static JProperty Prop(string key, IEnumerable<JObject> value)
        {
            return new JProperty(key, value);
        }

        /// <summary>
        /// Builds a <see cref="JProperty"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static JProperty Prop(string key, JValue value)
        {
            return new JProperty(key, value);
        }

        public static JProperty Prop(string key, string value)
        {
            return Prop(key, new JValue(value));
        }

        public static JProperty Prop(string key, int value)
        {
            return Prop(key, new JValue(value));
        }

        public static JProperty Prop(string key, long value)
        {
            return Prop(key, new JValue(value));
        }

        public static JProperty Prop(string key, bool value)
        {
            return Prop(key, new JValue(value));
        }
    }
}
