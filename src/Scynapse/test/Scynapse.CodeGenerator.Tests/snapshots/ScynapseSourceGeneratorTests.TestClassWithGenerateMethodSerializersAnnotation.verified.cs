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
    [global::Scynapse.CompoundTypeAliasAttribute("inv", typeof(global::Scynapse.Runtime.GrainReference), typeof(global::IMyGrain), "6D39E404")]
    public sealed class Invokable_IMyGrain_GrainReference_6D39E404 : global::Scynapse.Runtime.TaskRequest<string>
    {
        public string arg0;
        global::IMyGrain _target;
        private static readonly global::System.Reflection.MethodInfo MethodBackingField = ScynapseGeneratedCodeHelper.GetMethodInfoOrDefault(typeof(global::IMyGrain), "SayHello", null, new[] { typeof(string) });
        public override int GetArgumentCount() => 1;
        public override string GetMethodName() => "SayHello";
        public override string GetInterfaceName() => "IMyGrain";
        public override string GetActivityName() => "IMyGrain/SayHello";
        public override global::System.Type GetInterfaceType() => typeof(global::IMyGrain);
        public override global::System.Reflection.MethodInfo GetMethod() => MethodBackingField;
        public override void SetTarget(global::Scynapse.Serialization.Invocation.ITargetHolder holder) => _target = holder.GetTarget<global::IMyGrain>();
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

        protected override global::System.Threading.Tasks.Task<string> InvokeInner() => _target.SayHello(arg0);
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    internal sealed class Proxy_IMyGrain : global::Scynapse.Runtime.GrainReference, global::IMyGrain
    {
        public Proxy_IMyGrain(global::Scynapse.Runtime.GrainReferenceShared arg0, global::Scynapse.Runtime.IdSpan arg1) : base(arg0, arg1)
        {
        }

        global::System.Threading.Tasks.Task<string> global::IMyGrain.SayHello(string arg0)
        {
            var request = new ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404();
            request.arg0 = arg0;
            return base.InvokeAsync<string>(request).AsTask();
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Codec_Invokable_IMyGrain_GrainReference_6D39E404 : global::Scynapse.Serialization.Codecs.IFieldCodec<ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404>
    {
        private readonly global::System.Type _codecFieldType = typeof(ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404);
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Serialize<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404 instance)
            where TBufferWriter : global::System.Buffers.IBufferWriter<byte>
        {
            global::Scynapse.Serialization.Codecs.StringCodec.WriteField(ref writer, 0U, instance.arg0);
        }

        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Deserialize<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404 instance)
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
        public void WriteField<TBufferWriter>(ref global::Scynapse.Serialization.Buffers.Writer<TBufferWriter> writer, uint fieldIdDelta, global::System.Type expectedType, ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404 @value)
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
        public ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404 ReadValue<TReaderInput>(ref global::Scynapse.Serialization.Buffers.Reader<TReaderInput> reader, global::Scynapse.Serialization.WireProtocol.Field field)
        {
            if (field.IsReference)
                return ReferenceCodec.ReadReference<ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404, TReaderInput>(ref reader, field);
            field.EnsureWireTypeTagDelimited();
            var result = new ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404();
            ReferenceCodec.MarkValueField(reader.Session);
            Deserialize(ref reader, result);
            return result;
        }
    }

    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("ScynapseCodeGen", "9.0.0.0"), global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never), global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute]
    public sealed class Copier_Invokable_IMyGrain_GrainReference_6D39E404 : global::Scynapse.Serialization.Cloning.IDeepCopier<ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404>
    {
        [global::System.Runtime.CompilerServices.MethodImplAttribute(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404 DeepCopy(ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404 original, global::Scynapse.Serialization.Cloning.CopyContext context)
        {
            if (original is null)
                return null;
            var result = new ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404();
            result.arg0 = original.arg0;
            return result;
        }
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
            config.Serializers.Add(typeof(ScynapseCodeGen.Codec_Invokable_IMyGrain_GrainReference_6D39E404));
            config.Copiers.Add(typeof(ScynapseCodeGen.Copier_Invokable_IMyGrain_GrainReference_6D39E404));
            config.InterfaceProxies.Add(typeof(ScynapseCodeGen.Proxy_IMyGrain));
            config.Interfaces.Add(typeof(global::IMyGrain));
            var n1 = config.CompoundTypeAliases.Add("inv");
            var n2 = n1.Add(typeof(global::Scynapse.Runtime.GrainReference));
            var n3 = n2.Add(typeof(global::IMyGrain));
            n3.Add("6D39E404", typeof(ScynapseCodeGen.Invokable_IMyGrain_GrainReference_6D39E404));
        }
    }
}
#pragma warning restore CS1591, RS0016, RS0041
