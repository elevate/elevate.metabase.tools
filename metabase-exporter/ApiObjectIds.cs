using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace metabase_exporter
{
    [JsonConverter(typeof(IdJsonConverter<CardId>))]
    public struct CardId : INewTypeComp<CardId, int>
    {
        public int Value { get; }

        public CardId(int value)
        {
            Value = value;
        }

        public int CompareTo(CardId other) => Value.CompareTo(other.Value);
        public bool Equals(CardId other) => Value.Equals(other.Value);
        public CardId New(int value) => new CardId(value);

        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator >(CardId a, CardId b) => a.CompareTo(b) > 0;
        public static bool operator <(CardId a, CardId b) => a.CompareTo(b) < 0;
        public static bool operator <=(CardId a, CardId b) => a.CompareTo(b) <= 0;
        public static bool operator >=(CardId a, CardId b) => a.CompareTo(b) >= 0;
        public static bool operator ==(CardId a, CardId b) => a.CompareTo(b) == 0;
        public static bool operator !=(CardId a, CardId b) => !(a == b);
    }

    [JsonConverter(typeof(IdJsonConverter<CollectionId>))]
    public struct CollectionId: INewTypeComp<CollectionId, int>
    {
        public int Value { get; }

        public CollectionId(int value)
        {
            Value = value;
        }

        public int CompareTo(CollectionId other) => Value.CompareTo(other.Value);
        public bool Equals(CollectionId other) => Value.Equals(other.Value);
        public CollectionId New(int value) => new CollectionId(value);

        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator >(CollectionId a, CollectionId b) => a.CompareTo(b) > 0;
        public static bool operator <(CollectionId a, CollectionId b) => a.CompareTo(b) < 0;
        public static bool operator <=(CollectionId a, CollectionId b) => a.CompareTo(b) <= 0;
        public static bool operator >=(CollectionId a, CollectionId b) => a.CompareTo(b) >= 0;
        public static bool operator ==(CollectionId a, CollectionId b) => a.CompareTo(b) == 0;
        public static bool operator !=(CollectionId a, CollectionId b) => !(a == b);

    }

    [JsonConverter(typeof(IdJsonConverter<DashboardId>))]
    public struct DashboardId: INewTypeComp<DashboardId, int>
    {
        public int Value { get; }

        public DashboardId(int value)
        {
            Value = value;
        }

        public int CompareTo(DashboardId other) => Value.CompareTo(other.Value);
        public bool Equals(DashboardId other) => Value.Equals(other.Value);
        public DashboardId New(int value) => new DashboardId(value);

        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator >(DashboardId a, DashboardId b) => a.CompareTo(b) > 0;
        public static bool operator <(DashboardId a, DashboardId b) => a.CompareTo(b) < 0;
        public static bool operator <=(DashboardId a, DashboardId b) => a.CompareTo(b) <= 0;
        public static bool operator >=(DashboardId a, DashboardId b) => a.CompareTo(b) >= 0;
        public static bool operator ==(DashboardId a, DashboardId b) => a.CompareTo(b) == 0;
        public static bool operator !=(DashboardId a, DashboardId b) => !(a == b);

    }

    [JsonConverter(typeof(IdJsonConverter<DashboardCardId>))]
    public struct DashboardCardId: INewTypeComp<DashboardCardId, int>
    {
        public int Value { get; }

        public DashboardCardId(int value)
        {
            Value = value;
        }

        public int CompareTo(DashboardCardId other) => Value.CompareTo(other.Value);
        public bool Equals(DashboardCardId other) => Value.Equals(other.Value);
        public DashboardCardId New(int value) => new DashboardCardId(value);

        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator >(DashboardCardId a, DashboardCardId b) => a.CompareTo(b) > 0;
        public static bool operator <(DashboardCardId a, DashboardCardId b) => a.CompareTo(b) < 0;
        public static bool operator <=(DashboardCardId a, DashboardCardId b) => a.CompareTo(b) <= 0;
        public static bool operator >=(DashboardCardId a, DashboardCardId b) => a.CompareTo(b) >= 0;
        public static bool operator ==(DashboardCardId a, DashboardCardId b) => a.CompareTo(b) == 0;
        public static bool operator !=(DashboardCardId a, DashboardCardId b) => !(a == b);

    }
}
