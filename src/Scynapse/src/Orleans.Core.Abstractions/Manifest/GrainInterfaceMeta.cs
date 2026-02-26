using System;
using System.Collections.Immutable;
using Orleans.Runtime;

namespace Orleans.Metadata
{
    /// <summary>
    /// Metadata about a grain interface.
    /// </summary>
    [Serializable, GenerateSerializer, Immutable]
    public sealed class GrainInterfaceMeta
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrainInterfaceMeta"/> class.
        /// </summary>
        public GrainInterfaceMeta(
            GrainInterfaceType interfaceType,
            string fullName,
            ImmutableList<GrainMethodMeta> methods)
        {
            InterfaceType = interfaceType;
            FullName = fullName ?? throw new ArgumentNullException(nameof(fullName));
            Methods = methods ?? ImmutableList<GrainMethodMeta>.Empty;
        }

        /// <summary>
        /// Gets the Orleans interface type identifier.
        /// </summary>
        [Id(0)]
        public GrainInterfaceType InterfaceType { get; }

        /// <summary>
        /// Gets the full CLR type name of the interface.
        /// </summary>
        [Id(1)]
        public string FullName { get; }

        /// <summary>
        /// Gets the methods available on this interface.
        /// </summary>
        [Id(2)]
        public ImmutableList<GrainMethodMeta> Methods { get; }
    }

    /// <summary>
    /// Metadata about a grain method (for reflection-like invocation).
    /// </summary>
    [Serializable, GenerateSerializer, Immutable]
    public sealed class GrainMethodMeta
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrainMethodMeta"/> class.
        /// </summary>
        public GrainMethodMeta(
            string name,
            string returnType,
            ImmutableList<GrainParameterMeta> parameters,
            int methodId)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
            Parameters = parameters ?? ImmutableList<GrainParameterMeta>.Empty;
            MethodId = methodId;
        }

        /// <summary>
        /// Gets the name of the method.
        /// </summary>
        [Id(0)]
        public string Name { get; }

        /// <summary>
        /// Gets the return type name.
        /// </summary>
        [Id(1)]
        public string ReturnType { get; }

        /// <summary>
        /// Gets the method parameters.
        /// </summary>
        [Id(2)]
        public ImmutableList<GrainParameterMeta> Parameters { get; }

        /// <summary>
        /// Gets the Orleans method identifier used for dispatch.
        /// </summary>
        [Id(3)]
        public int MethodId { get; }
    }

    /// <summary>
    /// Metadata about a method parameter.
    /// </summary>
    [Serializable, GenerateSerializer, Immutable]
    public sealed class GrainParameterMeta
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GrainParameterMeta"/> class.
        /// </summary>
        public GrainParameterMeta(
            string name,
            string typeName,
            bool isOptional)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            IsOptional = isOptional;
        }

        /// <summary>
        /// Gets the parameter name.
        /// </summary>
        [Id(0)]
        public string Name { get; }

        /// <summary>
        /// Gets the parameter type name.
        /// </summary>
        [Id(1)]
        public string TypeName { get; }

        /// <summary>
        /// Gets whether the parameter is optional (has a default value).
        /// </summary>
        [Id(2)]
        public bool IsOptional { get; }
    }
}
