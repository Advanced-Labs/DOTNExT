#nullable enable

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
                bool isPartial,
                bool isPersisted = false,
                string? stateFieldName = null,
                bool autoSave = false)
            {
                Property = property;
                GrainClass = grainClass;
                GrainInterface = grainInterface;
                MethodName = methodName;
                CanSet = canSet;
                IsPartial = isPartial;
                IsPersisted = isPersisted;
                StateFieldName = stateFieldName;
                AutoSave = autoSave;
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

            /// <summary>Whether this property maps to IPersistentState.</summary>
            public bool IsPersisted { get; }

            /// <summary>Name of the IPersistentState field (e.g., "_state").</summary>
            public string? StateFieldName { get; }

            /// <summary>Whether to auto-save after each set operation.</summary>
            public bool AutoSave { get; }

            /// <summary>The property type.</summary>
            public ITypeSymbol PropertyType => Property.Type;

            /// <summary>The property name.</summary>
            public string PropertyName => Property.Name;
        }

        /// <summary>
        /// Scans a grain class for state properties.
        /// Only processes grains that explicitly opt-in via partial properties or [State] attributes.
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

            // IMPORTANT: Only process grains that explicitly opt-in to state property generation.
            // A grain opts-in by having at least one:
            // 1. Partial property (requires codegen for backing fields), OR
            // 2. Property with [State] attribute
            // This prevents generating code for existing Orleans grains that don't use this feature.
            if (!HasOptedInToStateProperties(grainClass))
            {
                return properties;
            }

            // Detect IPersistentState fields in the grain class for persistence mapping
            var persistentStateFields = DetectPersistentStateFields(grainClass);

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

                // Get persistence settings from [State] attribute
                var (isPersisted, stateFieldName, autoSave) = GetPersistenceSettings(property, persistentStateFields);

                properties.Add(new StatePropertyDescription(
                    property,
                    grainClass,
                    grainInterface,
                    methodName,
                    canSet,
                    isPartial,
                    isPersisted,
                    stateFieldName,
                    autoSave));
            }

            return properties;
        }

        /// <summary>
        /// Checks if a grain class has opted-in to state property generation.
        /// A grain opts-in by having at least one partial property or [State] attribute.
        /// </summary>
        private bool HasOptedInToStateProperties(INamedTypeSymbol grainClass)
        {
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

                // Opt-in: Has [State] attribute
                if (HasAttribute(property, LibraryTypes.StateAttribute))
                    return true;

                // Opt-in: Is a partial property
                if (IsPartialProperty(property))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Detects IPersistentState&lt;T&gt; fields in a grain class.
        /// </summary>
        private Dictionary<string, IFieldSymbol> DetectPersistentStateFields(INamedTypeSymbol grainClass)
        {
            var fields = new Dictionary<string, IFieldSymbol>();

            if (LibraryTypes.IPersistentState_1 is null)
            {
                return fields;
            }

            foreach (var member in grainClass.GetMembers())
            {
                if (member is not IFieldSymbol field)
                    continue;

                // Check if field type is IPersistentState<T>
                if (field.Type is INamedTypeSymbol namedType &&
                    namedType.IsGenericType &&
                    namedType.OriginalDefinition is not null &&
                    SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, LibraryTypes.IPersistentState_1))
                {
                    fields[field.Name] = field;
                }
            }

            return fields;
        }

        /// <summary>
        /// Gets persistence settings from [State] attribute.
        /// </summary>
        private (bool IsPersisted, string? StateFieldName, bool AutoSave) GetPersistenceSettings(
            IPropertySymbol property,
            Dictionary<string, IFieldSymbol> persistentStateFields)
        {
            if (LibraryTypes.StateAttribute is null)
            {
                return (false, null, false);
            }

            var stateAttr = property.GetAttributes()
                .FirstOrDefault(attr =>
                    SymbolEqualityComparer.Default.Equals(attr.AttributeClass, LibraryTypes.StateAttribute));

            if (stateAttr is null)
            {
                return (false, null, false);
            }

            bool isPersisted = false;
            string? stateFieldName = null;
            bool autoSave = false;

            foreach (var arg in stateAttr.NamedArguments)
            {
                switch (arg.Key)
                {
                    case "Persisted" when arg.Value.Value is bool persisted:
                        isPersisted = persisted;
                        break;
                    case "StateProperty" when arg.Value.Value is string fieldName:
                        stateFieldName = fieldName;
                        break;
                    case "AutoSave" when arg.Value.Value is bool save:
                        autoSave = save;
                        break;
                }
            }

            // Validate: if Persisted is true, StateProperty must be set and the field must exist
            if (isPersisted)
            {
                if (string.IsNullOrEmpty(stateFieldName))
                {
                    // TODO: Could emit diagnostic here
                    isPersisted = false;
                }
                else if (!persistentStateFields.ContainsKey(stateFieldName!))
                {
                    // TODO: Could emit diagnostic here - field not found
                    isPersisted = false;
                }
            }

            return (isPersisted, stateFieldName, autoSave);
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
        /// Generates StateTask&lt;T&gt; property signatures for the partial grain interface.
        /// These allow client code to use property-style access: await grain.Name; await (grain.Name &lt;&lt; value);
        /// </summary>
        /// <remarks>
        /// For each state property, generates:
        /// <code>StateTask&lt;T&gt; PropertyName { get; }</code>
        /// The proxy class implements these properties with the actual invokable logic.
        /// </remarks>
        public MemberDeclarationSyntax[] GenerateInterfaceStateTaskProperties(
            IEnumerable<StatePropertyDescription> properties)
        {
            var props = new List<MemberDeclarationSyntax>();

            foreach (var prop in properties)
            {
                // Generate: StateTask<T> PropertyName { get; }
                var stateTaskType = GenericName(
                    Identifier("global::Orleans.StateTask"),
                    TypeArgumentList(SingletonSeparatedList(prop.PropertyType.ToTypeSyntax())));

                var propDecl = PropertyDeclaration(stateTaskType, prop.PropertyName)
                    .WithAccessorList(AccessorList(SingletonList(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))));

                props.Add(propDecl);
            }

            return props.ToArray();
        }

        /// <summary>
        /// Generates explicit interface implementations for StateTask properties on the grain class.
        /// These allow the grain class to implement the interface's StateTask properties with local access.
        /// </summary>
        /// <remarks>
        /// For each state property, generates:
        /// <code>
        /// StateTask&lt;T&gt; IInterface.PropertyName => new StateTask&lt;T&gt;(
        ///     () => new ValueTask&lt;T&gt;(PropertyName),
        ///     v => { PropertyName = v; return ValueTask.CompletedTask; }
        /// );
        /// </code>
        /// </remarks>
        public MemberDeclarationSyntax[] GenerateGrainStateTaskPropertyImplementations(
            IEnumerable<StatePropertyDescription> properties)
        {
            var props = new List<MemberDeclarationSyntax>();

            foreach (var prop in properties)
            {
                // Generate: StateTask<T> IInterface.PropertyName => new StateTask<T>(getter, setter);
                var stateTaskType = GenericName(
                    Identifier("global::Orleans.StateTask"),
                    TypeArgumentList(SingletonSeparatedList(prop.PropertyType.ToTypeSyntax())));

                // Getter lambda: () => new ValueTask<T>(PropertyName)
                var valueTaskType = GenericName(
                    Identifier("global::System.Threading.Tasks.ValueTask"),
                    TypeArgumentList(SingletonSeparatedList(prop.PropertyType.ToTypeSyntax())));
                var getterLambda = ParenthesizedLambdaExpression(
                    ObjectCreationExpression(valueTaskType)
                        .WithArgumentList(ArgumentList(SingletonSeparatedList(
                            Argument(IdentifierName(prop.PropertyName))))));

                // Setter lambda: v => { PropertyName = v; return ValueTask.CompletedTask; }
                ExpressionSyntax setterLambda;
                if (prop.CanSet)
                {
                    setterLambda = SimpleLambdaExpression(
                        Parameter(Identifier("v")),
                        Block(
                            ExpressionStatement(AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(prop.PropertyName),
                                IdentifierName("v"))),
                            ReturnStatement(MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ParseTypeName("global::System.Threading.Tasks.ValueTask"),
                                IdentifierName("CompletedTask")))));
                }
                else
                {
                    // No setter - return lambda that throws
                    setterLambda = SimpleLambdaExpression(
                        Parameter(Identifier("v")),
                        ThrowExpression(
                            ObjectCreationExpression(ParseTypeName("global::System.NotSupportedException"))
                                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                    Argument(LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        Literal($"Property '{prop.PropertyName}' is read-only"))))))));
                }

                // new StateTask<T>(getterLambda, setterLambda)
                var newStateTask = ObjectCreationExpression(stateTaskType)
                    .WithArgumentList(ArgumentList(SeparatedList(new[] {
                        Argument(getterLambda),
                        Argument(setterLambda)
                    })));

                var propDecl = PropertyDeclaration(stateTaskType, prop.PropertyName)
                    .WithExplicitInterfaceSpecifier(
                        ExplicitInterfaceSpecifier(prop.GrainInterface.ToNameSyntax()))
                    .WithExpressionBody(ArrowExpressionClause(newStateTask))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

                props.Add(propDecl);
            }

            return props.ToArray();
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
        /// Generates backing fields and property implementations for partial properties.
        /// These are added to the partial grain class to complete the partial property declarations.
        /// </summary>
        /// <remarks>
        /// For a non-persisted partial property like:
        /// <code>public partial string Name { get; set; }</code>
        ///
        /// This generates:
        /// <code>
        /// private string _name_backing = default!;
        /// public partial string Name
        /// {
        ///     get => _name_backing;
        ///     set => _name_backing = value;
        /// }
        /// </code>
        ///
        /// For a persisted partial property like:
        /// <code>[State(Persisted = true, StateProperty = nameof(_state))]
        /// public partial int Score { get; set; }</code>
        ///
        /// This generates:
        /// <code>
        /// public partial int Score
        /// {
        ///     get => _state.State.Score;
        ///     set => _state.State.Score = value;
        /// }
        /// </code>
        ///
        /// With AutoSave = true:
        /// <code>
        /// public partial int Score
        /// {
        ///     get => _state.State.Score;
        ///     set { _state.State.Score = value; _ = _state.WriteStateAsync(); }
        /// }
        /// </code>
        /// </remarks>
        public MemberDeclarationSyntax[] GeneratePartialPropertyImplementations(
            IEnumerable<StatePropertyDescription> properties)
        {
            var members = new List<MemberDeclarationSyntax>();
            var partialProperties = properties.Where(p => p.IsPartial).ToList();

            if (partialProperties.Count == 0)
            {
                return Array.Empty<MemberDeclarationSyntax>();
            }

            foreach (var prop in partialProperties)
            {
                if (prop.IsPersisted && !string.IsNullOrEmpty(prop.StateFieldName))
                {
                    // Persisted property: no backing field, access state directly
                    var propertyImpl = GeneratePersistedPropertyImpl(prop);
                    members.Add(propertyImpl);
                }
                else
                {
                    // Non-persisted property: generate backing field and simple accessors
                    var backingFieldName = $"_{ToCamelCase(prop.PropertyName)}_backing";
                    var backingField = GenerateBackingField(prop, backingFieldName);
                    members.Add(backingField);

                    var propertyImpl = GeneratePartialPropertyImpl(prop, backingFieldName);
                    members.Add(propertyImpl);
                }
            }

            return members.ToArray();
        }

        private FieldDeclarationSyntax GenerateBackingField(StatePropertyDescription prop, string fieldName)
        {
            var propertyType = prop.PropertyType.ToTypeSyntax();

            // Create the variable declarator with optional initializer
            VariableDeclaratorSyntax declarator;

            // For reference types, initialize with default! to avoid nullable warnings
            // For value types, just use default
            if (prop.PropertyType.IsReferenceType)
            {
                // default!
                declarator = VariableDeclarator(Identifier(fieldName))
                    .WithInitializer(EqualsValueClause(
                        PostfixUnaryExpression(
                            SyntaxKind.SuppressNullableWarningExpression,
                            LiteralExpression(SyntaxKind.DefaultLiteralExpression))));
            }
            else
            {
                // No initializer needed for value types - they default to 0/false/etc
                declarator = VariableDeclarator(Identifier(fieldName));
            }

            return FieldDeclaration(
                VariableDeclaration(propertyType)
                    .WithVariables(SingletonSeparatedList(declarator)))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)));
        }

        private PropertyDeclarationSyntax GeneratePartialPropertyImpl(StatePropertyDescription prop, string backingFieldName)
        {
            var propertyType = prop.PropertyType.ToTypeSyntax();

            // Getter: get => _fieldName;
            var getter = AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithExpressionBody(ArrowExpressionClause(IdentifierName(backingFieldName)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            var accessors = new List<AccessorDeclarationSyntax> { getter };

            // Setter (if property has one): set => _fieldName = value;
            if (prop.Property.SetMethod is not null)
            {
                var setter = AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithExpressionBody(ArrowExpressionClause(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(backingFieldName),
                            IdentifierName("value"))))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
                accessors.Add(setter);
            }

            return PropertyDeclaration(propertyType, Identifier(prop.PropertyName))
                .WithModifiers(TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.PartialKeyword)))
                .WithAccessorList(AccessorList(List(accessors)));
        }

        /// <summary>
        /// Generates a partial property implementation that maps to IPersistentState.
        /// </summary>
        /// <remarks>
        /// For a property with [State(Persisted = true, StateProperty = nameof(_state))]:
        /// - Getter accesses: _state.State.PropertyName
        /// - Setter assigns: _state.State.PropertyName = value
        /// - If AutoSave = true: also calls _ = _state.WriteStateAsync() (fire-and-forget)
        /// </remarks>
        private PropertyDeclarationSyntax GeneratePersistedPropertyImpl(StatePropertyDescription prop)
        {
            var propertyType = prop.PropertyType.ToTypeSyntax();
            var stateFieldName = prop.StateFieldName!;

            // Build: _stateField.State.PropertyName
            var stateAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(stateFieldName),
                    IdentifierName("State")),
                IdentifierName(prop.PropertyName));

            // Getter: get => _stateField.State.PropertyName;
            var getter = AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithExpressionBody(ArrowExpressionClause(stateAccess))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            var accessors = new List<AccessorDeclarationSyntax> { getter };

            // Setter (if property has one)
            if (prop.Property.SetMethod is not null)
            {
                AccessorDeclarationSyntax setter;

                if (prop.AutoSave)
                {
                    // With AutoSave: set { _stateField.State.PropertyName = value; _ = _stateField.WriteStateAsync(); }
                    var assignStatement = ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            stateAccess,
                            IdentifierName("value")));

                    // _ = _stateField.WriteStateAsync();
                    var writeStateCall = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(stateFieldName),
                            IdentifierName("WriteStateAsync")));

                    var discardAssignment = ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName("_"),
                            writeStateCall));

                    setter = AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithBody(Block(assignStatement, discardAssignment));
                }
                else
                {
                    // Without AutoSave: set => _stateField.State.PropertyName = value;
                    setter = AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                        .WithExpressionBody(ArrowExpressionClause(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                stateAccess,
                                IdentifierName("value"))))
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
                }

                accessors.Add(setter);
            }

            return PropertyDeclaration(propertyType, Identifier(prop.PropertyName))
                .WithModifiers(TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.PartialKeyword)))
                .WithAccessorList(AccessorList(List(accessors)));
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // If first char is already lowercase, return as-is
            if (char.IsLower(name[0]))
                return name;

            // Convert first character to lowercase
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
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
            // The proxy method is an explicit interface implementation, so we need to cast 'this'
            // to the interface type to call it: ((IInterface)this).GetMethodName()
            var interfaceTypeSyntax = prop.GrainInterface.ToTypeSyntax();
            var castThis = ParenthesizedExpression(
                CastExpression(interfaceTypeSyntax, ThisExpression()));
            var getMethodCall = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    castThis,
                    IdentifierName($"Get{prop.MethodName}")));

            // new ValueTask<T>(((IInterface)this).GetMethodName())
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

            // The proxy method is an explicit interface implementation, so we need to cast 'this'
            // to the interface type to call it: ((IInterface)this).SetMethodName(v)
            var interfaceTypeSyntax = prop.GrainInterface.ToTypeSyntax();
            var castThis = ParenthesizedExpression(
                CastExpression(interfaceTypeSyntax, ThisExpression()));
            var setMethodCall = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    castThis,
                    IdentifierName($"Set{prop.MethodName}")),
                ArgumentList(SingletonSeparatedList(Argument(IdentifierName("v")))));

            // new ValueTask(((IInterface)this).SetMethodName(v))
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

        // ═══════════════════════════════════════════════════════════════════════════
        // STATE PROPERTY PROXY METHODS
        // Generates the GetX/SetX methods on the proxy class that make RPC calls
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Generates proxy method implementations for state property accessor methods (GetX/SetX).
        /// These are explicit interface implementations that create invokables and call InvokeAsync.
        /// </summary>
        /// <param name="properties">The state properties to generate methods for.</param>
        /// <param name="typeParameterSubstitutions">Type parameter substitutions for generic interfaces.</param>
        /// <param name="proxyBaseType">The proxy base type for invokable lookup.</param>
        /// <returns>Method declarations for GetX and SetX methods, plus their invokable class descriptions.</returns>
        public (MemberDeclarationSyntax[] ProxyMethods, StatePropertyInvokableInfo[] InvokableInfos) GenerateStatePropertyProxyMethodsAndInvokables(
            IEnumerable<StatePropertyDescription> properties,
            Dictionary<ITypeParameterSymbol, string> typeParameterSubstitutions,
            INamedTypeSymbol proxyBaseType)
        {
            var proxyMethods = new List<MemberDeclarationSyntax>();
            var invokableInfos = new List<StatePropertyInvokableInfo>();

            foreach (var prop in properties)
            {
                var interfaceType = prop.GrainInterface;

                // Generate GetX method
                var (getMethod, getInvokable) = GenerateGetterProxyMethod(prop, typeParameterSubstitutions, proxyBaseType, interfaceType);
                proxyMethods.Add(getMethod);
                invokableInfos.Add(getInvokable);

                // Generate SetX method (if property has setter)
                if (prop.CanSet)
                {
                    var (setMethod, setInvokable) = GenerateSetterProxyMethod(prop, typeParameterSubstitutions, proxyBaseType, interfaceType);
                    proxyMethods.Add(setMethod);
                    invokableInfos.Add(setInvokable);
                }
            }

            return (proxyMethods.ToArray(), invokableInfos.ToArray());
        }

        /// <summary>
        /// Information about a generated state property invokable.
        /// </summary>
        public class StatePropertyInvokableInfo
        {
            public string ClassName { get; set; } = "";
            public string Namespace { get; set; } = "";
            public ClassDeclarationSyntax ClassDeclaration { get; set; } = null!;
            public INamedTypeSymbol InterfaceType { get; set; } = null!;
            public string MethodId { get; set; } = "";
            public bool IsGetter { get; set; }
            public ITypeSymbol? PropertyType { get; set; }
        }

        private (MethodDeclarationSyntax Method, StatePropertyInvokableInfo InvokableInfo) GenerateGetterProxyMethod(
            StatePropertyDescription prop,
            Dictionary<ITypeParameterSymbol, string> typeParameterSubstitutions,
            INamedTypeSymbol proxyBaseType,
            INamedTypeSymbol interfaceType)
        {
            var methodName = $"Get{prop.MethodName}";
            var returnType = GenericName(
                Identifier("global::System.Threading.Tasks.Task"),
                TypeArgumentList(SingletonSeparatedList(prop.PropertyType.ToTypeSyntax(typeParameterSubstitutions))));

            // Generate invokable class name
            var invokableClassName = $"Invokable_{interfaceType.Name}_{methodName}";
            var invokableNs = CodeGenerator.GetGeneratedNamespaceName(interfaceType);
            var methodId = CreateMethodId(interfaceType, methodName, Array.Empty<ITypeSymbol>());

            // Generate invokable class
            var invokableClass = GenerateGetterInvokable(prop, invokableClassName, interfaceType, typeParameterSubstitutions, methodId);

            // Generate proxy method body:
            // var request = new InvokableType();
            // return base.InvokeAsync<T>(request).AsTask();
            var statements = new List<StatementSyntax>();

            // var request = new InvokableType();
            var invokableTypeSyntax = QualifiedName(ParseName(invokableNs), IdentifierName(invokableClassName));
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(ParseTypeName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator("request")
                            .WithInitializer(EqualsValueClause(
                                ObjectCreationExpression(invokableTypeSyntax)
                                    .WithArgumentList(ArgumentList())))))));

            // return base.InvokeAsync<T>(request).AsTask();
            var invokeExpr = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    BaseExpression(),
                    GenericName("InvokeAsync")
                        .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(
                            prop.PropertyType.ToTypeSyntax(typeParameterSubstitutions))))));
            invokeExpr = invokeExpr.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("request")))));
            var asTaskExpr = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, invokeExpr, IdentifierName("AsTask")));
            statements.Add(ReturnStatement(asTaskExpr));

            var method = MethodDeclaration(returnType, methodName)
                .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(interfaceType.ToNameSyntax()))
                .WithBody(Block(statements));

            var invokableInfo = new StatePropertyInvokableInfo
            {
                ClassName = invokableClassName,
                Namespace = invokableNs,
                ClassDeclaration = invokableClass,
                InterfaceType = interfaceType,
                MethodId = methodId,
                IsGetter = true,
                PropertyType = prop.PropertyType
            };

            return (method, invokableInfo);
        }

        private (MethodDeclarationSyntax Method, StatePropertyInvokableInfo InvokableInfo) GenerateSetterProxyMethod(
            StatePropertyDescription prop,
            Dictionary<ITypeParameterSymbol, string> typeParameterSubstitutions,
            INamedTypeSymbol proxyBaseType,
            INamedTypeSymbol interfaceType)
        {
            var methodName = $"Set{prop.MethodName}";
            var returnType = ParseTypeName("global::System.Threading.Tasks.Task");
            var paramType = prop.PropertyType.ToTypeSyntax(typeParameterSubstitutions);

            // Generate invokable class name
            var invokableClassName = $"Invokable_{interfaceType.Name}_{methodName}";
            var invokableNs = CodeGenerator.GetGeneratedNamespaceName(interfaceType);
            var methodId = CreateMethodId(interfaceType, methodName, new[] { prop.PropertyType });

            // Generate invokable class
            var invokableClass = GenerateSetterInvokable(prop, invokableClassName, interfaceType, typeParameterSubstitutions, methodId);

            // Generate proxy method body:
            // var request = new InvokableType();
            // request.arg0 = arg0;
            // return base.InvokeAsync(request).AsTask();
            var statements = new List<StatementSyntax>();

            // var request = new InvokableType();
            var invokableTypeSyntax = QualifiedName(ParseName(invokableNs), IdentifierName(invokableClassName));
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(ParseTypeName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator("request")
                            .WithInitializer(EqualsValueClause(
                                ObjectCreationExpression(invokableTypeSyntax)
                                    .WithArgumentList(ArgumentList())))))));

            // request.arg0 = arg0;
            statements.Add(ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("request"), IdentifierName("arg0")),
                    IdentifierName("arg0"))));

            // return base.InvokeAsync(request).AsTask();
            var invokeExpr = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, BaseExpression(), IdentifierName("InvokeAsync")));
            invokeExpr = invokeExpr.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("request")))));
            var asTaskExpr = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, invokeExpr, IdentifierName("AsTask")));
            statements.Add(ReturnStatement(asTaskExpr));

            var method = MethodDeclaration(returnType, methodName)
                .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(interfaceType.ToNameSyntax()))
                .WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("arg0")).WithType(paramType))))
                .WithBody(Block(statements));

            var invokableInfo = new StatePropertyInvokableInfo
            {
                ClassName = invokableClassName,
                Namespace = invokableNs,
                ClassDeclaration = invokableClass,
                InterfaceType = interfaceType,
                MethodId = methodId,
                IsGetter = false,
                PropertyType = prop.PropertyType
            };

            return (method, invokableInfo);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // STATE PROPERTY INVOKABLES
        // Generates the invokable request classes for state property methods
        // ═══════════════════════════════════════════════════════════════════════════

        private ClassDeclarationSyntax GenerateGetterInvokable(
            StatePropertyDescription prop,
            string className,
            INamedTypeSymbol interfaceType,
            Dictionary<ITypeParameterSymbol, string> typeParameterSubstitutions,
            string methodId)
        {
            // Generate a class that derives from TaskRequest<T>
            // internal sealed class Invokable_IFoo_GetBar : TaskRequest<BarType>
            // {
            //     private IFoo _target;
            //     public override void SetTarget(ITargetHolder holder) => _target = holder.GetTarget<IFoo>();
            //     public override object GetTarget() => _target;
            //     public override Task<BarType> Invoke() => _target.GetBar();
            //     protected override string GetMethodName() => "GetBar";
            //     protected override string GetInterfaceName() => "IFoo";
            //     protected override Type GetInterfaceType() => typeof(IFoo);
            //     protected override int GetArgumentCount() => 0;
            // }

            var propertyTypeSyntax = prop.PropertyType.ToTypeSyntax(typeParameterSubstitutions);
            var interfaceTypeSyntax = interfaceType.ToTypeSyntax();

            // Base type: TaskRequest<T> from Orleans.Runtime namespace
            var baseType = GenericName(
                Identifier("global::Orleans.Runtime.TaskRequest"),
                TypeArgumentList(SingletonSeparatedList(propertyTypeSyntax)));

            var members = new List<MemberDeclarationSyntax>();

            // Field: private IFoo _target;
            members.Add(FieldDeclaration(
                VariableDeclaration(interfaceTypeSyntax)
                    .WithVariables(SingletonSeparatedList(VariableDeclarator("_target"))))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword))));

            // SetTarget method
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "SetTarget")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("holder"))
                        .WithType(ParseTypeName("global::Orleans.Serialization.Invocation.ITargetHolder")))))
                .WithExpressionBody(ArrowExpressionClause(
                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName("_target"),
                        InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("holder"),
                                GenericName("GetTarget").WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(interfaceTypeSyntax))))))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetTarget method
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.ObjectKeyword)), "GetTarget")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(IdentifierName("_target")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // InvokeInner method: protected override Task<T> InvokeInner() => _target.GetPropertyName();
            var invokeReturnType = GenericName(
                Identifier("global::System.Threading.Tasks.Task"),
                TypeArgumentList(SingletonSeparatedList(propertyTypeSyntax)));
            members.Add(MethodDeclaration(invokeReturnType, "InvokeInner")
                .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("_target"),
                            IdentifierName($"Get{prop.MethodName}")))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetMethodName
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)), "GetMethodName")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"Get{prop.MethodName}"))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetInterfaceName
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)), "GetInterfaceName")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(interfaceType.Name))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetInterfaceType
            members.Add(MethodDeclaration(ParseTypeName("global::System.Type"), "GetInterfaceType")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(TypeOfExpression(interfaceTypeSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetArgumentCount
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.IntKeyword)), "GetArgumentCount")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetActivityName: public override string GetActivityName() => "IFoo/GetBar";
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)), "GetActivityName")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"{interfaceType.Name}/Get{prop.MethodName}"))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetMethod: public override MethodInfo GetMethod() => OrleansGeneratedCodeHelper.GetMethodInfoOrDefault(typeof(IFoo), "GetBar", Type.EmptyTypes, Type.EmptyTypes);
            members.Add(MethodDeclaration(ParseTypeName("global::System.Reflection.MethodInfo"), "GetMethod")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    InvocationExpression(ParseName("OrleansGeneratedCodeHelper.GetMethodInfoOrDefault"))
                        .WithArgumentList(ArgumentList(SeparatedList(new[] {
                            Argument(TypeOfExpression(interfaceTypeSyntax)),
                            Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"Get{prop.MethodName}"))),
                            Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                ParseTypeName("global::System.Type"),
                                IdentifierName("EmptyTypes"))),
                            Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                ParseTypeName("global::System.Type"),
                                IdentifierName("EmptyTypes")))
                        })))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // Dispose: public override void Dispose() { }
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Dispose")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithBody(Block()));

            // Build the class
            var classDecl = ClassDeclaration(className)
                .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.SealedKeyword)))
                .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(baseType))))
                .AddAttributeLists(CodeGenerator.GetGeneratedCodeAttributes())
                .AddAttributeLists(AttributeList(SingletonSeparatedList(
                    Attribute(ParseName("global::Orleans.GenerateSerializerAttribute")))))
                .AddAttributeLists(GenerateCompoundTypeAliasAttribute(interfaceType, methodId))
                .AddMembers(members.ToArray());

            return classDecl;
        }

        private ClassDeclarationSyntax GenerateSetterInvokable(
            StatePropertyDescription prop,
            string className,
            INamedTypeSymbol interfaceType,
            Dictionary<ITypeParameterSymbol, string> typeParameterSubstitutions,
            string methodId)
        {
            // Generate a class that derives from TaskRequest (non-generic)
            // internal sealed class Invokable_IFoo_SetBar : TaskRequest
            // {
            //     [Id(0)]
            //     public BarType arg0;
            //     private IFoo _target;
            //     public override void SetTarget(ITargetHolder holder) => _target = holder.GetTarget<IFoo>();
            //     public override object GetTarget() => _target;
            //     public override Task Invoke() => _target.SetBar(arg0);
            //     protected override string GetMethodName() => "SetBar";
            //     protected override string GetInterfaceName() => "IFoo";
            //     protected override Type GetInterfaceType() => typeof(IFoo);
            //     protected override int GetArgumentCount() => 1;
            //     public override object GetArgument(int index) => index == 0 ? arg0 : OrleansGeneratedCodeHelper.InvokableThrowArgumentOutOfRange(index, 0);
            //     public override void SetArgument(int index, object value) { if (index == 0) arg0 = (BarType)value; else OrleansGeneratedCodeHelper.InvokableThrowArgumentOutOfRange(index, 0); }
            // }

            var propertyTypeSyntax = prop.PropertyType.ToTypeSyntax(typeParameterSubstitutions);
            var interfaceTypeSyntax = interfaceType.ToTypeSyntax();

            // Base type: TaskRequest (non-generic) from Orleans.Runtime namespace
            var baseType = ParseTypeName("global::Orleans.Runtime.TaskRequest");

            var members = new List<MemberDeclarationSyntax>();

            // Field with [Id(0)]: public BarType arg0;
            members.Add(FieldDeclaration(
                VariableDeclaration(propertyTypeSyntax)
                    .WithVariables(SingletonSeparatedList(VariableDeclarator("arg0"))))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .AddAttributeLists(AttributeList(SingletonSeparatedList(
                    Attribute(ParseName("global::Orleans.IdAttribute"))
                        .AddArgumentListArguments(AttributeArgument(
                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))))))));

            // Field: private IFoo _target;
            members.Add(FieldDeclaration(
                VariableDeclaration(interfaceTypeSyntax)
                    .WithVariables(SingletonSeparatedList(VariableDeclarator("_target"))))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword))));

            // SetTarget method
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "SetTarget")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("holder"))
                        .WithType(ParseTypeName("global::Orleans.Serialization.Invocation.ITargetHolder")))))
                .WithExpressionBody(ArrowExpressionClause(
                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName("_target"),
                        InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("holder"),
                                GenericName("GetTarget").WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(interfaceTypeSyntax))))))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetTarget method
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.ObjectKeyword)), "GetTarget")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(IdentifierName("_target")))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // InvokeInner method: protected override Task InvokeInner() => _target.SetPropertyName(arg0);
            var invokeReturnType = ParseTypeName("global::System.Threading.Tasks.Task");
            members.Add(MethodDeclaration(invokeReturnType, "InvokeInner")
                .WithModifiers(TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("_target"),
                            IdentifierName($"Set{prop.MethodName}")))
                        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(IdentifierName("arg0")))))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetMethodName
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)), "GetMethodName")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"Set{prop.MethodName}"))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetInterfaceName
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)), "GetInterfaceName")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(interfaceType.Name))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetInterfaceType
            members.Add(MethodDeclaration(ParseTypeName("global::System.Type"), "GetInterfaceType")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(TypeOfExpression(interfaceTypeSyntax)))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetArgumentCount
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.IntKeyword)), "GetArgumentCount")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetArgument
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.ObjectKeyword)), "GetArgument")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("index")).WithType(PredefinedType(Token(SyntaxKind.IntKeyword))))))
                .WithExpressionBody(ArrowExpressionClause(
                    ConditionalExpression(
                        BinaryExpression(SyntaxKind.EqualsExpression, IdentifierName("index"), LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))),
                        IdentifierName("arg0"),
                        InvocationExpression(ParseName("OrleansGeneratedCodeHelper.InvokableThrowArgumentOutOfRange"))
                            .WithArgumentList(ArgumentList(SeparatedList(new[] {
                                Argument(IdentifierName("index")),
                                Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)))
                            }))))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // SetArgument
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "SetArgument")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithParameterList(ParameterList(SeparatedList(new[] {
                    Parameter(Identifier("index")).WithType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                    Parameter(Identifier("value")).WithType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))
                })))
                .WithBody(Block(
                    IfStatement(
                        BinaryExpression(SyntaxKind.EqualsExpression, IdentifierName("index"), LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))),
                        ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName("arg0"),
                            CastExpression(propertyTypeSyntax, IdentifierName("value")))),
                        ElseClause(ExpressionStatement(
                            InvocationExpression(ParseName("OrleansGeneratedCodeHelper.InvokableThrowArgumentOutOfRange"))
                                .WithArgumentList(ArgumentList(SeparatedList(new[] {
                                    Argument(IdentifierName("index")),
                                    Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)))
                                })))))))));

            // GetActivityName: public override string GetActivityName() => "IFoo/SetBar";
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.StringKeyword)), "GetActivityName")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"{interfaceType.Name}/Set{prop.MethodName}"))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // GetMethod: public override MethodInfo GetMethod() => OrleansGeneratedCodeHelper.GetMethodInfoOrDefault(typeof(IFoo), "SetBar", new Type[] { typeof(T) }, Type.EmptyTypes);
            members.Add(MethodDeclaration(ParseTypeName("global::System.Reflection.MethodInfo"), "GetMethod")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithExpressionBody(ArrowExpressionClause(
                    InvocationExpression(ParseName("OrleansGeneratedCodeHelper.GetMethodInfoOrDefault"))
                        .WithArgumentList(ArgumentList(SeparatedList(new[] {
                            Argument(TypeOfExpression(interfaceTypeSyntax)),
                            Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal($"Set{prop.MethodName}"))),
                            Argument(ArrayCreationExpression(
                                ArrayType(ParseTypeName("global::System.Type"))
                                    .WithRankSpecifiers(SingletonList(ArrayRankSpecifier())))
                                .WithInitializer(InitializerExpression(SyntaxKind.ArrayInitializerExpression,
                                    SingletonSeparatedList<ExpressionSyntax>(TypeOfExpression(propertyTypeSyntax))))),
                            Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                ParseTypeName("global::System.Type"),
                                IdentifierName("EmptyTypes")))
                        })))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

            // Dispose: public override void Dispose() { }
            members.Add(MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Dispose")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
                .WithBody(Block()));

            // Build the class
            var classDecl = ClassDeclaration(className)
                .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.SealedKeyword)))
                .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(baseType))))
                .AddAttributeLists(CodeGenerator.GetGeneratedCodeAttributes())
                .AddAttributeLists(AttributeList(SingletonSeparatedList(
                    Attribute(ParseName("global::Orleans.GenerateSerializerAttribute")))))
                .AddAttributeLists(GenerateCompoundTypeAliasAttribute(interfaceType, methodId))
                .AddMembers(members.ToArray());

            return classDecl;
        }

        private string CreateMethodId(INamedTypeSymbol interfaceType, string methodName, ITypeSymbol[] parameterTypes)
        {
            // Create a unique method identifier similar to how InvokableGenerator does it
            var sb = new System.Text.StringBuilder();
            sb.Append(interfaceType.ToDisplayString());
            sb.Append('.');
            sb.Append(methodName);
            sb.Append('(');
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(parameterTypes[i].ToDisplayString());
            }
            sb.Append(')');
            var methodSignature = sb.ToString();
            var hash = Orleans.CodeGenerator.Hashing.XxHash32.Hash(System.Text.Encoding.UTF8.GetBytes(methodSignature));
            return Orleans.CodeGenerator.Hashing.HexConverter.ToString(hash);
        }

        private AttributeListSyntax GenerateCompoundTypeAliasAttribute(INamedTypeSymbol interfaceType, string methodId)
        {
            // [CompoundTypeAlias(new object[] { "inv", typeof(GrainReference), typeof(IFoo), "methodId" })]
            return AttributeList(SingletonSeparatedList(
                Attribute(ParseName("global::Orleans.CompoundTypeAliasAttribute"))
                    .AddArgumentListArguments(
                        AttributeArgument(
                            ArrayCreationExpression(
                                ArrayType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))
                                    .WithRankSpecifiers(SingletonList(ArrayRankSpecifier())))
                                .WithInitializer(InitializerExpression(SyntaxKind.ArrayInitializerExpression,
                                    SeparatedList<ExpressionSyntax>(new ExpressionSyntax[] {
                                        LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("inv")),
                                        TypeOfExpression(ParseTypeName("global::Orleans.Runtime.GrainReference")),
                                        TypeOfExpression(interfaceType.ToTypeSyntax()),
                                        LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(methodId))
                                    })))))));
        }
    }
}
