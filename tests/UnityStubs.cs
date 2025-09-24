using System;
using System.Text.Json;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SerializeField : Attribute
    {
    }

    public interface ISerializationCallbackReceiver
    {
        void OnBeforeSerialize();
        void OnAfterDeserialize();
    }

    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        public static string ToJson(object obj)
        {
            return ToJson(obj, false);
        }

        public static string ToJson(object obj, bool prettyPrint)
        {
            if (obj is ISerializationCallbackReceiver receiver)
            {
                receiver.OnBeforeSerialize();
            }

            var options = new JsonSerializerOptions(DefaultOptions)
            {
                WriteIndented = prettyPrint
            };

            var type = obj?.GetType() ?? typeof(object);
            return JsonSerializer.Serialize(obj, type, options);
        }

        public static T FromJson<T>(string json)
        {
            var result = JsonSerializer.Deserialize<T>(json, DefaultOptions);
            if (result is ISerializationCallbackReceiver receiver)
            {
                receiver.OnAfterDeserialize();
            }

            return result;
        }
    }
}

namespace UnityEngine.Serialization
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class FormerlySerializedAsAttribute : Attribute
    {
        public FormerlySerializedAsAttribute(string oldName)
        {
        }
    }
}
