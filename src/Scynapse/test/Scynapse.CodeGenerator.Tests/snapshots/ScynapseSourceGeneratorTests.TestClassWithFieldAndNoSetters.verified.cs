#pragma warning disable CS1591, RS0016, RS0041
[assembly: global::Scynapse.ApplicationPartAttribute("TestProject")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Core.Abstractions")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Serialization")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Core")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Runtime")]
[assembly: global::Scynapse.Serialization.Configuration.TypeManifestProviderAttribute(typeof(ScynapseCodeGen.TestProject.Metadata_TestProject))]
namespace ScynapseCodeGen
{
    using global::Scynapse.Serialization.Codecs;
    using global::Scynapse.Serialization.GeneratedCodeHelpers;

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_ExternalType : global::Scynapse.Serialization.Codecs.IFieldCodec<global::ExternalType>, global::Scynapse.Serialization.Serializers.IBaseCodec<global::ExternalType>
    {
        private readonly global::System.Type _codecFieldType = typeof(global::ExternalType);
        private readonly global::Scynapse.Serialization.Activators.IActivator<global::ExternalType> _activator;
        private static readonly global::System.Func<global::ExternalType, int> getField0 = (global::System.Func<global::ExternalType, int>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetGetter(typeof(global::ExternalType), "Amount");
        private static readonly global::System.Action<global::ExternalType, int> setField0 = (global::System.Action<global::ExternalType, int>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetReferenceSetter(typeof(global::ExternalType), "Amount");
        public Codec_ExternalType(global::Scynapse.Serialization.Activators.IActivator<global::ExternalType> _activator)
        {
            this._activator = ScynapseGeneratedCodeHelper.UnwrapService(this, _activator);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, global::ExternalType instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            global::Scynapse.Serialization.Codecs.Int32Codec.WriteField(ref writer, 0U, getField0(instance));
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::ExternalType instance)
        {
            uint id = 0U;
            global::Scynapse.Serialization.WireProtocol.Field header = default;
            while (true)
            {
                reader.ReadFieldHeader(ref header);
                if (header.IsEndBaseOrEndObject)
                    break;
                id += header.FieldIdDelta;
                if (id == 0U)
                {
                    setField0(instance, global::Scynapse.Serialization.Codecs.Int32Codec.ReadValue(ref reader, header));
                    reader.ReadFieldHeader(ref header);
                }

                reader.ConsumeEndBaseOrEndObject(ref header);
                break;
            }
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, global::ExternalType @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null || @value.GetType() == typeof(global::ExternalType))
            {
                if (ReferenceCodec.TryWriteReferenceField(ref writer, fieldIdDelta, expectedType, @value))
                    return;
                writer.WriteStartObject(fieldIdDelta, expectedType, _codecFieldType);
                Serialize(ref writer, @value);
                writer.WriteEndObject();
            }
            else
                writer.SerializeUnexpectedType(fieldIdDelta, expectedType, @value);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public global::ExternalType ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<global::ExternalType, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            global::System.Type valueType = field.FieldType;
            if (valueType is null || valueType == _codecFieldType)
            {
                var result = _activator.Create();
                ReferenceCodec.RecordObject(reader.Session, result);
                Deserialize(ref reader, result);
                return result;
            }

            return reader.DeserializeUnexpectedType<TReaderInput, global::ExternalType>(ref field);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_ExternalType : global::Scynapse.Serialization.Cloning.IDeepCopier<global::ExternalType>, global::Scynapse.Serialization.Cloning.IBaseCopier<global::ExternalType>
    {
        private readonly global::Scynapse.Serialization.Activators.IActivator<global::ExternalType> _activator;
        private static readonly global::System.Func<global::ExternalType, int> getField0 = (global::System.Func<global::ExternalType, int>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetGetter(typeof(global::ExternalType), "Amount");
        private static readonly global::System.Action<global::ExternalType, int> setField0 = (global::System.Action<global::ExternalType, int>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetReferenceSetter(typeof(global::ExternalType), "Amount");
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public global::ExternalType DeepCopy(global::ExternalType original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (context.TryGetCopy(original, out global::ExternalType existing))
                return existing;
            if (original.GetType() != typeof(global::ExternalType))
                return context.DeepCopy(original);
            var result = _activator.Create();
            context.RecordCopy(original, result);
            DeepCopy(original, result, context);
            return result;
        }

        public Copier_ExternalType(global::Scynapse.Serialization.Activators.IActivator<global::ExternalType> _activator)
        {
            this._activator = ScynapseGeneratedCodeHelper.UnwrapService(this, _activator);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void DeepCopy(global::ExternalType input, global::ExternalType output, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            setField0(output, getField0(input));
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Activator_ExternalType : global::Scynapse.Serialization.Activators.IActivator<global::ExternalType>
    {
        private readonly int _arg0;
        public Activator_ExternalType(int arg0)
        {
            _arg0 = ScynapseGeneratedCodeHelper.UnwrapService(this, arg0);
        }

        public global::ExternalType Create() => new global::ExternalType(_arg0);
    }
}

namespace ScynapseCodeGen.TestProject
{
    using global::Scynapse.Serialization.Codecs;
    using global::Scynapse.Serialization.GeneratedCodeHelpers;

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Metadata_TestProject : global::Scynapse.Serialization.Configuration.TypeManifestProviderBase
    {
        protected override void ConfigureInner(global::Scynapse.Serialization.Configuration.TypeManifestOptions config)
        {
            config.Serializers.Add(typeof(ScynapseCodeGen.Codec_ExternalType));
            config.Copiers.Add(typeof(ScynapseCodeGen.Copier_ExternalType));
            config.Activators.Add(typeof(ScynapseCodeGen.Activator_ExternalType));
        }
    }
}
#pragma warning restore CS1591, RS0016, RS0041
