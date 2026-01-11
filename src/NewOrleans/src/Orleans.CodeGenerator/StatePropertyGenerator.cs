using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orleans.CodeGenerator.SyntaxGeneration;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Orleans.CodeGenerator
{
    /// <summary>
    /// Generates StateTask properties on proxy classes for Get/Set method pairs found on grain interfaces.
    /// This enables property-like syntax for accessing grain state: <c>await grain.Name</c> and <c>await (grain.Name &lt;&lt; "value")</c>
    /// </summary>
    internal class StatePropertyGenerator
    {
        private readonly CodeGenerator _codeGenerator;

        public StatePropertyGenerator(CodeGenerator codeGenerator)
        {
            _codeGenerator = codeGenerator;
        }

        private LibraryTypes LibraryTypes => _codeGenerator.LibraryTypes;

        /// <summary>
        /// Represents a detected state property with its getter and setter methods.
        /// </summary>
        public class StatePropertyDescription
        {
            public string PropertyName { get; }
            public ITypeSymbol PropertyType { get; }
            public IMethodSymbol GetterMethod { get; }
            public IMethodSymbol? SetterMethod { get; }

            public StatePropertyDescription(
                string propertyName,
                ITypeSymbol propertyType,
                IMethodSymbol getterMethod,
                IMethodSymbol? setterMethod)
            {
                PropertyName = propertyName;
                PropertyType = propertyType;
                GetterMethod = getterMethod;
                SetterMethod = setterMethod;
            }

            public bool HasSetter => SetterMethod is not null;
        }

        /// <summary>
        /// Detects state properties by finding Get/Set method pairs on the interface.
        /// A property is detected when:
        /// - There's a method named "GetXXX" with no parameters returning Task&lt;T&gt; or ValueTask&lt;T&gt;
        /// - Optionally, there's a matching "SetXXX" with one parameter of type T returning Task or ValueTask
        /// </summary>
        public List<StatePropertyDescription> DetectStateProperties(ProxyInterfaceDescription interfaceDescription)
        {
            if (!LibraryTypes.SupportsStateProperties)
            {
                return new List<StatePropertyDescription>();
            }

            var properties = new Dictionary<string, StatePropertyDescription>(StringComparer.Ordinal);

            // First pass: find all getters (GetXXX methods with no params returning Task<T>/ValueTask<T>)
            foreach (var methodDesc in interfaceDescription.Methods)
            {
                var method = methodDesc.Method;

                // Check for getter pattern: GetXXX()
                if (method.Name.StartsWith("Get", StringComparison.Ordinal) &&
                    method.Name.Length > 3 &&
                    method.Parameters.Length == 0)
                {
                    var returnType = method.ReturnType as INamedTypeSymbol;
                    if (returnType is null) continue;

                    // Must return Task<T> or ValueTask<T>
                    ITypeSymbol? propertyType = null;
                    if (returnType.TypeArguments.Length == 1)
                    {
                        if (SymbolEqualityComparer.Default.Equals(returnType.ConstructedFrom, LibraryTypes.Task_1) ||
                            SymbolEqualityComparer.Default.Equals(returnType.ConstructedFrom, LibraryTypes.ValueTask_1))
                        {
                            propertyType = returnType.TypeArguments[0];
                        }
                    }

                    if (propertyType is null) continue;

                    var propertyName = method.Name.Substring(3); // Remove "Get" prefix
                    properties[propertyName] = new StatePropertyDescription(
                        propertyName,
                        propertyType,
                        method,
                        setterMethod: null);
                }
            }

            // Second pass: find matching setters
            foreach (var methodDesc in interfaceDescription.Methods)
            {
                var method = methodDesc.Method;

                // Check for setter pattern: SetXXX(T value)
                if (method.Name.StartsWith("Set", StringComparison.Ordinal) &&
                    method.Name.Length > 3 &&
                    method.Parameters.Length == 1)
                {
                    var returnType = method.ReturnType as INamedTypeSymbol;
                    if (returnType is null) continue;

                    // Must return Task or ValueTask (not generic versions)
                    var isValidReturnType =
                        SymbolEqualityComparer.Default.Equals(returnType, LibraryTypes.Task) ||
                        SymbolEqualityComparer.Default.Equals(returnType, LibraryTypes.ValueTask);

                    if (!isValidReturnType) continue;

                    var propertyName = method.Name.Substring(3); // Remove "Set" prefix

                    // Check if there's a matching getter with compatible type
                    if (properties.TryGetValue(propertyName, out var existing))
                    {
                        var paramType = method.Parameters[0].Type;
                        if (SymbolEqualityComparer.Default.Equals(paramType, existing.PropertyType))
                        {
                            // Update the property description to include the setter
                            properties[propertyName] = new StatePropertyDescription(
                                existing.PropertyName,
                                existing.PropertyType,
                                existing.GetterMethod,
                                method);
                        }
                    }
                }
            }

            return properties.Values.ToList();
        }

        /// <summary>
        /// Generates StateTask property declarations for the proxy class.
        /// </summary>
        public MemberDeclarationSyntax[] GenerateStateTaskProperties(
            ProxyInterfaceDescription interfaceDescription,
            List<StatePropertyDescription> stateProperties,
            Dictionary<ITypeParameterSymbol, string> typeParameterSubstitutions)
        {
            if (stateProperties.Count == 0 || LibraryTypes.StateTask_1 is null)
            {
                return Array.Empty<MemberDeclarationSyntax>();
            }

            var properties = new List<MemberDeclarationSyntax>();

            foreach (var prop in stateProperties)
            {
                var property = CreateStateTaskProperty(prop, typeParameterSubstitutions);
                properties.Add(property);
            }

            return properties.ToArray();
        }

        private PropertyDeclarationSyntax CreateStateTaskProperty(
            StatePropertyDescription prop,
            Dictionary<ITypeParameterSymbol, string> typeParameterSubstitutions)
        {
            // StateTask<T> where T is the property type
            var stateTaskType = LibraryTypes.StateTask_1!.Construct(prop.PropertyType);
            var typeSyntax = stateTaskType.ToTypeSyntax(typeParameterSubstitutions);

            // The getter delegate calls the Get method
            var getterExpression = CreateGetterLambda(prop);

            // The setter delegate calls the Set method (or throws if no setter)
            var setterExpression = CreateSetterLambda(prop);

            // new StateTask<T>(() => GetXXX().AsValueTask(), v => SetXXX(v).AsValueTask())
            var newExpression = ObjectCreationExpression(typeSyntax)
                .WithArgumentList(ArgumentList(SeparatedList(new[]
                {
                    Argument(getterExpression),
                    Argument(setterExpression)
                })));

            // public StateTask<T> PropertyName => new StateTask<T>(getter, setter);
            var property = PropertyDeclaration(typeSyntax, Identifier(prop.PropertyName))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithExpressionBody(ArrowExpressionClause(newExpression))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            return property;
        }

        private ExpressionSyntax CreateGetterLambda(StatePropertyDescription prop)
        {
            // Get method call: GetXXX()
            var getMethodCall = InvocationExpression(IdentifierName(prop.GetterMethod.Name));

            // Check return type to see if we need .AsValueTask() conversion
            var returnType = prop.GetterMethod.ReturnType as INamedTypeSymbol;
            ExpressionSyntax resultExpr;

            if (SymbolEqualityComparer.Default.Equals(returnType?.ConstructedFrom, LibraryTypes.Task_1))
            {
                // Task<T> -> need to convert to ValueTask<T>: new ValueTask<T>(GetXXX())
                var valueTaskType = LibraryTypes.ValueTask_1.Construct(prop.PropertyType);
                resultExpr = ObjectCreationExpression(valueTaskType.ToTypeSyntax())
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(getMethodCall))));
            }
            else
            {
                // Already ValueTask<T>
                resultExpr = getMethodCall;
            }

            // () => resultExpr
            return ParenthesizedLambdaExpression(resultExpr);
        }

        private ExpressionSyntax CreateSetterLambda(StatePropertyDescription prop)
        {
            if (!prop.HasSetter)
            {
                // No setter - return a lambda that throws
                // v => throw new NotSupportedException("Property 'XXX' is read-only")
                return SimpleLambdaExpression(
                    Parameter(Identifier("v")),
                    ThrowExpression(
                        ObjectCreationExpression(ParseTypeName("global::System.NotSupportedException"))
                            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                Argument(LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal($"Property '{prop.PropertyName}' is read-only"))))))));
            }

            // v => SetXXX(v) with appropriate conversion
            var setMethodCall = InvocationExpression(
                IdentifierName(prop.SetterMethod!.Name),
                ArgumentList(SingletonSeparatedList(Argument(IdentifierName("v")))));

            var returnType = prop.SetterMethod.ReturnType as INamedTypeSymbol;
            ExpressionSyntax resultExpr;

            if (SymbolEqualityComparer.Default.Equals(returnType, LibraryTypes.Task))
            {
                // Task -> need to convert to ValueTask: new ValueTask(SetXXX(v))
                resultExpr = ObjectCreationExpression(LibraryTypes.ValueTask.ToTypeSyntax())
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(setMethodCall))));
            }
            else
            {
                // Already ValueTask
                resultExpr = setMethodCall;
            }

            // v => resultExpr
            return SimpleLambdaExpression(
                Parameter(Identifier("v")),
                resultExpr);
        }
    }
}
