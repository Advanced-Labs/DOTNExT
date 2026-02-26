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
    [global::Scynapse.CompoundTypeAliasAttribute("inv", typeof(global::Scynapse.Runtime.GrainReference), typeof(global::TestProject.IResponseTimeoutGrain), "6BE752C8")]
    public sealed class Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8 : global::Scynapse.Runtime.TaskRequest<string>
    {
        public string arg0;
        global::TestProject.IResponseTimeoutGrain _target;
        private static readonly global::System.Reflection.MethodInfo MethodBackingField = ScynapseGeneratedCodeHelper.GetMethodInfoOrDefault(typeof(global::TestProject.IResponseTimeoutGrain), "LongRunningMethod", null, new[] { typeof(string) });
        private static readonly global::System.TimeSpan _responseTimeoutValue = global::System.TimeSpan.FromTicks(100000000L);
        public override global::System.TimeSpan? GetDefaultResponseTimeout() => _responseTimeoutValue;
        public override int GetArgumentCount() => 1;
        public override string GetMethodName() => "LongRunningMethod";
        public override string GetInterfaceName() => "TestProject.IResponseTimeoutGrain";
        public override string GetActivityName() => "IResponseTimeoutGrain/LongRunningMethod";
        public override global::System.Type GetInterfaceType() => typeof(global::TestProject.IResponseTimeoutGrain);
        public override global::System.Reflection.MethodInfo GetMethod() => MethodBackingField;
        public override void SetTarget(global::Scynapse.Serialization.Invocation.ITargetHolder holder) => _target = holder.GetTarget<global::TestProject.IResponseTimeoutGrain>();
        public override object GetTarget() => _target;
        public override void Dispose()
        {
            arg0 = default;
            _target = default;
        }

        public override object GetArgument(int index)
        {
            switch (index)
            {
                case 0:
                    return arg0;
                default:
                    return ScynapseGeneratedCodeHelper.InvokableThrowArgumentOutOfRange(index, 0);
            }
        }

        public override void SetArgument(int index, object value)
        {
            switch (index)
            {
                case 0:
                    arg0 = (string)value;
                    return;
                default:
                    ScynapseGeneratedCodeHelper.InvokableThrowArgumentOutOfRange(index, 0);
                    return;
            }
        }

        protected override global::System.Threading.Tasks.Task<string> InvokeInner() => _target.LongRunningMethod(arg0);
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Proxy_IResponseTimeoutGrain : global::Scynapse.Runtime.GrainReference, global::TestProject.IResponseTimeoutGrain
    {
        public Proxy_IResponseTimeoutGrain(global::Scynapse.Runtime.GrainReferenceShared arg0, global::Scynapse.Runtime.IdSpan arg1) : base(arg0, arg1)
        {
        }

        global::System.Threading.Tasks.Task<string> global::TestProject.IResponseTimeoutGrain.LongRunningMethod(string arg0)
        {
            var request = new ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8();
            request.arg0 = arg0;
            return base.InvokeAsync<string>(request).AsTask();
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8 : global::Scynapse.Serialization.Codecs.IFieldCodec<ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8>
    {
        private readonly global::System.Type _codecFieldType = typeof(ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8);
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8 instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            global::Scynapse.Serialization.Codecs.StringCodec.WriteField(ref writer, 0U, instance.arg0);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8 instance)
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
                    instance.arg0 = global::Scynapse.Serialization.Codecs.StringCodec.ReadValue(ref reader, header);
                    reader.ReadFieldHeader(ref header);
                }

                reader.ConsumeEndBaseOrEndObject(ref header);
                break;
            }
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8 @value)
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
        public ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8 ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            var result = new ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8();
            ReferenceCodec.MarkValueField(reader.Session);
            Deserialize(ref reader, result);
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8 : global::Scynapse.Serialization.Cloning.IDeepCopier<ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8>
    {
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8 DeepCopy(ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8 original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (original is null)
                return null;
            var result = new ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8();
            result.arg0 = original.arg0;
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Metadata_TestProject : global::Scynapse.Serialization.Configuration.TypeManifestProviderBase
    {
        protected override void ConfigureInner(global::Scynapse.Serialization.Configuration.TypeManifestOptions config)
        {
            config.Serializers.Add(typeof(ScynapseCodeGen.TestProject.Codec_Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8));
            config.Copiers.Add(typeof(ScynapseCodeGen.TestProject.Copier_Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8));
            config.InterfaceProxies.Add(typeof(ScynapseCodeGen.TestProject.Proxy_IResponseTimeoutGrain));
            config.Interfaces.Add(typeof(global::TestProject.IResponseTimeoutGrain));
            config.InterfaceImplementations.Add(typeof(global::TestProject.ResponseTimeoutGrain));
            var n1 = config.CompoundTypeAliases.Add("inv");
            var n2 = n1.Add(typeof(global::Scynapse.Runtime.GrainReference));
            var n3 = n2.Add(typeof(global::TestProject.IResponseTimeoutGrain));
            n3.Add("6BE752C8", typeof(ScynapseCodeGen.TestProject.Invokable_IResponseTimeoutGrain_GrainReference_6BE752C8));
        }
    }
}
#pragma warning restore CS1591, RS0016, RS0041
