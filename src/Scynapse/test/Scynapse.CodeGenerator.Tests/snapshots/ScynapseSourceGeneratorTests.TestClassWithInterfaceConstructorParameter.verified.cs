#pragma warning disable CS1591, RS0016, RS0041
[assembly: global::Scynapse.ApplicationPartAttribute("TestProject")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Core.Abstractions")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Serialization")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Core")]
[assembly: global::Scynapse.ApplicationPartAttribute("Scynapse.Runtime")]
[assembly: global::Scynapse.Serialization.Configuration.TypeManifestProviderAttribute(typeof(ScynapseCodeGen.TestProject.Metadata_TestProject))]
namespace ScynapseCodeGen.TestProject
{
    using global::Scynapse.Serialization.Codecs;
    using global::Scynapse.Serialization.GeneratedCodeHelpers;

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_InterfaceCtorParam : global::Scynapse.Serialization.Codecs.IFieldCodec<global::TestProject.InterfaceCtorParam>, global::Scynapse.Serialization.Serializers.IBaseCodec<global::TestProject.InterfaceCtorParam>
    {
        private readonly global::System.Type _codecFieldType = typeof(global::TestProject.InterfaceCtorParam);
        private readonly global::Scynapse.Serialization.Activators.IActivator<global::TestProject.InterfaceCtorParam> _activator;
        private readonly global::System.Type _type0 = typeof(global::TestProject.IMyInterface);
        private readonly global::Scynapse.Serialization.Codecs.IFieldCodec<global::TestProject.IMyInterface> _codec0;
        private static readonly global::System.Func<global::TestProject.InterfaceCtorParam, global::TestProject.IMyInterface> getField0 = (global::System.Func<global::TestProject.InterfaceCtorParam, global::TestProject.IMyInterface>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetGetter(typeof(global::TestProject.InterfaceCtorParam), "_iface");
        private static readonly global::System.Action<global::TestProject.InterfaceCtorParam, global::TestProject.IMyInterface> setField0 = (global::System.Action<global::TestProject.InterfaceCtorParam, global::TestProject.IMyInterface>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetReferenceSetter(typeof(global::TestProject.InterfaceCtorParam), "_iface");
        public Codec_InterfaceCtorParam(global::Scynapse.Serialization.Activators.IActivator<global::TestProject.InterfaceCtorParam> _activator, global::Scynapse.Serialization.Serializers.ICodecProvider codecProvider)
        {
            this._activator = ScynapseGeneratedCodeHelper.UnwrapService(this, _activator);
            _codec0 = ScynapseGeneratedCodeHelper.GetService<global::Scynapse.Serialization.Codecs.IFieldCodec<global::TestProject.IMyInterface>>(this, codecProvider);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, global::TestProject.InterfaceCtorParam instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            _codec0.WriteField(ref writer, 0U, _type0, getField0(instance));
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::TestProject.InterfaceCtorParam instance)
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
                    setField0(instance, _codec0.ReadValue(ref reader, header));
                    reader.ReadFieldHeader(ref header);
                }

                reader.ConsumeEndBaseOrEndObject(ref header);
                break;
            }
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, global::TestProject.InterfaceCtorParam @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null || @value.GetType() == typeof(global::TestProject.InterfaceCtorParam))
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
        public global::TestProject.InterfaceCtorParam ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<global::TestProject.InterfaceCtorParam, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            global::System.Type valueType = field.FieldType;
            if (valueType is null || valueType == _codecFieldType)
            {
                var result = _activator.Create();
                ReferenceCodec.RecordObject(reader.Session, result);
                Deserialize(ref reader, result);
                return result;
            }

            return reader.DeserializeUnexpectedType<TReaderInput, global::TestProject.InterfaceCtorParam>(ref field);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_InterfaceCtorParam : global::Scynapse.Serialization.Cloning.IDeepCopier<global::TestProject.InterfaceCtorParam>, global::Scynapse.Serialization.Cloning.IBaseCopier<global::TestProject.InterfaceCtorParam>
    {
        private readonly global::Scynapse.Serialization.Activators.IActivator<global::TestProject.InterfaceCtorParam> _activator;
        private readonly global::Scynapse.Serialization.Cloning.IDeepCopier<global::TestProject.IMyInterface> _copier0;
        private static readonly global::System.Func<global::TestProject.InterfaceCtorParam, global::TestProject.IMyInterface> getField0 = (global::System.Func<global::TestProject.InterfaceCtorParam, global::TestProject.IMyInterface>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetGetter(typeof(global::TestProject.InterfaceCtorParam), "_iface");
        private static readonly global::System.Action<global::TestProject.InterfaceCtorParam, global::TestProject.IMyInterface> setField0 = (global::System.Action<global::TestProject.InterfaceCtorParam, global::TestProject.IMyInterface>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetReferenceSetter(typeof(global::TestProject.InterfaceCtorParam), "_iface");
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public global::TestProject.InterfaceCtorParam DeepCopy(global::TestProject.InterfaceCtorParam original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (context.TryGetCopy(original, out global::TestProject.InterfaceCtorParam existing))
                return existing;
            if (original.GetType() != typeof(global::TestProject.InterfaceCtorParam))
                return context.DeepCopy(original);
            var result = _activator.Create();
            context.RecordCopy(original, result);
            DeepCopy(original, result, context);
            return result;
        }

        public Copier_InterfaceCtorParam(global::Scynapse.Serialization.Activators.IActivator<global::TestProject.InterfaceCtorParam> _activator, global::Scynapse.Serialization.Serializers.ICodecProvider codecProvider)
        {
            this._activator = ScynapseGeneratedCodeHelper.UnwrapService(this, _activator);
            _copier0 = ScynapseGeneratedCodeHelper.GetService<global::Scynapse.Serialization.Cloning.IDeepCopier<global::TestProject.IMyInterface>>(this, codecProvider);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void DeepCopy(global::TestProject.InterfaceCtorParam input, global::TestProject.InterfaceCtorParam output, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            setField0(output, _copier0.DeepCopy(getField0(input), context));
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Metadata_TestProject : global::Scynapse.Serialization.Configuration.TypeManifestProviderBase
    {
        protected override void ConfigureInner(global::Scynapse.Serialization.Configuration.TypeManifestOptions config)
        {
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_InterfaceCtorParam));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_InterfaceCtorParam));
        }
    }
}
#pragma warning restore CS1591, RS0016, RS0041
