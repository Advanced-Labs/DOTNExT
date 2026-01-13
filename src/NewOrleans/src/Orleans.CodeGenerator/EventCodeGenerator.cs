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
    /// Generates code for NewOrleans Events on grains.
    ///
    /// This generator scans grain classes for public events (EventHandler, EventHandler&lt;T&gt;)
    /// and generates:
    /// 1. Interface event declarations and subscription method signatures
    /// 2. Grain infrastructure (stream fields, lifecycle hooks, bridge handlers)
    /// 3. Proxy local event implementation and subscription methods
    /// </summary>
    internal class EventCodeGenerator
    {
        private readonly CodeGenerator _codeGenerator;

        public EventCodeGenerator(CodeGenerator codeGenerator)
        {
            _codeGenerator = codeGenerator;
        }

        private LibraryTypes LibraryTypes => _codeGenerator.LibraryTypes;

        /// <summary>
        /// Describes an event detected on a grain class.
        /// </summary>
        public class EventDescription
        {
            public EventDescription(
                IEventSymbol eventSymbol,
                INamedTypeSymbol grainClass,
                INamedTypeSymbol grainInterface,
                ITypeSymbol? payloadType,
                GrainKeyType keyType)
            {
                Event = eventSymbol;
                GrainClass = grainClass;
                GrainInterface = grainInterface;
                PayloadType = payloadType;
                KeyType = keyType;
            }

            /// <summary>The event symbol on the grain class.</summary>
            public IEventSymbol Event { get; }

            /// <summary>The grain class containing the event.</summary>
            public INamedTypeSymbol GrainClass { get; }

            /// <summary>The grain interface to add event/methods to.</summary>
            public INamedTypeSymbol GrainInterface { get; }

            /// <summary>The event payload type (T in EventHandler&lt;T&gt;), or null for EventHandler.</summary>
            public ITypeSymbol? PayloadType { get; }

            /// <summary>The grain key type for stream ID generation.</summary>
            public GrainKeyType KeyType { get; }

            /// <summary>The event name.</summary>
            public string EventName => Event.Name;

            /// <summary>Whether this is a typed event (EventHandler&lt;T&gt;) vs untyped (EventHandler).</summary>
            public bool HasPayload => PayloadType is not null;

            /// <summary>The stream namespace for this event (e.g., "IPlayerGrain.ChatMessage").</summary>
            public string StreamNamespace => $"{GrainInterface.Name}.{EventName}";
        }

        /// <summary>
        /// Grain key types supported by Orleans.
        /// </summary>
        public enum GrainKeyType
        {
            String,
            Guid,
            Integer,
            GuidCompound,
            IntegerCompound
        }

        /// <summary>
        /// Scans a grain class for events that should be distributed.
        /// </summary>
        public List<EventDescription> ScanGrainClass(INamedTypeSymbol grainClass)
        {
            if (!LibraryTypes.SupportsEvents)
            {
                return new List<EventDescription>();
            }

            var events = new List<EventDescription>();

            // Find the primary grain interface
            var grainInterface = FindPrimaryGrainInterface(grainClass);
            if (grainInterface is null)
            {
                return events;
            }

            // Determine the grain key type
            var keyType = GetGrainKeyType(grainClass);
            if (keyType is null)
            {
                return events;
            }

            // Scan public events on the grain class
            foreach (var member in grainClass.GetMembers())
            {
                if (member is not IEventSymbol eventSymbol)
                    continue;

                // Skip non-public events
                if (eventSymbol.DeclaredAccessibility != Accessibility.Public)
                    continue;

                // Skip events with [NotEvent] attribute
                if (HasAttribute(eventSymbol, LibraryTypes.NotEventAttribute))
                    continue;

                // Check if the event type is EventHandler or EventHandler<T>
                var payloadType = GetEventPayloadType(eventSymbol);
                if (payloadType is null && !IsPlainEventHandler(eventSymbol))
                {
                    // Unsupported event handler type - skip
                    continue;
                }

                events.Add(new EventDescription(
                    eventSymbol,
                    grainClass,
                    grainInterface,
                    payloadType,
                    keyType.Value));
            }

            return events;
        }

        /// <summary>
        /// Generates interface members for events (event declarations + subscription methods).
        /// </summary>
        public MemberDeclarationSyntax[] GenerateInterfaceMembers(IEnumerable<EventDescription> events)
        {
            var members = new List<MemberDeclarationSyntax>();

            foreach (var evt in events)
            {
                // Generate: event EventHandler<T>? EventName;
                var eventDecl = GenerateInterfaceEventDeclaration(evt);
                members.Add(eventDecl);

                // Generate: Task<IEventSubscription<T>> SubscribeToEventNameAsync();
                var subscribeMethod = GenerateInterfaceSubscribeMethod(evt, withHandler: false);
                members.Add(subscribeMethod);

                // Generate: Task<IEventSubscription<T>> SubscribeToEventNameAsync(Func<T, Task> handler);
                var subscribeMethodWithHandler = GenerateInterfaceSubscribeMethod(evt, withHandler: true);
                members.Add(subscribeMethodWithHandler);
            }

            return members.ToArray();
        }

        /// <summary>
        /// Generates grain class members for events (stream fields, lifecycle, bridge handlers, subscription implementations).
        /// </summary>
        public MemberDeclarationSyntax[] GenerateGrainMembers(IEnumerable<EventDescription> events)
        {
            var eventsList = events.ToList();
            if (eventsList.Count == 0)
            {
                return Array.Empty<MemberDeclarationSyntax>();
            }

            var members = new List<MemberDeclarationSyntax>();

            // Generate stream fields for each event
            foreach (var evt in eventsList)
            {
                members.Add(GenerateStreamField(evt));
                members.Add(GenerateBridgeHandlerField(evt));
            }

            // Generate ILifecycleParticipant implementation
            members.Add(GenerateParticipateMethod(eventsList));

            // Generate __InitializeNewOrleansEvents method
            members.Add(GenerateInitializeMethod(eventsList));

            // Generate __CleanupNewOrleansEvents method
            members.Add(GenerateCleanupMethod(eventsList));

            // Generate __PublishToStreamAsync helper
            members.Add(GeneratePublishToStreamAsyncMethod());

            // Generate subscription method implementations (that throw NotSupportedException)
            foreach (var evt in eventsList)
            {
                members.Add(GenerateGrainSubscribeMethodThrows(evt, withHandler: false));
                members.Add(GenerateGrainSubscribeMethodThrows(evt, withHandler: true));
            }

            return members.ToArray();
        }

        /// <summary>
        /// Generates proxy class members for events (local handlers, raise methods, subscription methods).
        /// </summary>
        public MemberDeclarationSyntax[] GenerateProxyMembers(
            IEnumerable<EventDescription> events,
            Dictionary<ITypeParameterSymbol, string> typeParameterSubstitutions)
        {
            var eventsList = events.ToList();
            if (eventsList.Count == 0)
            {
                return Array.Empty<MemberDeclarationSyntax>();
            }

            var members = new List<MemberDeclarationSyntax>();

            foreach (var evt in eventsList)
            {
                // Generate local handler backing field
                members.Add(GenerateProxyHandlerField(evt));

                // Generate event property with add/remove accessors
                members.Add(GenerateProxyEventProperty(evt));

                // Generate __RaiseEventName method
                members.Add(GenerateProxyRaiseMethod(evt));

                // Generate SubscribeToEventNameAsync methods
                members.Add(GenerateProxySubscribeMethod(evt, withHandler: false));
                members.Add(GenerateProxySubscribeMethod(evt, withHandler: true));
            }

            // Generate __GetEventStream helper
            members.Add(GenerateProxyGetEventStreamMethod(eventsList[0]));

            return members.ToArray();
        }

        #region Interface Generation

        private EventFieldDeclarationSyntax GenerateInterfaceEventDeclaration(EventDescription evt)
        {
            TypeSyntax eventType;
            if (evt.HasPayload)
            {
                eventType = GenericName(
                    Identifier("global::System.EventHandler"),
                    TypeArgumentList(SingletonSeparatedList(evt.PayloadType!.ToTypeSyntax())));
            }
            else
            {
                eventType = ParseTypeName("global::System.EventHandler");
            }

            // Add nullable annotation
            eventType = NullableType(eventType);

            var variableDecl = VariableDeclaration(eventType)
                .AddVariables(VariableDeclarator(evt.EventName));

            return EventFieldDeclaration(variableDecl);
        }

        private MethodDeclarationSyntax GenerateInterfaceSubscribeMethod(EventDescription evt, bool withHandler)
        {
            // Return type: Task<IEventSubscription<T>> or Task<IEventSubscription<EventArgs>>
            var payloadTypeSyntax = evt.HasPayload
                ? evt.PayloadType!.ToTypeSyntax()
                : ParseTypeName("global::System.EventArgs");

            var subscriptionType = GenericName(
                Identifier("global::Orleans.IEventSubscription"),
                TypeArgumentList(SingletonSeparatedList(payloadTypeSyntax)));

            var returnType = GenericName(
                Identifier("global::System.Threading.Tasks.Task"),
                TypeArgumentList(SingletonSeparatedList<TypeSyntax>(subscriptionType)));

            var methodName = $"SubscribeTo{evt.EventName}Async";

            var method = MethodDeclaration(returnType, methodName);

            if (withHandler)
            {
                // Parameter: Func<T, Task> handler
                var funcType = GenericName(
                    Identifier("global::System.Func"),
                    TypeArgumentList(SeparatedList<TypeSyntax>(new[]
                    {
                        payloadTypeSyntax,
                        ParseTypeName("global::System.Threading.Tasks.Task")
                    })));

                method = method.WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("handler")).WithType(funcType))));
            }

            return method.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        }

        #endregion

        #region Grain Generation

        private FieldDeclarationSyntax GenerateStreamField(EventDescription evt)
        {
            // private IAsyncStream<T>? __eventName_stream;
            var payloadTypeSyntax = evt.HasPayload
                ? evt.PayloadType!.ToTypeSyntax()
                : ParseTypeName("global::System.EventArgs");

            var streamType = NullableType(GenericName(
                Identifier("global::Orleans.Streams.IAsyncStream"),
                TypeArgumentList(SingletonSeparatedList(payloadTypeSyntax))));

            var fieldName = $"__{ToCamelCase(evt.EventName)}_stream";

            return FieldDeclaration(
                VariableDeclaration(streamType)
                    .AddVariables(VariableDeclarator(fieldName)))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)));
        }

        private FieldDeclarationSyntax GenerateBridgeHandlerField(EventDescription evt)
        {
            // private EventHandler<T>? __eventName_bridge;
            TypeSyntax eventType;
            if (evt.HasPayload)
            {
                eventType = GenericName(
                    Identifier("global::System.EventHandler"),
                    TypeArgumentList(SingletonSeparatedList(evt.PayloadType!.ToTypeSyntax())));
            }
            else
            {
                eventType = ParseTypeName("global::System.EventHandler");
            }

            var fieldName = $"__{ToCamelCase(evt.EventName)}_bridge";

            return FieldDeclaration(
                VariableDeclaration(NullableType(eventType))
                    .AddVariables(VariableDeclarator(fieldName)))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)));
        }

        private MethodDeclarationSyntax GenerateParticipateMethod(List<EventDescription> events)
        {
            // void ILifecycleParticipant<IGrainLifecycle>.Participate(IGrainLifecycle lifecycle)
            var statements = new List<StatementSyntax>();

            // lifecycle.Subscribe<GrainClass>(GrainLifecycleStage.Activate, OnActivate, OnDeactivate);
            var grainClassName = events[0].GrainClass.Name;

            var subscribeCall = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("lifecycle"),
                    GenericName(
                        Identifier("Subscribe"),
                        TypeArgumentList(SingletonSeparatedList<TypeSyntax>(
                            IdentifierName(grainClassName))))))
                .WithArgumentList(ArgumentList(SeparatedList(new[]
                {
                    Argument(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ParseTypeName("global::Orleans.Runtime.GrainLifecycleStage"),
                        IdentifierName("Activate"))),
                    Argument(SimpleLambdaExpression(
                        Parameter(Identifier("ct")),
                        Block(
                            ExpressionStatement(InvocationExpression(IdentifierName("__InitializeNewOrleansEvents"))),
                            ReturnStatement(MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ParseTypeName("global::System.Threading.Tasks.Task"),
                                IdentifierName("CompletedTask")))))),
                    Argument(SimpleLambdaExpression(
                        Parameter(Identifier("ct")),
                        Block(
                            ExpressionStatement(InvocationExpression(IdentifierName("__CleanupNewOrleansEvents"))),
                            ReturnStatement(MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ParseTypeName("global::System.Threading.Tasks.Task"),
                                IdentifierName("CompletedTask"))))))
                })));

            statements.Add(ExpressionStatement(subscribeCall));

            return MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)),
                Identifier("Participate"))
                .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(
                    GenericName(
                        Identifier("global::Orleans.ILifecycleParticipant"),
                        TypeArgumentList(SingletonSeparatedList<TypeSyntax>(
                            ParseTypeName("global::Orleans.Runtime.IGrainLifecycle"))))))
                .WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("lifecycle"))
                        .WithType(ParseTypeName("global::Orleans.Runtime.IGrainLifecycle")))))
                .WithBody(Block(statements));
        }

        private MethodDeclarationSyntax GenerateInitializeMethod(List<EventDescription> events)
        {
            var statements = new List<StatementSyntax>();

            // var streamProvider = this.GetStreamProvider("SMS");
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(VariableDeclarator("streamProvider")
                        .WithInitializer(EqualsValueClause(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ThisExpression(),
                                    IdentifierName("GetStreamProvider")))
                                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                    Argument(LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        Literal("SMS")))))))))));

            // var grainKey = this.GetPrimaryKeyXxx();
            var keyExtraction = GetKeyExtractionExpression(events[0].KeyType);
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(VariableDeclarator("grainKey")
                        .WithInitializer(EqualsValueClause(keyExtraction)))));

            // For each event, set up stream and bridge
            foreach (var evt in events)
            {
                var streamFieldName = $"__{ToCamelCase(evt.EventName)}_stream";
                var bridgeFieldName = $"__{ToCamelCase(evt.EventName)}_bridge";
                var payloadTypeSyntax = evt.HasPayload
                    ? evt.PayloadType!.ToTypeSyntax()
                    : ParseTypeName("global::System.EventArgs");

                // var streamId = StreamId.Create("IPlayerGrain.ChatMessage", grainKey);
                var streamIdVarName = $"{ToCamelCase(evt.EventName)}StreamId";
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .AddVariables(VariableDeclarator(streamIdVarName)
                            .WithInitializer(EqualsValueClause(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        ParseTypeName("global::Orleans.Runtime.StreamId"),
                                        IdentifierName("Create")))
                                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                                    {
                                        Argument(LiteralExpression(
                                            SyntaxKind.StringLiteralExpression,
                                            Literal(evt.StreamNamespace))),
                                        Argument(IdentifierName("grainKey"))
                                    }))))))));

                // __eventName_stream = streamProvider.GetStream<T>(streamId);
                statements.Add(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(streamFieldName),
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("streamProvider"),
                                GenericName(
                                    Identifier("GetStream"),
                                    TypeArgumentList(SingletonSeparatedList(payloadTypeSyntax)))))
                            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                Argument(IdentifierName(streamIdVarName))))))));

                // __eventName_bridge = (sender, payload) => { _ = __PublishToStreamAsync(__eventName_stream, payload); };
                var bridgeLambda = ParenthesizedLambdaExpression(
                    ParameterList(SeparatedList(new[]
                    {
                        Parameter(Identifier("sender")),
                        Parameter(Identifier("payload"))
                    })),
                    Block(
                        ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName("_"),
                                InvocationExpression(IdentifierName("__PublishToStreamAsync"))
                                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                                    {
                                        Argument(IdentifierName(streamFieldName)),
                                        Argument(IdentifierName("payload"))
                                    })))))));

                statements.Add(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(bridgeFieldName),
                        bridgeLambda)));

                // EventName += __eventName_bridge;
                statements.Add(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.AddAssignmentExpression,
                        IdentifierName(evt.EventName),
                        IdentifierName(bridgeFieldName))));
            }

            return MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)),
                Identifier("__InitializeNewOrleansEvents"))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
                .WithBody(Block(statements));
        }

        private MethodDeclarationSyntax GenerateCleanupMethod(List<EventDescription> events)
        {
            var statements = new List<StatementSyntax>();

            foreach (var evt in events)
            {
                var bridgeFieldName = $"__{ToCamelCase(evt.EventName)}_bridge";

                // if (__eventName_bridge != null) { EventName -= __eventName_bridge; __eventName_bridge = null; }
                statements.Add(IfStatement(
                    BinaryExpression(
                        SyntaxKind.NotEqualsExpression,
                        IdentifierName(bridgeFieldName),
                        LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    Block(
                        ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SubtractAssignmentExpression,
                                IdentifierName(evt.EventName),
                                IdentifierName(bridgeFieldName))),
                        ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(bridgeFieldName),
                                LiteralExpression(SyntaxKind.NullLiteralExpression))))));
            }

            return MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)),
                Identifier("__CleanupNewOrleansEvents"))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
                .WithBody(Block(statements));
        }

        private MethodDeclarationSyntax GeneratePublishToStreamAsyncMethod()
        {
            // private async Task __PublishToStreamAsync<T>(IAsyncStream<T>? stream, T payload)
            var statements = new List<StatementSyntax>();

            // if (stream == null) return;
            statements.Add(IfStatement(
                BinaryExpression(
                    SyntaxKind.EqualsExpression,
                    IdentifierName("stream"),
                    LiteralExpression(SyntaxKind.NullLiteralExpression)),
                ReturnStatement()));

            // try { await stream.OnNextAsync(payload); } catch (Exception) { /* log warning */ }
            var tryBlock = Block(
                ExpressionStatement(
                    AwaitExpression(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("stream"),
                                IdentifierName("OnNextAsync")))
                            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                Argument(IdentifierName("payload"))))))));

            var catchBlock = CatchClause()
                .WithDeclaration(CatchDeclaration(ParseTypeName("global::System.Exception")))
                .WithBlock(Block()); // Swallow exception - best-effort delivery

            statements.Add(TryStatement()
                .WithBlock(tryBlock)
                .AddCatches(catchBlock));

            return MethodDeclaration(
                ParseTypeName("global::System.Threading.Tasks.Task"),
                Identifier("__PublishToStreamAsync"))
                .WithModifiers(TokenList(
                    Token(SyntaxKind.PrivateKeyword),
                    Token(SyntaxKind.AsyncKeyword)))
                .WithTypeParameterList(TypeParameterList(SingletonSeparatedList(
                    TypeParameter("T"))))
                .WithParameterList(ParameterList(SeparatedList(new[]
                {
                    Parameter(Identifier("stream"))
                        .WithType(NullableType(GenericName(
                            Identifier("global::Orleans.Streams.IAsyncStream"),
                            TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName("T")))))),
                    Parameter(Identifier("payload"))
                        .WithType(IdentifierName("T"))
                })))
                .WithBody(Block(statements));
        }

        private MethodDeclarationSyntax GenerateGrainSubscribeMethodThrows(EventDescription evt, bool withHandler)
        {
            // Grain-side subscription methods throw NotSupportedException
            // because subscriptions should only be called through proxies
            var payloadTypeSyntax = evt.HasPayload
                ? evt.PayloadType!.ToTypeSyntax()
                : ParseTypeName("global::System.EventArgs");

            var subscriptionType = GenericName(
                Identifier("global::Orleans.IEventSubscription"),
                TypeArgumentList(SingletonSeparatedList(payloadTypeSyntax)));

            var returnType = GenericName(
                Identifier("global::System.Threading.Tasks.Task"),
                TypeArgumentList(SingletonSeparatedList<TypeSyntax>(subscriptionType)));

            var methodName = $"SubscribeTo{evt.EventName}Async";

            var throwExpr = ThrowExpression(
                ObjectCreationExpression(ParseTypeName("global::System.NotSupportedException"))
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(
                        Argument(LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            Literal($"Event subscriptions must be created through grain references, not direct grain calls. " +
                                   $"Use GrainFactory.GetGrain<{evt.GrainInterface.Name}>(key).{methodName}() instead.")))))));

            var method = MethodDeclaration(returnType, methodName)
                .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(
                    evt.GrainInterface.ToNameSyntax()))
                .WithExpressionBody(ArrowExpressionClause(throwExpr))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            if (withHandler)
            {
                var funcType = GenericName(
                    Identifier("global::System.Func"),
                    TypeArgumentList(SeparatedList<TypeSyntax>(new[]
                    {
                        payloadTypeSyntax,
                        ParseTypeName("global::System.Threading.Tasks.Task")
                    })));

                method = method.WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("handler")).WithType(funcType))));
            }

            return method;
        }

        #endregion

        #region Proxy Generation

        private FieldDeclarationSyntax GenerateProxyHandlerField(EventDescription evt)
        {
            // private EventHandler<T>? __eventName_localHandlers;
            TypeSyntax eventType;
            if (evt.HasPayload)
            {
                eventType = GenericName(
                    Identifier("global::System.EventHandler"),
                    TypeArgumentList(SingletonSeparatedList(evt.PayloadType!.ToTypeSyntax())));
            }
            else
            {
                eventType = ParseTypeName("global::System.EventHandler");
            }

            var fieldName = $"__{ToCamelCase(evt.EventName)}_localHandlers";

            return FieldDeclaration(
                VariableDeclaration(NullableType(eventType))
                    .AddVariables(VariableDeclarator(fieldName)))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)));
        }

        private EventDeclarationSyntax GenerateProxyEventProperty(EventDescription evt)
        {
            // public event EventHandler<T>? EventName { add => ...; remove => ...; }
            TypeSyntax eventType;
            if (evt.HasPayload)
            {
                eventType = GenericName(
                    Identifier("global::System.EventHandler"),
                    TypeArgumentList(SingletonSeparatedList(evt.PayloadType!.ToTypeSyntax())));
            }
            else
            {
                eventType = ParseTypeName("global::System.EventHandler");
            }

            var fieldName = $"__{ToCamelCase(evt.EventName)}_localHandlers";

            var addAccessor = AccessorDeclaration(SyntaxKind.AddAccessorDeclaration)
                .WithExpressionBody(ArrowExpressionClause(
                    AssignmentExpression(
                        SyntaxKind.AddAssignmentExpression,
                        IdentifierName(fieldName),
                        IdentifierName("value"))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            var removeAccessor = AccessorDeclaration(SyntaxKind.RemoveAccessorDeclaration)
                .WithExpressionBody(ArrowExpressionClause(
                    AssignmentExpression(
                        SyntaxKind.SubtractAssignmentExpression,
                        IdentifierName(fieldName),
                        IdentifierName("value"))))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

            return EventDeclaration(NullableType(eventType), evt.EventName)
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithAccessorList(AccessorList(List(new[] { addAccessor, removeAccessor })));
        }

        private MethodDeclarationSyntax GenerateProxyRaiseMethod(EventDescription evt)
        {
            // internal void __RaiseEventName(T payload)
            var fieldName = $"__{ToCamelCase(evt.EventName)}_localHandlers";
            var methodName = $"__Raise{evt.EventName}";

            var payloadTypeSyntax = evt.HasPayload
                ? evt.PayloadType!.ToTypeSyntax()
                : ParseTypeName("global::System.EventArgs");

            // Thread-safe pattern: capture to local variable first
            var statements = new List<StatementSyntax>();

            // var handlers = __eventName_localHandlers;
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(VariableDeclarator("handlers")
                        .WithInitializer(EqualsValueClause(IdentifierName(fieldName))))));

            // if (handlers == null) return;
            statements.Add(IfStatement(
                BinaryExpression(
                    SyntaxKind.EqualsExpression,
                    IdentifierName("handlers"),
                    LiteralExpression(SyntaxKind.NullLiteralExpression)),
                ReturnStatement()));

            // try { handlers.Invoke(this, payload); } catch { }
            var invokeArgs = evt.HasPayload
                ? ArgumentList(SeparatedList(new[]
                    {
                        Argument(ThisExpression()),
                        Argument(IdentifierName("payload"))
                    }))
                : ArgumentList(SeparatedList(new[]
                    {
                        Argument(ThisExpression()),
                        Argument(MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            ParseTypeName("global::System.EventArgs"),
                            IdentifierName("Empty")))
                    }));

            var tryBlock = Block(
                ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("handlers"),
                            IdentifierName("Invoke")))
                        .WithArgumentList(invokeArgs)));

            statements.Add(TryStatement()
                .WithBlock(tryBlock)
                .AddCatches(CatchClause().WithBlock(Block()))); // Swallow handler exceptions

            var method = MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)),
                Identifier(methodName))
                .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword)))
                .WithBody(Block(statements));

            if (evt.HasPayload)
            {
                method = method.WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("payload")).WithType(payloadTypeSyntax))));
            }

            return method;
        }

        private MethodDeclarationSyntax GenerateProxySubscribeMethod(EventDescription evt, bool withHandler)
        {
            var payloadTypeSyntax = evt.HasPayload
                ? evt.PayloadType!.ToTypeSyntax()
                : ParseTypeName("global::System.EventArgs");

            var subscriptionType = GenericName(
                Identifier("global::Orleans.IEventSubscription"),
                TypeArgumentList(SingletonSeparatedList(payloadTypeSyntax)));

            var returnType = GenericName(
                Identifier("global::System.Threading.Tasks.Task"),
                TypeArgumentList(SingletonSeparatedList<TypeSyntax>(subscriptionType)));

            var methodName = $"SubscribeTo{evt.EventName}Async";
            var raiseMethodName = $"__Raise{evt.EventName}";

            var statements = new List<StatementSyntax>();

            // var stream = __GetEventStream<T>("IPlayerGrain.ChatMessage");
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(VariableDeclarator("stream")
                        .WithInitializer(EqualsValueClause(
                            InvocationExpression(
                                GenericName(
                                    Identifier("__GetEventStream"),
                                    TypeArgumentList(SingletonSeparatedList(payloadTypeSyntax))))
                                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                    Argument(LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        Literal(evt.StreamNamespace)))))))))));

            // var streamId = stream.StreamId;
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(VariableDeclarator("streamId")
                        .WithInitializer(EqualsValueClause(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("stream"),
                                IdentifierName("StreamId")))))));

            // Build the subscribe lambda
            // Lambda signature: (T payload, StreamSequenceToken token) => Task
            var tokenType = ParseTypeName("global::Orleans.Streams.StreamSequenceToken");
            ExpressionSyntax subscribeLambda;
            if (withHandler)
            {
                // async (T payload, StreamSequenceToken token) => { await handler(payload); __RaiseEventName(payload); }
                subscribeLambda = ParenthesizedLambdaExpression(
                    ParameterList(SeparatedList(new[]
                    {
                        Parameter(Identifier("payload")).WithType(payloadTypeSyntax),
                        Parameter(Identifier("token")).WithType(tokenType)
                    })),
                    Block(
                        ExpressionStatement(
                            AwaitExpression(
                                InvocationExpression(IdentifierName("handler"))
                                    .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                        Argument(IdentifierName("payload"))))))),
                        ExpressionStatement(
                            InvocationExpression(IdentifierName(raiseMethodName))
                                .WithArgumentList(evt.HasPayload
                                    ? ArgumentList(SingletonSeparatedList(Argument(IdentifierName("payload"))))
                                    : ArgumentList()))))
                    .WithAsyncKeyword(Token(SyntaxKind.AsyncKeyword));
            }
            else
            {
                // (T payload, StreamSequenceToken token) => { __RaiseEventName(payload); return Task.CompletedTask; }
                subscribeLambda = ParenthesizedLambdaExpression(
                    ParameterList(SeparatedList(new[]
                    {
                        Parameter(Identifier("payload")).WithType(payloadTypeSyntax),
                        Parameter(Identifier("token")).WithType(tokenType)
                    })),
                    Block(
                        ExpressionStatement(
                            InvocationExpression(IdentifierName(raiseMethodName))
                                .WithArgumentList(evt.HasPayload
                                    ? ArgumentList(SingletonSeparatedList(Argument(IdentifierName("payload"))))
                                    : ArgumentList())),
                        ReturnStatement(MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            ParseTypeName("global::System.Threading.Tasks.Task"),
                            IdentifierName("CompletedTask")))));
            }

            // var handle = await Orleans.Streams.AsyncObservableExtensions.SubscribeAsync(stream, lambda);
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(VariableDeclarator("handle")
                        .WithInitializer(EqualsValueClause(
                            AwaitExpression(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        ParseTypeName("global::Orleans.Streams.AsyncObservableExtensions"),
                                        IdentifierName("SubscribeAsync")))
                                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                                    {
                                        Argument(IdentifierName("stream")),
                                        Argument(subscribeLambda)
                                    }))))))))));

            // return new EventSubscription<T>(handle, streamId);
            statements.Add(ReturnStatement(
                ObjectCreationExpression(
                    GenericName(
                        Identifier("global::Orleans.Streaming.EventSubscription"),
                        TypeArgumentList(SingletonSeparatedList(payloadTypeSyntax))))
                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                    {
                        Argument(IdentifierName("handle")),
                        Argument(IdentifierName("streamId"))
                    })))));

            var method = MethodDeclaration(returnType, methodName)
                .WithModifiers(TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.AsyncKeyword)))
                .WithBody(Block(statements));

            if (withHandler)
            {
                var funcType = GenericName(
                    Identifier("global::System.Func"),
                    TypeArgumentList(SeparatedList<TypeSyntax>(new[]
                    {
                        payloadTypeSyntax,
                        ParseTypeName("global::System.Threading.Tasks.Task")
                    })));

                method = method.WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("handler")).WithType(funcType))));
            }

            return method;
        }

        private MethodDeclarationSyntax GenerateProxyGetEventStreamMethod(EventDescription evt)
        {
            // private IAsyncStream<T> __GetEventStream<T>(string eventNamespace)
            var statements = new List<StatementSyntax>();

            // var streamProvider = ServiceProviderKeyedServiceExtensions.GetRequiredKeyedService<IStreamProvider>(this.Shared.ServiceProvider, "SMS");
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(VariableDeclarator("streamProvider")
                        .WithInitializer(EqualsValueClause(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ParseTypeName("global::Microsoft.Extensions.DependencyInjection.ServiceProviderKeyedServiceExtensions"),
                                    GenericName(
                                        Identifier("GetRequiredKeyedService"),
                                        TypeArgumentList(SingletonSeparatedList<TypeSyntax>(
                                            ParseTypeName("global::Orleans.Streams.IStreamProvider"))))))
                                .WithArgumentList(ArgumentList(SeparatedList(new[]
                                {
                                    Argument(MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        ThisExpression(),
                                        IdentifierName("ServiceProvider"))),
                                    Argument(LiteralExpression(
                                        SyntaxKind.StringLiteralExpression,
                                        Literal("SMS")))
                                })))))))));

            // var grainKey = this.GetPrimaryKeyXxx();
            var keyExtraction = GetProxyKeyExtractionExpression(evt.KeyType);
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(VariableDeclarator("grainKey")
                        .WithInitializer(EqualsValueClause(keyExtraction)))));

            // var streamId = StreamId.Create(eventNamespace, grainKey);
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(VariableDeclarator("streamId")
                        .WithInitializer(EqualsValueClause(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    ParseTypeName("global::Orleans.Runtime.StreamId"),
                                    IdentifierName("Create")))
                                .WithArgumentList(ArgumentList(SeparatedList(new[]
                                {
                                    Argument(IdentifierName("eventNamespace")),
                                    Argument(IdentifierName("grainKey"))
                                }))))))));

            // return streamProvider.GetStream<T>(streamId);
            statements.Add(ReturnStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("streamProvider"),
                        GenericName(
                            Identifier("GetStream"),
                            TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName("T"))))))
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(
                        Argument(IdentifierName("streamId")))))));

            return MethodDeclaration(
                GenericName(
                    Identifier("global::Orleans.Streams.IAsyncStream"),
                    TypeArgumentList(SingletonSeparatedList<TypeSyntax>(IdentifierName("T")))),
                Identifier("__GetEventStream"))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
                .WithTypeParameterList(TypeParameterList(SingletonSeparatedList(TypeParameter("T"))))
                .WithParameterList(ParameterList(SingletonSeparatedList(
                    Parameter(Identifier("eventNamespace"))
                        .WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))))
                .WithBody(Block(statements));
        }

        #endregion

        #region Helper Methods

        private INamedTypeSymbol? FindPrimaryGrainInterface(INamedTypeSymbol grainClass)
        {
            foreach (var iface in grainClass.AllInterfaces)
            {
                if (IsGrainKeyInterface(iface) || iface.AllInterfaces.Any(IsGrainKeyInterface))
                {
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
            var name = iface.ToDisplayString();
            return name.StartsWith("Orleans.IGrainWith") && name.EndsWith("Key");
        }

        private GrainKeyType? GetGrainKeyType(INamedTypeSymbol grainClass)
        {
            foreach (var iface in grainClass.AllInterfaces)
            {
                var name = iface.ToDisplayString();
                if (name == "Orleans.IGrainWithStringKey") return GrainKeyType.String;
                if (name == "Orleans.IGrainWithGuidKey") return GrainKeyType.Guid;
                if (name == "Orleans.IGrainWithIntegerKey") return GrainKeyType.Integer;
                if (name == "Orleans.IGrainWithGuidCompoundKey") return GrainKeyType.GuidCompound;
                if (name == "Orleans.IGrainWithIntegerCompoundKey") return GrainKeyType.IntegerCompound;
            }
            return null;
        }

        private ExpressionSyntax GetKeyExtractionExpression(GrainKeyType keyType)
        {
            return keyType switch
            {
                GrainKeyType.String => InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(),
                        IdentifierName("GetPrimaryKeyString"))),
                GrainKeyType.Guid => InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ThisExpression(),
                                IdentifierName("GetPrimaryKey"))),
                        IdentifierName("ToString"))),
                GrainKeyType.Integer => InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ThisExpression(),
                                IdentifierName("GetPrimaryKeyLong"))),
                        IdentifierName("ToString"))),
                GrainKeyType.GuidCompound => CreateCompoundKeyExpression("GetPrimaryKey"),
                GrainKeyType.IntegerCompound => CreateCompoundKeyExpression("GetPrimaryKeyLong"),
                _ => throw new ArgumentOutOfRangeException(nameof(keyType))
            };
        }

        private ExpressionSyntax GetProxyKeyExtractionExpression(GrainKeyType keyType)
        {
            // Proxy uses this.GetPrimaryKeyString() etc. which is available on GrainReference
            return GetKeyExtractionExpression(keyType);
        }

        private ExpressionSyntax CreateCompoundKeyExpression(string methodName)
        {
            // $"{this.GetPrimaryKey(out var ext)}:{ext}" or similar
            // For simplicity, we'll use string interpolation
            return InterpolatedStringExpression(
                Token(SyntaxKind.InterpolatedStringStartToken),
                List<InterpolatedStringContentSyntax>(new InterpolatedStringContentSyntax[]
                {
                    Interpolation(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ThisExpression(),
                                IdentifierName(methodName)))
                            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                Argument(DeclarationExpression(
                                    IdentifierName("var"),
                                    SingleVariableDesignation(Identifier("ext"))))
                                    .WithRefKindKeyword(Token(SyntaxKind.OutKeyword)))))),
                    InterpolatedStringText()
                        .WithTextToken(Token(
                            TriviaList(),
                            SyntaxKind.InterpolatedStringTextToken,
                            ":",
                            ":",
                            TriviaList())),
                    Interpolation(IdentifierName("ext"))
                }));
        }

        private bool HasAttribute(IEventSymbol eventSymbol, INamedTypeSymbol? attributeType)
        {
            if (attributeType is null) return false;
            return eventSymbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
        }

        private ITypeSymbol? GetEventPayloadType(IEventSymbol eventSymbol)
        {
            // Check if the event type is EventHandler<T>
            if (eventSymbol.Type is INamedTypeSymbol namedType &&
                namedType.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, LibraryTypes.EventHandler_1))
            {
                return namedType.TypeArguments[0];
            }
            return null;
        }

        private bool IsPlainEventHandler(IEventSymbol eventSymbol)
        {
            return SymbolEqualityComparer.Default.Equals(eventSymbol.Type, LibraryTypes.EventHandler);
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (char.IsLower(name[0])) return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        #endregion
    }
}
