using Newtonsoft.Json;
using System;

namespace metabase_exporter;

public interface INewTypeEq<TSelf, TValue> : IEquatable<TSelf>
    where TValue : IEquatable<TValue>
    where TSelf : INewTypeEq<TSelf, TValue>
{
    TSelf New(TValue value);
    TValue Value { get; }
}

public interface INewTypeComp<TSelf, TValue> : INewTypeEq<TSelf, TValue>, IComparable<TSelf>
    where TValue : IEquatable<TValue>, IComparable<TValue>
    where TSelf : INewTypeEq<TSelf, TValue>, INewTypeComp<TSelf, TValue>
{
}

class IdJsonConverter<T> : JsonConverter
    where T: INewTypeEq<T, int>, new()
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(T);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        //var ok = reader.Read();
        //if (ok == false)
        //{
        //    throw new Exception($"Error reading {typeof(T)}");
        //}
        var rawValue = reader.Value;
        if (rawValue == null)
        {
            return null;
        }
        var value = int.Parse(rawValue.ToString());
        return new T().New(value);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var id = (T)value;
        writer.WriteValue(id.Value);
    }
}