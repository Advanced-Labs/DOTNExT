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
    public sealed class Codec_GenericWithCtor<T> : global::Scynapse.Serialization.Codecs.IFieldCodec<global::TestProject.GenericWithCtor<T>>, global::Scynapse.Serialization.Serializers.IBaseCodec<global::TestProject.GenericWithCtor<T>>
    {
        private readonly global::System.Type _codecFieldType = typeof(global::TestProject.GenericWithCtor<T>);
        private readonly global::Scynapse.Serialization.Activators.IActivator<global::TestProject.GenericWithCtor<T>> _activator;
        private readonly global::System.Type _type0 = typeof(T);
        private readonly global::Scynapse.Serialization.Codecs.IFieldCodec<T> _codec0;
        private static readonly global::System.Func<global::TestProject.GenericWithCtor<T>, int> getField0 = (global::System.Func<global::TestProject.GenericWithCtor<T>, int>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetGetter(typeof(global::TestProject.GenericWithCtor<T>), "_id");
        private static readonly global::System.Action<global::TestProject.GenericWithCtor<T>, int> setField0 = (global::System.Action<global::TestProject.GenericWithCtor<T>, int>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetReferenceSetter(typeof(global::TestProject.GenericWithCtor<T>), "_id");
        private static readonly global::System.Func<global::TestProject.GenericWithCtor<T>, T> getField1 = (global::System.Func<global::TestProject.GenericWithCtor<T>, T>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetGetter(typeof(global::TestProject.GenericWithCtor<T>), "_value");
        private static readonly global::System.Action<global::TestProject.GenericWithCtor<T>, T> setField1 = (global::System.Action<global::TestProject.GenericWithCtor<T>, T>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetReferenceSetter(typeof(global::TestProject.GenericWithCtor<T>), "_value");
        public Codec_GenericWithCtor(global::Scynapse.Serialization.Activators.IActivator<global::TestProject.GenericWithCtor<T>> _activator, global::Scynapse.Serialization.Serializers.ICodecProvider codecProvider)
        {
            this._activator = ScynapseGeneratedCodeHelper.UnwrapService(this, _activator);
            _codec0 = ScynapseGeneratedCodeHelper.GetService<global::Scynapse.Serialization.Codecs.IFieldCodec<T>>(this, codecProvider);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, global::TestProject.GenericWithCtor<T> instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            _codec0.WriteField(ref writer, 0U, _type0, getField1(instance));
            global::Scynapse.Serialization.Codecs.Int32Codec.WriteField(ref writer, 1U, getField0(instance));
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::TestProject.GenericWithCtor<T> instance)
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
                    setField1(instance, _codec0.ReadValue(ref reader, header));
                    reader.ReadFieldHeader(ref header);
                    if (header.IsEndBaseOrEndObject)
                        break;
                    id += header.FieldIdDelta;
                }

                if (id == 1U)
                {
                    setField0(instance, global::Scynapse.Serialization.Codecs.Int32Codec.ReadValue(ref reader, header));
                    reader.ReadFieldHeader(ref header);
                }

                reader.ConsumeEndBaseOrEndObject(ref header);
                break;
            }
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, global::TestProject.GenericWithCtor<T> @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null || @value.GetType() == typeof(global::TestProject.GenericWithCtor<T>))
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
        public global::TestProject.GenericWithCtor<T> ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<global::TestProject.GenericWithCtor<T>, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            global::System.Type valueType = field.FieldType;
            if (valueType is null || valueType == _codecFieldType)
            {
                var result = _activator.Create();
                ReferenceCodec.RecordObject(reader.Session, result);
                Deserialize(ref reader, result);
                return result;
            }

            return reader.DeserializeUnexpectedType<TReaderInput, global::TestProject.GenericWithCtor<T>>(ref field);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_GenericWithCtor<T> : global::Scynapse.Serialization.Cloning.IDeepCopier<global::TestProject.GenericWithCtor<T>>, global::Scynapse.Serialization.Cloning.IBaseCopier<global::TestProject.GenericWithCtor<T>>
    {
        private readonly global::Scynapse.Serialization.Activators.IActivator<global::TestProject.GenericWithCtor<T>> _activator;
        private readonly global::Scynapse.Serialization.Cloning.IDeepCopier<T> _copier0;
        private static readonly global::System.Func<global::TestProject.GenericWithCtor<T>, int> getField0 = (global::System.Func<global::TestProject.GenericWithCtor<T>, int>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetGetter(typeof(global::TestProject.GenericWithCtor<T>), "_id");
        private static readonly global::System.Action<global::TestProject.GenericWithCtor<T>, int> setField0 = (global::System.Action<global::TestProject.GenericWithCtor<T>, int>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetReferenceSetter(typeof(global::TestProject.GenericWithCtor<T>), "_id");
        private static readonly global::System.Func<global::TestProject.GenericWithCtor<T>, T> getField1 = (global::System.Func<global::TestProject.GenericWithCtor<T>, T>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetGetter(typeof(global::TestProject.GenericWithCtor<T>), "_value");
        private static readonly global::System.Action<global::TestProject.GenericWithCtor<T>, T> setField1 = (global::System.Action<global::TestProject.GenericWithCtor<T>, T>)global::Scynapse.Serialization.Utilities.FieldAccessor.GetReferenceSetter(typeof(global::TestProject.GenericWithCtor<T>), "_value");
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public global::TestProject.GenericWithCtor<T> DeepCopy(global::TestProject.GenericWithCtor<T> original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (context.TryGetCopy(original, out global::TestProject.GenericWithCtor<T> existing))
                return existing;
            if (original.GetType() != typeof(global::TestProject.GenericWithCtor<T>))
                return context.DeepCopy(original);
            var result = _activator.Create();
            context.RecordCopy(original, result);
            DeepCopy(original, result, context);
            return result;
        }

        public Copier_GenericWithCtor(global::Scynapse.Serialization.Activators.IActivator<global::TestProject.GenericWithCtor<T>> _activator, global::Scynapse.Serialization.Serializers.ICodecProvider codecProvider)
        {
            this._activator = ScynapseGeneratedCodeHelper.UnwrapService(this, _activator);
            _copier0 = ScynapseGeneratedCodeHelper.GetService<global::Scynapse.Serialization.Cloning.IDeepCopier<T>>(this, codecProvider);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void DeepCopy(global::TestProject.GenericWithCtor<T> input, global::TestProject.GenericWithCtor<T> output, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            setField1(output, _copier0.DeepCopy(getField1(input), context));
            setField0(output, getField0(input));
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_UsesGenericWithCtor : global::Scynapse.Serialization.Codecs.IFieldCodec<global::TestProject.UsesGenericWithCtor>, global::Scynapse.Serialization.Serializers.IBaseCodec<global::TestProject.UsesGenericWithCtor>
    {
        private readonly global::System.Type _codecFieldType = typeof(global::TestProject.UsesGenericWithCtor);
        private readonly global::System.Type _type0 = typeof(global::TestProject.GenericWithCtor<string>);
        private readonly ScynapseCodeGen.TestProject.Codec_GenericWithCtor<string> _codec0;
        public Codec_UsesGenericWithCtor(global::Scynapse.Serialization.Serializers.ICodecProvider codecProvider)
        {
            _codec0 = ScynapseGeneratedCodeHelper.GetService<ScynapseCodeGen.TestProject.Codec_GenericWithCtor<string>>(this, codecProvider);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, global::TestProject.UsesGenericWithCtor instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            _codec0.WriteField(ref writer, 0U, _type0, instance.StringGen);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::TestProject.UsesGenericWithCtor instance)
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
                    instance.StringGen = _codec0.ReadValue(ref reader, header);
                    reader.ReadFieldHeader(ref header);
                }

                reader.ConsumeEndBaseOrEndObject(ref header);
                break;
            }
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, global::TestProject.UsesGenericWithCtor @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null || @value.GetType() == typeof(global::TestProject.UsesGenericWithCtor))
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
        public global::TestProject.UsesGenericWithCtor ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<global::TestProject.UsesGenericWithCtor, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            global::System.Type valueType = field.FieldType;
            if (valueType is null || valueType == _codecFieldType)
            {
                var result = new global::TestProject.UsesGenericWithCtor();
                ReferenceCodec.RecordObject(reader.Session, result);
                Deserialize(ref reader, result);
                return result;
            }

            return reader.DeserializeUnexpectedType<TReaderInput, global::TestProject.UsesGenericWithCtor>(ref field);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_UsesGenericWithCtor : global::Scynapse.Serialization.Cloning.IDeepCopier<global::TestProject.UsesGenericWithCtor>, global::Scynapse.Serialization.Cloning.IBaseCopier<global::TestProject.UsesGenericWithCtor>
    {
        private readonly ScynapseCodeGen.TestProject.Copier_GenericWithCtor<string> _copier0;
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public global::TestProject.UsesGenericWithCtor DeepCopy(global::TestProject.UsesGenericWithCtor original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (context.TryGetCopy(original, out global::TestProject.UsesGenericWithCtor existing))
                return existing;
            if (original.GetType() != typeof(global::TestProject.UsesGenericWithCtor))
                return context.DeepCopy(original);
            var result = new global::TestProject.UsesGenericWithCtor();
            context.RecordCopy(original, result);
            DeepCopy(original, result, context);
            return result;
        }

        public Copier_UsesGenericWithCtor(global::Scynapse.Serialization.Serializers.ICodecProvider codecProvider)
        {
            _copier0 = ScynapseGeneratedCodeHelper.GetService<ScynapseCodeGen.TestProject.Copier_GenericWithCtor<string>>(this, codecProvider);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void DeepCopy(global::TestProject.UsesGenericWithCtor input, global::TestProject.UsesGenericWithCtor output, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            output.StringGen = _copier0.DeepCopy(input.StringGen, context);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Activator_UsesGenericWithCtor : global::Scynapse.Serialization.Activators.IActivator<global::TestProject.UsesGenericWithCtor>
    {
        public global::TestProject.UsesGenericWithCtor Create() => new global::TestProject.UsesGenericWithCtor();
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Metadata_TestProject : global::Scynapse.Serialization.Configuration.TypeManifestProviderBase
    {
        protected override void ConfigureInner(global::Scynapse.Serialization.Configuration.TypeManifestOptions config)
        {
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_GenericWithCtor<>));
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_UsesGenericWithCtor));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_GenericWithCtor<>));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_UsesGenericWithCtor));
            config.Activators.Add(typeof(ScynapseCodeGen.TestProject.Activator_UsesGenericWithCtor));
        }
    }
}
#pragma warning restore CS1591, RS0016, RS0041
