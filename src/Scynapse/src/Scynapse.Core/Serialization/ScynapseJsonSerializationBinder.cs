using System;
using Newtonsoft.Json.Serialization;
using Scynapse.Serialization.TypeSystem;

namespace Scynapse.Serialization
{
    /// <summary>
    /// Implementation of <see cref="ISerializationBinder"/> which resolves types using a <see cref="TypeResolver"/>.
    /// </summary>
    public class ScynapseJsonSerializationBinder : DefaultSerializationBinder
    {
        private readonly TypeResolver typeResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScynapseJsonSerializationBinder"/> class.
        /// </summary>
        /// <param name="typeResolver">The type resolver.</param>
        public ScynapseJsonSerializationBinder(TypeResolver typeResolver)
        {
            this.typeResolver = typeResolver;
        }

        /// <inheritdoc />
        public override Type BindToType(string assemblyName, string typeName)
        {
            var fullName = !string.IsNullOrWhiteSpace(assemblyName) ? typeName + ',' + assemblyName : typeName;
            if (typeResolver.TryResolveType(fullName, out var type)) return type;

            return base.BindToType(assemblyName, typeName);
        }
    }
}