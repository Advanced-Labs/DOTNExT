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
    [global::Scynapse.CompoundTypeAliasAttribute("inv", typeof(global::Scynapse.Runtime.GrainReference), typeof(global::TestProject.IMyGrainWithGuidKey), "8F0FEC0E")]
    public sealed class Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E : global::Scynapse.Runtime.TaskRequest<global::System.Guid>
    {
        global::TestProject.IMyGrainWithGuidKey _target;
        private static readonly global::System.Reflection.MethodInfo MethodBackingField = ScynapseGeneratedCodeHelper.GetMethodInfoOrDefault(typeof(global::TestProject.IMyGrainWithGuidKey), "GetGuidValue", null, null);
        public override string GetMethodName() => "GetGuidValue";
        public override string GetInterfaceName() => "TestProject.IMyGrainWithGuidKey";
        public override string GetActivityName() => "IMyGrainWithGuidKey/GetGuidValue";
        public override global::System.Type GetInterfaceType() => typeof(global::TestProject.IMyGrainWithGuidKey);
        public override global::System.Reflection.MethodInfo GetMethod() => MethodBackingField;
        public override void SetTarget(global::Scynapse.Serialization.Invocation.ITargetHolder holder) => _target = holder.GetTarget<global::TestProject.IMyGrainWithGuidKey>();
        public override object GetTarget() => _target;
        public override void Dispose()
        {
            _target = default;
        }

        protected override global::System.Threading.Tasks.Task<global::System.Guid> InvokeInner() => _target.GetGuidValue();
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Proxy_IMyGrainWithGuidKey : global::Scynapse.Runtime.GrainReference, global::TestProject.IMyGrainWithGuidKey
    {
        public Proxy_IMyGrainWithGuidKey(global::Scynapse.Runtime.GrainReferenceShared arg0, global::Scynapse.Runtime.IdSpan arg1) : base(arg0, arg1)
        {
        }

        global::System.Threading.Tasks.Task<global::System.Guid> global::TestProject.IMyGrainWithGuidKey.GetGuidValue()
        {
            var request = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E();
            return base.InvokeAsync<global::System.Guid>(request).AsTask();
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    [global::Scynapse.CompoundTypeAliasAttribute("inv", typeof(global::Scynapse.Runtime.GrainReference), typeof(global::TestProject.IMyGrainWithStringKey), "43570316")]
    public sealed class Invokable_IMyGrainWithStringKey_GrainReference_43570316 : global::Scynapse.Runtime.TaskRequest<string>
    {
        global::TestProject.IMyGrainWithStringKey _target;
        private static readonly global::System.Reflection.MethodInfo MethodBackingField = ScynapseGeneratedCodeHelper.GetMethodInfoOrDefault(typeof(global::TestProject.IMyGrainWithStringKey), "GetStringKey", null, null);
        public override string GetMethodName() => "GetStringKey";
        public override string GetInterfaceName() => "TestProject.IMyGrainWithStringKey";
        public override string GetActivityName() => "IMyGrainWithStringKey/GetStringKey";
        public override global::System.Type GetInterfaceType() => typeof(global::TestProject.IMyGrainWithStringKey);
        public override global::System.Reflection.MethodInfo GetMethod() => MethodBackingField;
        public override void SetTarget(global::Scynapse.Serialization.Invocation.ITargetHolder holder) => _target = holder.GetTarget<global::TestProject.IMyGrainWithStringKey>();
        public override object GetTarget() => _target;
        public override void Dispose()
        {
            _target = default;
        }

        protected override global::System.Threading.Tasks.Task<string> InvokeInner() => _target.GetStringKey();
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Proxy_IMyGrainWithStringKey : global::Scynapse.Runtime.GrainReference, global::TestProject.IMyGrainWithStringKey
    {
        public Proxy_IMyGrainWithStringKey(global::Scynapse.Runtime.GrainReferenceShared arg0, global::Scynapse.Runtime.IdSpan arg1) : base(arg0, arg1)
        {
        }

        global::System.Threading.Tasks.Task<string> global::TestProject.IMyGrainWithStringKey.GetStringKey()
        {
            var request = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316();
            return base.InvokeAsync<string>(request).AsTask();
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    [global::Scynapse.CompoundTypeAliasAttribute("inv", typeof(global::Scynapse.Runtime.GrainReference), typeof(global::TestProject.IMyGrainWithGuidCompoundKey), "A9FEF7AF")]
    public sealed class Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF : global::Scynapse.Runtime.TaskRequest<global::System.Tuple<global::System.Guid, string>>
    {
        global::TestProject.IMyGrainWithGuidCompoundKey _target;
        private static readonly global::System.Reflection.MethodInfo MethodBackingField = ScynapseGeneratedCodeHelper.GetMethodInfoOrDefault(typeof(global::TestProject.IMyGrainWithGuidCompoundKey), "GetGuidAndStringKey", null, null);
        public override string GetMethodName() => "GetGuidAndStringKey";
        public override string GetInterfaceName() => "TestProject.IMyGrainWithGuidCompoundKey";
        public override string GetActivityName() => "IMyGrainWithGuidCompoundKey/GetGuidAndStringKey";
        public override global::System.Type GetInterfaceType() => typeof(global::TestProject.IMyGrainWithGuidCompoundKey);
        public override global::System.Reflection.MethodInfo GetMethod() => MethodBackingField;
        public override void SetTarget(global::Scynapse.Serialization.Invocation.ITargetHolder holder) => _target = holder.GetTarget<global::TestProject.IMyGrainWithGuidCompoundKey>();
        public override object GetTarget() => _target;
        public override void Dispose()
        {
            _target = default;
        }

        protected override global::System.Threading.Tasks.Task<global::System.Tuple<global::System.Guid, string>> InvokeInner() => _target.GetGuidAndStringKey();
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Proxy_IMyGrainWithGuidCompoundKey : global::Scynapse.Runtime.GrainReference, global::TestProject.IMyGrainWithGuidCompoundKey
    {
        public Proxy_IMyGrainWithGuidCompoundKey(global::Scynapse.Runtime.GrainReferenceShared arg0, global::Scynapse.Runtime.IdSpan arg1) : base(arg0, arg1)
        {
        }

        global::System.Threading.Tasks.Task<global::System.Tuple<global::System.Guid, string>> global::TestProject.IMyGrainWithGuidCompoundKey.GetGuidAndStringKey()
        {
            var request = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF();
            return base.InvokeAsync<global::System.Tuple<global::System.Guid, string>>(request).AsTask();
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    [global::Scynapse.CompoundTypeAliasAttribute("inv", typeof(global::Scynapse.Runtime.GrainReference), typeof(global::TestProject.IMyGrainWithIntegerCompoundKey), "9814021A")]
    public sealed class Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A : global::Scynapse.Runtime.TaskRequest<global::System.Tuple<long, string>>
    {
        global::TestProject.IMyGrainWithIntegerCompoundKey _target;
        private static readonly global::System.Reflection.MethodInfo MethodBackingField = ScynapseGeneratedCodeHelper.GetMethodInfoOrDefault(typeof(global::TestProject.IMyGrainWithIntegerCompoundKey), "GetIntegerAndStringKey", null, null);
        public override string GetMethodName() => "GetIntegerAndStringKey";
        public override string GetInterfaceName() => "TestProject.IMyGrainWithIntegerCompoundKey";
        public override string GetActivityName() => "IMyGrainWithIntegerCompoundKey/GetIntegerAndStringKey";
        public override global::System.Type GetInterfaceType() => typeof(global::TestProject.IMyGrainWithIntegerCompoundKey);
        public override global::System.Reflection.MethodInfo GetMethod() => MethodBackingField;
        public override void SetTarget(global::Scynapse.Serialization.Invocation.ITargetHolder holder) => _target = holder.GetTarget<global::TestProject.IMyGrainWithIntegerCompoundKey>();
        public override object GetTarget() => _target;
        public override void Dispose()
        {
            _target = default;
        }

        protected override global::System.Threading.Tasks.Task<global::System.Tuple<long, string>> InvokeInner() => _target.GetIntegerAndStringKey();
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Proxy_IMyGrainWithIntegerCompoundKey : global::Scynapse.Runtime.GrainReference, global::TestProject.IMyGrainWithIntegerCompoundKey
    {
        public Proxy_IMyGrainWithIntegerCompoundKey(global::Scynapse.Runtime.GrainReferenceShared arg0, global::Scynapse.Runtime.IdSpan arg1) : base(arg0, arg1)
        {
        }

        global::System.Threading.Tasks.Task<global::System.Tuple<long, string>> global::TestProject.IMyGrainWithIntegerCompoundKey.GetIntegerAndStringKey()
        {
            var request = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A();
            return base.InvokeAsync<global::System.Tuple<long, string>>(request).AsTask();
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E : global::Scynapse.Serialization.Codecs.IFieldCodec<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E>
    {
        private readonly global::System.Type _codecFieldType = typeof(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E);
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E instance)
        {
            reader.ConsumeEndBaseOrEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null)
            {
                ReferenceCodec.WriteNullReference(ref writer, fieldIdDelta);
                return;
            }

            ReferenceCodec.MarkValueField(writer.Session);
            writer.WriteStartObject(fieldIdDelta, expectedType, _codecFieldType);
            Serialize(ref writer, @value);
            writer.WriteEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            var result = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E();
            ReferenceCodec.MarkValueField(reader.Session);
            Deserialize(ref reader, result);
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E : global::Scynapse.Serialization.Cloning.IDeepCopier<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E>
    {
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E DeepCopy(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (original is null)
                return null;
            var result = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E();
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_GrainWithGuidKey : global::Scynapse.Serialization.Codecs.IFieldCodec<global::TestProject.GrainWithGuidKey>, global::Scynapse.Serialization.Serializers.IBaseCodec<global::TestProject.GrainWithGuidKey>
    {
        private readonly global::System.Type _codecFieldType = typeof(global::TestProject.GrainWithGuidKey);
        private readonly global::Scynapse.Serialization.Serializers.IBaseCodec<global::Scynapse.Grain> _baseTypeSerializer;
        public Codec_GrainWithGuidKey(global::Scynapse.Serialization.Serializers.IBaseCodec<global::Scynapse.Grain> _baseTypeSerializer)
        {
            this._baseTypeSerializer = ScynapseGeneratedCodeHelper.UnwrapService(this, _baseTypeSerializer);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, global::TestProject.GrainWithGuidKey instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            _baseTypeSerializer.Serialize(ref writer, instance);
            writer.WriteEndBase();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::TestProject.GrainWithGuidKey instance)
        {
            _baseTypeSerializer.Deserialize(ref reader, instance);
            reader.ConsumeEndBaseOrEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, global::TestProject.GrainWithGuidKey @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null || @value.GetType() == typeof(global::TestProject.GrainWithGuidKey))
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
        public global::TestProject.GrainWithGuidKey ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<global::TestProject.GrainWithGuidKey, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            global::System.Type valueType = field.FieldType;
            if (valueType is null || valueType == _codecFieldType)
            {
                var result = new global::TestProject.GrainWithGuidKey();
                ReferenceCodec.RecordObject(reader.Session, result);
                Deserialize(ref reader, result);
                return result;
            }

            return reader.DeserializeUnexpectedType<TReaderInput, global::TestProject.GrainWithGuidKey>(ref field);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_GrainWithGuidKey : global::Scynapse.Serialization.Cloning.IDeepCopier<global::TestProject.GrainWithGuidKey>, global::Scynapse.Serialization.Cloning.IBaseCopier<global::TestProject.GrainWithGuidKey>
    {
        private readonly global::Scynapse.Serialization.Cloning.IBaseCopier<global::Scynapse.Grain> _baseTypeCopier;
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public global::TestProject.GrainWithGuidKey DeepCopy(global::TestProject.GrainWithGuidKey original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (context.TryGetCopy(original, out global::TestProject.GrainWithGuidKey existing))
                return existing;
            if (original.GetType() != typeof(global::TestProject.GrainWithGuidKey))
                return context.DeepCopy(original);
            var result = new global::TestProject.GrainWithGuidKey();
            context.RecordCopy(original, result);
            DeepCopy(original, result, context);
            return result;
        }

        public Copier_GrainWithGuidKey(global::Scynapse.Serialization.Cloning.IBaseCopier<global::Scynapse.Grain> _baseTypeCopier)
        {
            this._baseTypeCopier = ScynapseGeneratedCodeHelper.UnwrapService(this, _baseTypeCopier);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void DeepCopy(global::TestProject.GrainWithGuidKey input, global::TestProject.GrainWithGuidKey output, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            _baseTypeCopier.DeepCopy(input, output, context);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Activator_GrainWithGuidKey : global::Scynapse.Serialization.Activators.IActivator<global::TestProject.GrainWithGuidKey>
    {
        public global::TestProject.GrainWithGuidKey Create() => new global::TestProject.GrainWithGuidKey();
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_Invokable_IMyGrainWithStringKey_GrainReference_43570316 : global::Scynapse.Serialization.Codecs.IFieldCodec<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316>
    {
        private readonly global::System.Type _codecFieldType = typeof(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316);
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316 instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316 instance)
        {
            reader.ConsumeEndBaseOrEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316 @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null)
            {
                ReferenceCodec.WriteNullReference(ref writer, fieldIdDelta);
                return;
            }

            ReferenceCodec.MarkValueField(writer.Session);
            writer.WriteStartObject(fieldIdDelta, expectedType, _codecFieldType);
            Serialize(ref writer, @value);
            writer.WriteEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316 ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            var result = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316();
            ReferenceCodec.MarkValueField(reader.Session);
            Deserialize(ref reader, result);
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_Invokable_IMyGrainWithStringKey_GrainReference_43570316 : global::Scynapse.Serialization.Cloning.IDeepCopier<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316>
    {
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316 DeepCopy(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316 original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (original is null)
                return null;
            var result = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316();
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_GrainWithStringKey : global::Scynapse.Serialization.Codecs.IFieldCodec<global::TestProject.GrainWithStringKey>, global::Scynapse.Serialization.Serializers.IBaseCodec<global::TestProject.GrainWithStringKey>
    {
        private readonly global::System.Type _codecFieldType = typeof(global::TestProject.GrainWithStringKey);
        private readonly global::Scynapse.Serialization.Serializers.IBaseCodec<global::Scynapse.Grain> _baseTypeSerializer;
        public Codec_GrainWithStringKey(global::Scynapse.Serialization.Serializers.IBaseCodec<global::Scynapse.Grain> _baseTypeSerializer)
        {
            this._baseTypeSerializer = ScynapseGeneratedCodeHelper.UnwrapService(this, _baseTypeSerializer);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, global::TestProject.GrainWithStringKey instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            _baseTypeSerializer.Serialize(ref writer, instance);
            writer.WriteEndBase();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::TestProject.GrainWithStringKey instance)
        {
            _baseTypeSerializer.Deserialize(ref reader, instance);
            reader.ConsumeEndBaseOrEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, global::TestProject.GrainWithStringKey @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null || @value.GetType() == typeof(global::TestProject.GrainWithStringKey))
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
        public global::TestProject.GrainWithStringKey ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<global::TestProject.GrainWithStringKey, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            global::System.Type valueType = field.FieldType;
            if (valueType is null || valueType == _codecFieldType)
            {
                var result = new global::TestProject.GrainWithStringKey();
                ReferenceCodec.RecordObject(reader.Session, result);
                Deserialize(ref reader, result);
                return result;
            }

            return reader.DeserializeUnexpectedType<TReaderInput, global::TestProject.GrainWithStringKey>(ref field);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_GrainWithStringKey : global::Scynapse.Serialization.Cloning.IDeepCopier<global::TestProject.GrainWithStringKey>, global::Scynapse.Serialization.Cloning.IBaseCopier<global::TestProject.GrainWithStringKey>
    {
        private readonly global::Scynapse.Serialization.Cloning.IBaseCopier<global::Scynapse.Grain> _baseTypeCopier;
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public global::TestProject.GrainWithStringKey DeepCopy(global::TestProject.GrainWithStringKey original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (context.TryGetCopy(original, out global::TestProject.GrainWithStringKey existing))
                return existing;
            if (original.GetType() != typeof(global::TestProject.GrainWithStringKey))
                return context.DeepCopy(original);
            var result = new global::TestProject.GrainWithStringKey();
            context.RecordCopy(original, result);
            DeepCopy(original, result, context);
            return result;
        }

        public Copier_GrainWithStringKey(global::Scynapse.Serialization.Cloning.IBaseCopier<global::Scynapse.Grain> _baseTypeCopier)
        {
            this._baseTypeCopier = ScynapseGeneratedCodeHelper.UnwrapService(this, _baseTypeCopier);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void DeepCopy(global::TestProject.GrainWithStringKey input, global::TestProject.GrainWithStringKey output, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            _baseTypeCopier.DeepCopy(input, output, context);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Activator_GrainWithStringKey : global::Scynapse.Serialization.Activators.IActivator<global::TestProject.GrainWithStringKey>
    {
        public global::TestProject.GrainWithStringKey Create() => new global::TestProject.GrainWithStringKey();
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF : global::Scynapse.Serialization.Codecs.IFieldCodec<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF>
    {
        private readonly global::System.Type _codecFieldType = typeof(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF);
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF instance)
        {
            reader.ConsumeEndBaseOrEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null)
            {
                ReferenceCodec.WriteNullReference(ref writer, fieldIdDelta);
                return;
            }

            ReferenceCodec.MarkValueField(writer.Session);
            writer.WriteStartObject(fieldIdDelta, expectedType, _codecFieldType);
            Serialize(ref writer, @value);
            writer.WriteEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            var result = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF();
            ReferenceCodec.MarkValueField(reader.Session);
            Deserialize(ref reader, result);
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF : global::Scynapse.Serialization.Cloning.IDeepCopier<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF>
    {
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF DeepCopy(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (original is null)
                return null;
            var result = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF();
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_GrainWithGuidCompoundKey : global::Scynapse.Serialization.Codecs.IFieldCodec<global::TestProject.GrainWithGuidCompoundKey>, global::Scynapse.Serialization.Serializers.IBaseCodec<global::TestProject.GrainWithGuidCompoundKey>
    {
        private readonly global::System.Type _codecFieldType = typeof(global::TestProject.GrainWithGuidCompoundKey);
        private readonly global::Scynapse.Serialization.Serializers.IBaseCodec<global::Scynapse.Grain> _baseTypeSerializer;
        public Codec_GrainWithGuidCompoundKey(global::Scynapse.Serialization.Serializers.IBaseCodec<global::Scynapse.Grain> _baseTypeSerializer)
        {
            this._baseTypeSerializer = ScynapseGeneratedCodeHelper.UnwrapService(this, _baseTypeSerializer);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, global::TestProject.GrainWithGuidCompoundKey instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            _baseTypeSerializer.Serialize(ref writer, instance);
            writer.WriteEndBase();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::TestProject.GrainWithGuidCompoundKey instance)
        {
            _baseTypeSerializer.Deserialize(ref reader, instance);
            reader.ConsumeEndBaseOrEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, global::TestProject.GrainWithGuidCompoundKey @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null || @value.GetType() == typeof(global::TestProject.GrainWithGuidCompoundKey))
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
        public global::TestProject.GrainWithGuidCompoundKey ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<global::TestProject.GrainWithGuidCompoundKey, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            global::System.Type valueType = field.FieldType;
            if (valueType is null || valueType == _codecFieldType)
            {
                var result = new global::TestProject.GrainWithGuidCompoundKey();
                ReferenceCodec.RecordObject(reader.Session, result);
                Deserialize(ref reader, result);
                return result;
            }

            return reader.DeserializeUnexpectedType<TReaderInput, global::TestProject.GrainWithGuidCompoundKey>(ref field);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_GrainWithGuidCompoundKey : global::Scynapse.Serialization.Cloning.IDeepCopier<global::TestProject.GrainWithGuidCompoundKey>, global::Scynapse.Serialization.Cloning.IBaseCopier<global::TestProject.GrainWithGuidCompoundKey>
    {
        private readonly global::Scynapse.Serialization.Cloning.IBaseCopier<global::Scynapse.Grain> _baseTypeCopier;
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public global::TestProject.GrainWithGuidCompoundKey DeepCopy(global::TestProject.GrainWithGuidCompoundKey original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (context.TryGetCopy(original, out global::TestProject.GrainWithGuidCompoundKey existing))
                return existing;
            if (original.GetType() != typeof(global::TestProject.GrainWithGuidCompoundKey))
                return context.DeepCopy(original);
            var result = new global::TestProject.GrainWithGuidCompoundKey();
            context.RecordCopy(original, result);
            DeepCopy(original, result, context);
            return result;
        }

        public Copier_GrainWithGuidCompoundKey(global::Scynapse.Serialization.Cloning.IBaseCopier<global::Scynapse.Grain> _baseTypeCopier)
        {
            this._baseTypeCopier = ScynapseGeneratedCodeHelper.UnwrapService(this, _baseTypeCopier);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void DeepCopy(global::TestProject.GrainWithGuidCompoundKey input, global::TestProject.GrainWithGuidCompoundKey output, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            _baseTypeCopier.DeepCopy(input, output, context);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Activator_GrainWithGuidCompoundKey : global::Scynapse.Serialization.Activators.IActivator<global::TestProject.GrainWithGuidCompoundKey>
    {
        public global::TestProject.GrainWithGuidCompoundKey Create() => new global::TestProject.GrainWithGuidCompoundKey();
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A : global::Scynapse.Serialization.Codecs.IFieldCodec<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A>
    {
        private readonly global::System.Type _codecFieldType = typeof(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A);
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A instance)
        {
            reader.ConsumeEndBaseOrEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null)
            {
                ReferenceCodec.WriteNullReference(ref writer, fieldIdDelta);
                return;
            }

            ReferenceCodec.MarkValueField(writer.Session);
            writer.WriteStartObject(fieldIdDelta, expectedType, _codecFieldType);
            Serialize(ref writer, @value);
            writer.WriteEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            var result = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A();
            ReferenceCodec.MarkValueField(reader.Session);
            Deserialize(ref reader, result);
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A : global::Scynapse.Serialization.Cloning.IDeepCopier<ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A>
    {
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A DeepCopy(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (original is null)
                return null;
            var result = new ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A();
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_GrainWithIntegerCompoundKey : global::Scynapse.Serialization.Codecs.IFieldCodec<global::TestProject.GrainWithIntegerCompoundKey>, global::Scynapse.Serialization.Serializers.IBaseCodec<global::TestProject.GrainWithIntegerCompoundKey>
    {
        private readonly global::System.Type _codecFieldType = typeof(global::TestProject.GrainWithIntegerCompoundKey);
        private readonly global::Scynapse.Serialization.Serializers.IBaseCodec<global::Scynapse.Grain> _baseTypeSerializer;
        public Codec_GrainWithIntegerCompoundKey(global::Scynapse.Serialization.Serializers.IBaseCodec<global::Scynapse.Grain> _baseTypeSerializer)
        {
            this._baseTypeSerializer = ScynapseGeneratedCodeHelper.UnwrapService(this, _baseTypeSerializer);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, global::TestProject.GrainWithIntegerCompoundKey instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            _baseTypeSerializer.Serialize(ref writer, instance);
            writer.WriteEndBase();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::TestProject.GrainWithIntegerCompoundKey instance)
        {
            _baseTypeSerializer.Deserialize(ref reader, instance);
            reader.ConsumeEndBaseOrEndObject();
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, global::TestProject.GrainWithIntegerCompoundKey @value)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            if (@value is null || @value.GetType() == typeof(global::TestProject.GrainWithIntegerCompoundKey))
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
        public global::TestProject.GrainWithIntegerCompoundKey ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<global::TestProject.GrainWithIntegerCompoundKey, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            global::System.Type valueType = field.FieldType;
            if (valueType is null || valueType == _codecFieldType)
            {
                var result = new global::TestProject.GrainWithIntegerCompoundKey();
                ReferenceCodec.RecordObject(reader.Session, result);
                Deserialize(ref reader, result);
                return result;
            }

            return reader.DeserializeUnexpectedType<TReaderInput, global::TestProject.GrainWithIntegerCompoundKey>(ref field);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_GrainWithIntegerCompoundKey : global::Scynapse.Serialization.Cloning.IDeepCopier<global::TestProject.GrainWithIntegerCompoundKey>, global::Scynapse.Serialization.Cloning.IBaseCopier<global::TestProject.GrainWithIntegerCompoundKey>
    {
        private readonly global::Scynapse.Serialization.Cloning.IBaseCopier<global::Scynapse.Grain> _baseTypeCopier;
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public global::TestProject.GrainWithIntegerCompoundKey DeepCopy(global::TestProject.GrainWithIntegerCompoundKey original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (context.TryGetCopy(original, out global::TestProject.GrainWithIntegerCompoundKey existing))
                return existing;
            if (original.GetType() != typeof(global::TestProject.GrainWithIntegerCompoundKey))
                return context.DeepCopy(original);
            var result = new global::TestProject.GrainWithIntegerCompoundKey();
            context.RecordCopy(original, result);
            DeepCopy(original, result, context);
            return result;
        }

        public Copier_GrainWithIntegerCompoundKey(global::Scynapse.Serialization.Cloning.IBaseCopier<global::Scynapse.Grain> _baseTypeCopier)
        {
            this._baseTypeCopier = ScynapseGeneratedCodeHelper.UnwrapService(this, _baseTypeCopier);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void DeepCopy(global::TestProject.GrainWithIntegerCompoundKey input, global::TestProject.GrainWithIntegerCompoundKey output, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            _baseTypeCopier.DeepCopy(input, output, context);
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Activator_GrainWithIntegerCompoundKey : global::Scynapse.Serialization.Activators.IActivator<global::TestProject.GrainWithIntegerCompoundKey>
    {
        public global::TestProject.GrainWithIntegerCompoundKey Create() => new global::TestProject.GrainWithIntegerCompoundKey();
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Metadata_TestProject : global::Scynapse.Serialization.Configuration.TypeManifestProviderBase
    {
        protected override void ConfigureInner(global::Scynapse.Serialization.Configuration.TypeManifestOptions config)
        {
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E));
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_GrainWithGuidKey));
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_Invokable_IMyGrainWithStringKey_GrainReference_43570316));
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_GrainWithStringKey));
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF));
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_GrainWithGuidCompoundKey));
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A));
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_GrainWithIntegerCompoundKey));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_GrainWithGuidKey));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_Invokable_IMyGrainWithStringKey_GrainReference_43570316));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_GrainWithStringKey));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_GrainWithGuidCompoundKey));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_GrainWithIntegerCompoundKey));
            config.InterfaceProxies.Add(typeof(ScynapseCodeGen.TestProject.Proxy_IMyGrainWithGuidKey));
            config.InterfaceProxies.Add(typeof(ScynapseCodeGen.TestProject.Proxy_IMyGrainWithStringKey));
            config.InterfaceProxies.Add(typeof(ScynapseCodeGen.TestProject.Proxy_IMyGrainWithGuidCompoundKey));
            config.InterfaceProxies.Add(typeof(ScynapseCodeGen.TestProject.Proxy_IMyGrainWithIntegerCompoundKey));
            config.Interfaces.Add(typeof(global::TestProject.IMyGrainWithGuidKey));
            config.Interfaces.Add(typeof(global::TestProject.IMyGrainWithStringKey));
            config.Interfaces.Add(typeof(global::TestProject.IMyGrainWithGuidCompoundKey));
            config.Interfaces.Add(typeof(global::TestProject.IMyGrainWithIntegerCompoundKey));
            config.InterfaceImplementations.Add(typeof(global::TestProject.GrainWithGuidKey));
            config.InterfaceImplementations.Add(typeof(global::TestProject.GrainWithStringKey));
            config.InterfaceImplementations.Add(typeof(global::TestProject.GrainWithGuidCompoundKey));
            config.InterfaceImplementations.Add(typeof(global::TestProject.GrainWithIntegerCompoundKey));
            config.Activators.Add(typeof(ScynapseCodeGen.TestProject.Activator_GrainWithGuidKey));
            config.Activators.Add(typeof(ScynapseCodeGen.TestProject.Activator_GrainWithStringKey));
            config.Activators.Add(typeof(ScynapseCodeGen.TestProject.Activator_GrainWithGuidCompoundKey));
            config.Activators.Add(typeof(ScynapseCodeGen.TestProject.Activator_GrainWithIntegerCompoundKey));
            var n1 = config.CompoundTypeAliases.Add("inv");
            var n2 = n1.Add(typeof(global::Scynapse.Runtime.GrainReference));
            var n3 = n2.Add(typeof(global::TestProject.IMyGrainWithGuidKey));
            n3.Add("8F0FEC0E", typeof(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidKey_GrainReference_8F0FEC0E));
            var n5 = n2.Add(typeof(global::TestProject.IMyGrainWithStringKey));
            n5.Add("43570316", typeof(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithStringKey_GrainReference_43570316));
            var n7 = n2.Add(typeof(global::TestProject.IMyGrainWithGuidCompoundKey));
            n7.Add("A9FEF7AF", typeof(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithGuidCompoundKey_GrainReference_A9FEF7AF));
            var n9 = n2.Add(typeof(global::TestProject.IMyGrainWithIntegerCompoundKey));
            n9.Add("9814021A", typeof(ScynapseCodeGen.TestProject.Invokable_IMyGrainWithIntegerCompoundKey_GrainReference_9814021A));
        }
    }
}
#pragma warning restore CS1591, RS0016, RS0041
