using System;
using System.Collections.Concurrent;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Scynapse.Metadata;
using Scynapse.Runtime;

#nullable enable

namespace Scynapse.DynamicGrains
{
    /// <summary>
    /// A dynamic wrapper around a grain reference that enables late-bound method invocation
    /// using the Dynamic Language Runtime (DLR).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class allows calling grain methods without compile-time knowledge of the grain interface.
    /// Method calls are resolved at runtime using reflection.
    /// </para>
    /// <para>
    /// Usage:
    /// <code>
    /// dynamic grain = grainFactory.GetGrainDynamic("MyNamespace.IHelloGrain", "my-key");
    /// string result = await grain.SayHello("World");
    /// </code>
    /// </para>
    /// </remarks>
    public sealed class DynamicGrainReference : DynamicObject
    {
        private static readonly ConcurrentDictionary<(Type, string, int), MethodInfo> _methodCache = new();

        private readonly IAddressable _grainReference;
        private readonly Type _interfaceType;
        private readonly GrainTypeMeta? _metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicGrainReference"/> class.
        /// </summary>
        /// <param name="grainReference">The underlying grain reference.</param>
        /// <param name="interfaceType">The grain interface type (if known).</param>
        /// <param name="metadata">Optional grain type metadata from GTD.</param>
        public DynamicGrainReference(
            IAddressable grainReference,
            Type? interfaceType = null,
            GrainTypeMeta? metadata = null)
        {
            _grainReference = grainReference ?? throw new ArgumentNullException(nameof(grainReference));
            _interfaceType = interfaceType ?? grainReference.GetType();
            _metadata = metadata;
        }

        /// <summary>
        /// Gets the underlying grain reference.
        /// </summary>
        public IAddressable GrainReference => _grainReference;

        /// <summary>
        /// Gets the grain interface type.
        /// </summary>
        public Type InterfaceType => _interfaceType;

        /// <summary>
        /// Gets the grain type metadata (if available).
        /// </summary>
        public GrainTypeMeta? Metadata => _metadata;

        /// <summary>
        /// Gets the grain ID.
        /// </summary>
        public GrainId GrainId => _grainReference switch
        {
            IAddressable { } addressable when addressable is GrainReference gr => gr.GrainId,
            _ => default
        };

        /// <inheritdoc />
        public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
        {
            var methodName = binder.Name;
            var argTypes = args?.Select(a => a?.GetType() ?? typeof(object)).ToArray() ?? Array.Empty<Type>();

            // Try to find the method on the grain reference type
            var method = FindMethod(_interfaceType, methodName, argTypes.Length);

            if (method == null)
            {
                // Try to find on the actual grain reference type (proxy type)
                method = FindMethod(_grainReference.GetType(), methodName, argTypes.Length);
            }

            if (method == null)
            {
                result = null;
                return false;
            }

            try
            {
                result = method.Invoke(_grainReference, args);
                return true;
            }
            catch (TargetInvocationException ex)
            {
                // Unwrap and rethrow the inner exception
                throw ex.InnerException ?? ex;
            }
        }

        /// <inheritdoc />
        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            var property = _interfaceType.GetProperty(binder.Name)
                ?? _grainReference.GetType().GetProperty(binder.Name);

            if (property != null)
            {
                result = property.GetValue(_grainReference);
                return true;
            }

            result = null;
            return false;
        }

        /// <inheritdoc />
        public override bool TryConvert(ConvertBinder binder, out object? result)
        {
            if (binder.Type.IsAssignableFrom(_grainReference.GetType()))
            {
                result = _grainReference;
                return true;
            }

            if (binder.Type == typeof(IAddressable))
            {
                result = _grainReference;
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Invokes a method by name with the specified arguments.
        /// </summary>
        /// <param name="methodName">The method name.</param>
        /// <param name="args">The method arguments.</param>
        /// <returns>The result of the method invocation.</returns>
        public Task<object?> InvokeAsync(string methodName, params object?[]? args)
        {
            var argTypes = args?.Select(a => a?.GetType() ?? typeof(object)).ToArray() ?? Array.Empty<Type>();
            var method = FindMethod(_interfaceType, methodName, argTypes.Length)
                ?? FindMethod(_grainReference.GetType(), methodName, argTypes.Length);

            if (method == null)
            {
                throw new MissingMethodException(_interfaceType.FullName, methodName);
            }

            try
            {
                var result = method.Invoke(_grainReference, args);

                // Handle Task<T> return types
                if (result is Task task)
                {
                    return WrapTaskResult(task);
                }

                return Task.FromResult(result);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        private static async Task<object?> WrapTaskResult(Task task)
        {
            await task.ConfigureAwait(false);

            // Get the result if it's a Task<T>
            var taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                var resultProperty = taskType.GetProperty("Result");
                return resultProperty?.GetValue(task);
            }

            return null;
        }

        private static MethodInfo? FindMethod(Type type, string methodName, int argCount)
        {
            var cacheKey = (type, methodName, argCount);

            if (_methodCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            // Find methods with matching name and parameter count
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == methodName && m.GetParameters().Length == argCount)
                .ToArray();

            if (methods.Length == 0)
            {
                // Also check interfaces
                foreach (var iface in type.GetInterfaces())
                {
                    methods = iface.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.Name == methodName && m.GetParameters().Length == argCount)
                        .ToArray();

                    if (methods.Length > 0)
                    {
                        break;
                    }
                }
            }

            var method = methods.FirstOrDefault();

            if (method != null)
            {
                _methodCache.TryAdd(cacheKey, method);
            }

            return method;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"DynamicGrainReference({_interfaceType.Name}, {GrainId})";
        }
    }
}
