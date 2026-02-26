using Scynapse.Serialization;

namespace UnitTests.GrainInterfaces
{
    /// <summary>
    /// A type with an <see cref="IOnDeserialized"/> hook, to test that it is correctly called by the internal serializers.
    /// </summary>
    [Serializable]
    [Scynapse.GenerateSerializer]
    [Scynapse.SerializationCallbacks(typeof(Scynapse.Runtime.OnDeserializedCallbacks))]
    public class TypeWithOnDeserializedHook : IOnDeserialized
    {
        [NonSerialized]
        public DeserializationContext Context;

        [Scynapse.Id(0)]
        public int Int { get; set; }

        void IOnDeserialized.OnDeserialized(DeserializationContext context)
        {
            this.Context = context;
        }
    }

    [Serializable]
    [Scynapse.GenerateSerializer]
    public class BaseClassWithAutoProp
    {
        [Scynapse.Id(0)]
        public int AutoProp { get; set; }
    }

    /// <summary>
    /// Code generation test to ensure that an overridden autoprop with a type which differs from
    /// the base autoprop is not used during serializer generation
    /// </summary>
    [Serializable]
    [Scynapse.GenerateSerializer]
    public class SubClassOverridingAutoProp : BaseClassWithAutoProp
    {
        public new string AutoProp { get => base.AutoProp.ToString(); set => base.AutoProp = int.Parse(value); }
    }
}
