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
    /// Generates code for state property access on grains.
    ///
    /// This generator is PROPERTY-DRIVEN: it scans grain implementation classes for public properties
    /// and generates:
    /// 1. Interface method signatures (GetX/SetX) on the partial grain interface
    /// 2. Method implementations on the partial grain class
    /// 3. StateTask&lt;T&gt; properties on the proxy class
    /// </summary>
    internal class StatePropertyCodeGenerator
    {
        private readonly CodeGenerator _codeGenerator;

        public StatePropertyCodeGenerator(CodeGenerator codeGenerator)
        {
            _codeGenerator = codeGenerator;
        }

        private LibraryTypes LibraryTypes => _codeGenerator.LibraryTypes;

        /// <summary>
        /// Describes a state property detected on a grain class.
        /// </summary>
        public class StatePropertyDescription
        {
            public StatePropertyDescription(
                IPropertySymbol property,
                INamedTypeSymbol grainClass,
                INamedTypeSymbol grainInterface,
                string methodName,
                bool canSet,
                bool isPartial)
            {
                Property = property;
                GrainClass = grainClass;
                GrainInterface = grainInterface;
                MethodName = methodName;
                CanSet = canSet;
                IsPartial = isPartial;
            }

            /// <summary>The property symbol on the grain class.</summary>
            public IPropertySymbol Property { get; }

            /// <summary>The grain class containing the property.</summary>
            public INamedTypeSymbol GrainClass { get; }

            /// <summary>The grain interface to add methods to.</summary>
            public INamedTypeSymbol GrainInterface { get; }

            /// <summary>The base name for Get/Set methods (e.g., "Name" → GetName/SetName).</summary>
            public string MethodName { get; }

            /// <summary>Whether a setter method should be generated.</summary>
            public bool CanSet { get; }

            /// <summary>Whether the property is declared as partial (needs backing field generation).</summary>
            public bool IsPartial { get; }

            /// <summary>The property type.</summary>
            public ITypeSymbol PropertyType => Property.Type;

            /// <summary>The property name.</summary>
            public string PropertyName => Property.Name;
        }

        /// <summary>
        /// Scans a grain class for state properties.
        /// </summary>
        /// <param name="grainClass">The grain class to scan.</param>
        /// <returns>List of detected state properties.</returns>
        public List<StatePropertyDescription> ScanGrainClass(INamedTypeSymbol grainClass)
        {
            if (!LibraryTypes.SupportsStateProperties)
            {
                return new List<StatePropertyDescription>();
            }

            var properties = new List<StatePropertyDescription>();

            // Find the primary grain interface (first interface inheriting from IGrainWithXXXKey)
            var grainInterface = FindPrimaryGrainInterface(grainClass);
            if (grainInterface is null)
            {
                return properties;
            }

            // Scan public properties on the grain class
            foreach (var member in grainClass.GetMembers())
            {
                if (member is not IPropertySymbol property)
                    continue;

                // Skip non-public properties
                if (property.DeclaredAccessibility != Accessibility.Public)
                    continue;

                // Skip properties with [NotState] attribute
                if (HasAttribute(property, LibraryTypes.NotStateAttribute))
                    continue;

                // Skip indexers
                if (property.IsIndexer)
                    continue;

                // Get method name from [State(MethodName = "...")] or use property name
                var methodName = GetMethodName(property);

                // Get CanSet from [State(CanSet = false)] or check if property has setter
                var canSet = GetCanSet(property);

                // Check if property is partial (syntax-level check)
                var isPartial = IsPartialProperty(property);

                properties.Add(new StatePropertyDescription(
                    property,
                    grainClass,
                    grainInterface,
                    methodName,
                    canSet,
                    isPartial));
            }

            return properties;
        }

        /// <summary>
        /// Generates interface method signatures for state properties.
        /// These are added to the partial grain interface.
        /// </summary>
        public MemberDeclarationSyntax[] GenerateInterfaceMethodSignatures(
            IEnumerable<StatePropertyDescription> properties)
        {
            var methods = new List<MemberDeclarationSyntax>();

            foreach (var prop in properties)
            {
                // Generate: Task<T> GetPropertyName();
                var getterReturnType = GenericName(
                    Identifier("global::System.Threading.Tasks.Task"),
                    TypeArgumentList(SingletonSeparatedList(prop.PropertyType.ToTypeSyntax())));

                var getter = MethodDeclaration(getterReturnType, Identifier($"Get{prop.MethodName}"))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

                methods.Add(getter);

                // Generate: Task SetPropertyName(T value);
                if (prop.CanSet)
                {
                    var setterReturnType = ParseTypeName("global::System.Threading.Tasks.Task");
                    var parameter = Parameter(Identifier("value"))
                        .WithType(prop.PropertyType.ToTypeSyntax());

                    var setter = MethodDeclaration(setterReturnType, Identifier($"Set{prop.MethodName}"))
                        .WithParameterList(ParameterList(SingletonSeparatedList(parameter)))
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

                    methods.Add(setter);
                }
            }

            return methods.ToArray();
        }

        /// <summary>
        /// Generates grain class method implementations that delegate to properties.
        /// These are added to the partial grain class.
        /// </summary>
        public MemberDeclarationSyntax[] GenerateGrainMethodImplementations(
            IEnumerable<StatePropertyDescription> properties)
        {
            var methods = new List<MemberDeclarationSyntax>();

            foreach (var prop in properties)
            {
                // Generate: Task<T> IGrainInterface.GetPropertyName() => Task.FromResult(PropertyName);
                var getterReturnType = GenericName(
                    Identifier("global::System.Threading.Tasks.Task"),
                    TypeArgumentList(SingletonSeparatedList(prop.PropertyType.ToTypeSyntax())));

                var getterBody = ArrowExpressionClause(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            ParseTypeName("global::System.Threading.Tasks.Task"),
                            IdentifierName("FromResult")))
                        .WithArgumentList(ArgumentList(SingletonSeparatedList(
                            Argument(IdentifierName(prop.PropertyName))))));

                var getter = MethodDeclaration(getterReturnType, Identifier($"Get{prop.MethodName}"))
                    .WithExplicitInterfaceSpecifier(
                        ExplicitInterfaceSpecifier(prop.GrainInterface.ToNameSyntax()))
                    .WithExpressionBody(getterBody)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

                methods.Add(getter);

                // Generate: Task IGrainInterface.SetPropertyName(T value) { PropertyName = value; return Task.CompletedTask; }
                if (prop.CanSet)
                {
                    var setterReturnType = ParseTypeName("global::System.Threading.Tasks.Task");
                    var parameter = Parameter(Identifier("value"))
                        .WithType(prop.PropertyType.ToTypeSyntax());

                    var setterBody = Block(
                        ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(prop.PropertyName),
                                IdentifierName("value"))),
                        ReturnStatement(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ParseTypeName("global::System.Threading.Tasks.Task"),
                                IdentifierName("CompletedTask"))));

                    var setter = MethodDeclaration(setterReturnType, Identifier($"Set{prop.MethodName}"))
                        .WithExplicitInterfaceSpecifier(
                            ExplicitInterfaceSpecifier(prop.GrainInterface.ToNameSyntax()))
                        .WithParameterList(ParameterList(SingletonSeparatedList(parameter)))
                        .WithBody(setterBody);

                    methods.Add(setter);
                }
            }

            return methods.ToArray();
        }

        /// <summary>
        /// Generates StateTask properties for the proxy class.
        /// </summary>
        public MemberDeclarationSyntax[] GenerateProxyStateTaskProperties(
            IEnumerable<StatePropertyDescription> properties,
            Dictionary<ITypeParameterSymbol, string> typeParameterSubstitutions)
        {
            if (LibraryTypes.StateTask_1 is null)
            {
                return Array.Empty<MemberDeclarationSyntax>();
            }

            var result = new List<MemberDeclarationSyntax>();

            foreach (var prop in properties)
            {
                // StateTask<T> where T is the property type
                var stateTaskType = LibraryTypes.StateTask_1.Construct(prop.PropertyType);
                var typeSyntax = stateTaskType.ToTypeSyntax(typeParameterSubstitutions);

                // Create getter lambda: () => new ValueTask<T>(GetPropertyName())
                var getterLambda = CreateGetterLambda(prop);

                // Create setter lambda: v => new ValueTask(SetPropertyName(v)) or throw
                var setterLambda = CreateSetterLambda(prop);

                // new StateTask<T>(getter, setter)
                var newExpression = ObjectCreationExpression(typeSyntax)
                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                    {
                        Argument(getterLambda),
                        Argument(setterLambda)
                    })));

                // public StateTask<T> PropertyName => new StateTask<T>(getter, setter);
                var property = PropertyDeclaration(typeSyntax, Identifier(prop.PropertyName))
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithExpressionBody(ArrowExpressionClause(newExpression))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

                result.Add(property);
            }

            return result.ToArray();
        }

        private ExpressionSyntax CreateGetterLambda(StatePropertyDescription prop)
        {
            // GetMethodName() returns Task<T>, need to wrap in ValueTask<T>
            var getMethodCall = InvocationExpression(IdentifierName($"Get{prop.MethodName}"));

            // new ValueTask<T>(GetMethodName())
            var valueTaskType = GenericName(
                Identifier("global::System.Threading.Tasks.ValueTask"),
                TypeArgumentList(SingletonSeparatedList(prop.PropertyType.ToTypeSyntax())));

            var resultExpr = ObjectCreationExpression(valueTaskType)
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(getMethodCall))));

            // () => resultExpr
            return ParenthesizedLambdaExpression(resultExpr);
        }

        private ExpressionSyntax CreateSetterLambda(StatePropertyDescription prop)
        {
            if (!prop.CanSet)
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

            // SetMethodName(v) returns Task, need to wrap in ValueTask
            var setMethodCall = InvocationExpression(
                IdentifierName($"Set{prop.MethodName}"),
                ArgumentList(SingletonSeparatedList(Argument(IdentifierName("v")))));

            // new ValueTask(SetMethodName(v))
            var resultExpr = ObjectCreationExpression(ParseTypeName("global::System.Threading.Tasks.ValueTask"))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(setMethodCall))));

            // v => resultExpr
            return SimpleLambdaExpression(
                Parameter(Identifier("v")),
                resultExpr);
        }

        private INamedTypeSymbol? FindPrimaryGrainInterface(INamedTypeSymbol grainClass)
        {
            // Look for interfaces that inherit from IGrainWithXXXKey
            foreach (var iface in grainClass.AllInterfaces)
            {
                // Check if this interface or any of its base interfaces is a grain key interface
                if (IsGrainKeyInterface(iface) || iface.AllInterfaces.Any(IsGrainKeyInterface))
                {
                    // Return the first interface the grain class directly implements
                    // that is a grain interface (not the IGrainWithXXXKey itself)
                    foreach (var directIface in grainClass.Interfaces)
                    {
                        if (SymbolEqualityComparer.Default.Equals(directIface, iface))
                        {
                            return directIface;
                        }
                    }
                }
            }

            return null;
        }

        private bool IsGrainKeyInterface(INamedTypeSymbol iface)
        {
            // Check for IGrainWithXXXKey interfaces
            var name = iface.ToDisplayString();
            return name.StartsWith("Orleans.IGrainWith") && name.EndsWith("Key");
        }

        private bool HasAttribute(IPropertySymbol property, INamedTypeSymbol? attributeType)
        {
            if (attributeType is null) return false;

            return property.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
        }

        private string GetMethodName(IPropertySymbol property)
        {
            // Check for [State(MethodName = "...")]
            if (LibraryTypes.StateAttribute is not null)
            {
                var stateAttr = property.GetAttributes()
                    .FirstOrDefault(attr =>
                        SymbolEqualityComparer.Default.Equals(attr.AttributeClass, LibraryTypes.StateAttribute));

                if (stateAttr is not null)
                {
                    foreach (var arg in stateAttr.NamedArguments)
                    {
                        if (arg.Key == "MethodName" && arg.Value.Value is string methodName && !string.IsNullOrEmpty(methodName))
                        {
                            return methodName;
                        }
                    }
                }
            }

            return property.Name;
        }

        private bool GetCanSet(IPropertySymbol property)
        {
            // First check if property has a setter
            if (property.SetMethod is null)
            {
                return false;
            }

            // Check for [State(CanSet = false)]
            if (LibraryTypes.StateAttribute is not null)
            {
                var stateAttr = property.GetAttributes()
                    .FirstOrDefault(attr =>
                        SymbolEqualityComparer.Default.Equals(attr.AttributeClass, LibraryTypes.StateAttribute));

                if (stateAttr is not null)
                {
                    foreach (var arg in stateAttr.NamedArguments)
                    {
                        if (arg.Key == "CanSet" && arg.Value.Value is bool canSet)
                        {
                            return canSet;
                        }
                    }
                }
            }

            return true;
        }

        private bool IsPartialProperty(IPropertySymbol property)
        {
            // Check if the property is declared with 'partial' modifier
            foreach (var syntaxRef in property.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();
                if (syntax is PropertyDeclarationSyntax propSyntax)
                {
                    if (propSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
