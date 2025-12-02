// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    /// <summary>
    /// Produces a MoveNext() method for an async method.
    /// </summary>
    internal class AsyncMethodToStateMachineRewriter : MethodToStateMachineRewriter
    {
        /// <summary>
        /// The method being rewritten.
        /// </summary>
        protected readonly MethodSymbol _method;

        /// <summary>
        /// The field of the generated async class used to store the async method builder: an instance of
        /// <see cref="AsyncVoidMethodBuilder"/>, <see cref="AsyncTaskMethodBuilder"/>, or <see cref="AsyncTaskMethodBuilder{TResult}"/> depending on the
        /// return type of the async method.
        /// </summary>
        protected readonly FieldSymbol _asyncMethodBuilderField;

        /// <summary>
        /// A collection of well-known members for the current async method builder.
        /// </summary>
        protected readonly AsyncMethodBuilderMemberCollection _asyncMethodBuilderMemberCollection;

        /// <summary>
        /// The exprReturnLabel is used to label the return handling code at the end of the async state-machine
        /// method. Return expressions are rewritten as unconditional branches to exprReturnLabel.
        /// </summary>
        protected readonly LabelSymbol _exprReturnLabel;

        /// <summary>
        /// The label containing a return from the method when the async method has not completed.
        /// </summary>
        private readonly LabelSymbol _exitLabel;

        /// <summary>
        /// The field of the generated async class used in generic task returning async methods to store the value
        /// of rewritten return expressions. The return-handling code then uses <c>SetResult</c> on the async method builder
        /// to make the result available to the caller.
        /// </summary>
        private readonly LocalSymbol? _exprRetValue;

        private readonly LoweredDynamicOperationFactory _dynamicFactory;

        private readonly Dictionary<TypeSymbol, FieldSymbol> _awaiterFields;
        private int _nextAwaiterId;

        private readonly Dictionary<BoundValuePlaceholderBase, BoundExpression> _placeholderMap;

        #region DOTNExT Persistence Support
        /// <summary>
        /// Local variable to cache the persistence service reference during MoveNext execution.
        /// </summary>
        private LocalSymbol? _persistenceServiceLocal;

        /// <summary>
        /// Whether this method should have persistence support (has [Persistable] attribute).
        /// </summary>
        private readonly bool _enablePersistence;

        /// <summary>
        /// The method ID used for persistence (derived from containing type + method name).
        /// </summary>
        private readonly string? _persistenceMethodId;

        /// <summary>
        /// Cached type reference for DOTNExT.Persistence.AsyncPersistenceContext.
        /// </summary>
        private NamedTypeSymbol? _asyncPersistenceContextType;

        /// <summary>
        /// Cached type reference for DOTNExT.Persistence.IAsyncPersistenceService.
        /// </summary>
        private NamedTypeSymbol? _persistenceServiceType;

        /// <summary>
        /// File-based logging for DOTNExT Roslyn+ diagnostics.
        /// Writes to file specified by DOTNEXT_ROSLYN_LOG environment variable,
        /// or defaults to 'dotnext-roslyn-codegen.log' in temp directory.
        /// </summary>
        private static void LogToFile(string message)
        {
            try
            {
                var logPath = System.Environment.GetEnvironmentVariable("DOTNEXT_ROSLYN_LOG")
                    ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dotnext-roslyn-codegen.log");

                var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logLine = $"[{timestamp}] {message}{System.Environment.NewLine}";

                System.IO.File.AppendAllText(logPath, logLine);
            }
            catch
            {
                // Ignore logging errors - don't break compilation
            }
        }

        /// <summary>
        /// Log to both stderr and file for comprehensive debugging.
        /// </summary>
        private static void Log(string message)
        {
            System.Console.Error.WriteLine(message);
            LogToFile(message);
        }

        /// <summary>
        /// Logs a detailed description of generated code for debugging.
        /// Since we can't easily emit source from bound nodes, this provides
        /// a structured description that can be used to understand what was generated.
        /// </summary>
        private void LogGeneratedCodeDescription(string phase, string description)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== DOTNExT-Roslyn Generated Code: {phase} ===");
            sb.AppendLine($"Method: {_persistenceMethodId}");
            sb.AppendLine($"State Machine Type: {F.CurrentType?.Name ?? "unknown"}");
            sb.AppendLine($"Is Struct: {F.CurrentType?.IsValueType ?? false}");
            sb.AppendLine($"Description:");
            sb.AppendLine(description);
            sb.AppendLine("=== End Generated Code Description ===");
            Log(sb.ToString());
        }

        /// <summary>
        /// Logs information about how to view the actual generated IL/code.
        /// </summary>
        private static void LogCodeViewingInstructions()
        {
            var instructions = @"
=== How to View Generated Code ===
1. After compilation, use ILSpy or dnSpy on the output assembly
2. Look for the state machine type (ends with 'd__N' where N is a number)
3. Examine the MoveNext() method for persistence calls
4. Look for calls to:
   - AsyncPersistenceContext.get_Current()
   - IAsyncPersistenceService.TryRestore<T>(ref T, string)
   - IAsyncPersistenceService.Checkpoint(object, int, string)
5. Environment variable DOTNEXT_ROSLYN_LOG controls log file location
6. Default log location: [TEMP]/dotnext-roslyn-codegen.log
=== End Instructions ===
";
            Log(instructions);
        }
        #endregion

        internal AsyncMethodToStateMachineRewriter(
            MethodSymbol method,
            int methodOrdinal,
            AsyncMethodBuilderMemberCollection asyncMethodBuilderMemberCollection,
            SyntheticBoundNodeFactory F,
            FieldSymbol state,
            FieldSymbol builder,
            FieldSymbol? instanceIdField,
            IReadOnlySet<Symbol> hoistedVariables,
            IReadOnlyDictionary<Symbol, CapturedSymbolReplacement> nonReusableLocalProxies,
            SynthesizedLocalOrdinalsDispenser synthesizedLocalOrdinals,
            ArrayBuilder<StateMachineStateDebugInfo> stateMachineStateDebugInfoBuilder,
            VariableSlotAllocator? slotAllocatorOpt,
            int nextFreeHoistedLocalSlot,
            BindingDiagnosticBag diagnostics)
            : base(F, method, state, instanceIdField, hoistedVariables, nonReusableLocalProxies, synthesizedLocalOrdinals, stateMachineStateDebugInfoBuilder, slotAllocatorOpt, nextFreeHoistedLocalSlot, diagnostics)
        {
            _method = method;
            _asyncMethodBuilderMemberCollection = asyncMethodBuilderMemberCollection;
            _asyncMethodBuilderField = builder;
            _exprReturnLabel = F.GenerateLabel("exprReturn");
            _exitLabel = F.GenerateLabel("exitLabel");

            _exprRetValue = method.IsAsyncEffectivelyReturningGenericTask(F.Compilation)
                ? F.SynthesizedLocal(asyncMethodBuilderMemberCollection.ResultType, syntax: F.Syntax, kind: SynthesizedLocalKind.AsyncMethodReturnValue)
                : null;

            _dynamicFactory = new LoweredDynamicOperationFactory(F, methodOrdinal);
            _awaiterFields = new Dictionary<TypeSymbol, FieldSymbol>(Symbols.SymbolEqualityComparer.IgnoringDynamicTupleNamesAndNullability);
            _nextAwaiterId = slotAllocatorOpt?.PreviousAwaiterSlotCount ?? 0;

            _placeholderMap = new Dictionary<BoundValuePlaceholderBase, BoundExpression>();

            // DOTNExT: Check for [Persistable] attribute on the method
            _enablePersistence = method.GetAttributes().Any(a =>
                a.AttributeClass?.Name == "PersistableAttribute" ||
                a.AttributeClass?.ToDisplayString() == "DOTNExT.Persistence.PersistableAttribute");

            if (_enablePersistence)
            {
                _persistenceMethodId = $"{method.ContainingType.ToDisplayString()}.{method.Name}";

                // DOTNExT: Diagnostic - try to resolve persistence types early and report
                var contextType = F.Compilation.GetTypeByMetadataName("DOTNExT.Persistence.AsyncPersistenceContext");
                var serviceType = F.Compilation.GetTypeByMetadataName("DOTNExT.Persistence.IAsyncPersistenceService");

                Log($"[DOTNExT-Roslyn] Found [Persistable] on: {_persistenceMethodId}");
                Log($"[DOTNExT-Roslyn] AsyncPersistenceContext resolved: {contextType is not null}");
                Log($"[DOTNExT-Roslyn] IAsyncPersistenceService resolved: {serviceType is not null}");

                // Also try to list what types ARE available in the DOTNExT.Persistence namespace
                if (contextType is null || serviceType is null)
                {
                    Log($"[DOTNExT-Roslyn] WARNING: Persistence types not found!");
                    Log($"[DOTNExT-Roslyn] Listing {F.Compilation.References.Count()} compilation references:");
                    foreach (var reference in F.Compilation.References)
                    {
                        var refSymbol = F.Compilation.GetAssemblyOrModuleSymbol(reference);
                        if (refSymbol is AssemblySymbol asmSymbol)
                        {
                            Log($"[DOTNExT-Roslyn]   Assembly: {asmSymbol.Name}");
                            // Try to find DOTNExT namespace in this assembly
                            var globalNs = asmSymbol.GlobalNamespace;
                            var dotNextNs = globalNs.GetNamespaceMembers().FirstOrDefault(n => n.Name == "DOTNExT");
                            if (dotNextNs != null)
                            {
                                Log($"[DOTNExT-Roslyn]     Found DOTNExT namespace!");
                                var persistenceNs = dotNextNs.GetNamespaceMembers().FirstOrDefault(n => n.Name == "Persistence");
                                if (persistenceNs != null)
                                {
                                    Log($"[DOTNExT-Roslyn]     Found DOTNExT.Persistence namespace!");
                                    foreach (var type in persistenceNs.GetTypeMembers())
                                    {
                                        Log($"[DOTNExT-Roslyn]       Type: {type.Name} (accessible: {type.DeclaredAccessibility})");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Log($"[DOTNExT-Roslyn]   Reference: {reference.Display}");
                        }
                    }
                    Log($"[DOTNExT-Roslyn] To fix: ensure DOTNExT.Persistence types are in a referenced assembly with matching metadata name");
                }
                else
                {
                    Log($"[DOTNExT-Roslyn] SUCCESS: Persistence injection will be enabled for {_persistenceMethodId}");
                }
            }
        }

#nullable disable

        private FieldSymbol GetAwaiterField(TypeSymbol awaiterType)
        {
            FieldSymbol result;

            // Awaiters of the same type always share the same slot, regardless of what await expressions they belong to.
            // Even in case of nested await expressions only one awaiter is active.
            // So we don't need to tie the awaiter variable to a particular await expression and only use its type
            // to find the previous awaiter field.
            if (!_awaiterFields.TryGetValue(awaiterType, out result))
            {
                int slotIndex;
                if (slotAllocator == null || !slotAllocator.TryGetPreviousAwaiterSlotIndex(F.ModuleBuilderOpt.Translate(awaiterType, F.Syntax, F.Diagnostics.DiagnosticBag), F.Diagnostics.DiagnosticBag, out slotIndex))
                {
                    slotIndex = _nextAwaiterId++;
                }

                string fieldName = GeneratedNames.AsyncAwaiterFieldName(slotIndex);
                result = F.StateMachineField(awaiterType, fieldName, SynthesizedLocalKind.AwaiterField, slotIndex);
                _awaiterFields.Add(awaiterType, result);
            }

            return result;
        }

        protected sealed override string EncMissingStateMessage
            => CodeAnalysisResources.EncCannotResumeSuspendedAsyncMethod;

        protected sealed override StateMachineState FirstIncreasingResumableState
            => StateMachineState.FirstResumableAsyncState;

        /// <summary>
        /// Generate the body for <c>MoveNext()</c>.
        /// </summary>
        internal void GenerateMoveNext(BoundStatement body, MethodSymbol moveNextMethod)
        {
            F.CurrentFunction = moveNextMethod;

            // DOTNExT: Initialize persistence service local BEFORE processing the body
            // This is needed because VisitBody processes await expressions, which need
            // _persistenceServiceLocal to be set for checkpoint injection
            if (_enablePersistence)
            {
                InitializePersistenceServiceLocal();
            }

            BoundStatement rewrittenBody = VisitBody(body);

            ImmutableArray<StateMachineFieldSymbol> rootScopeHoistedLocals;
            TryUnwrapBoundStateMachineScope(ref rewrittenBody, out rootScopeHoistedLocals);

            var bodyBuilder = ArrayBuilder<BoundStatement>.GetInstance();

            bodyBuilder.Add(F.HiddenSequencePoint());
            bodyBuilder.Add(F.Assignment(F.Local(cachedState), F.Field(F.This(), stateField)));
            bodyBuilder.Add(CacheThisIfNeeded());

            // DOTNExT: Add persistence restoration check for [Persistable] methods
            if (_enablePersistence)
            {
                bodyBuilder.Add(GeneratePersistenceRestorationCheck());
            }

            var exceptionLocal = F.SynthesizedLocal(F.WellKnownType(WellKnownType.System_Exception));
            bodyBuilder.Add(
                GenerateTopLevelTry(
                    F.Block(ImmutableArray<LocalSymbol>.Empty,
                        // switch (state) ...
                        F.HiddenSequencePoint(),
                        Dispatch(isOutermost: true),
                        // [body]
                        rewrittenBody
                    ),
                    F.CatchBlocks(GenerateExceptionHandling(exceptionLocal, rootScopeHoistedLocals)))
                );

            // ReturnLabel (for the rewritten return expressions in the user's method body)
            bodyBuilder.Add(F.Label(_exprReturnLabel));

            // this.state = finishedState
            var stateDone = F.Assignment(F.Field(F.This(), stateField), F.Literal(StateMachineState.FinishedState));
            var block = body.Syntax as BlockSyntax;
            if (block == null)
            {
                // this happens, for example, in (async () => await e) where there is no block syntax
                bodyBuilder.Add(stateDone);
            }
            else
            {
                bodyBuilder.Add(F.SequencePointWithSpan(block, block.CloseBraceToken.Span, stateDone));
                bodyBuilder.Add(F.HiddenSequencePoint());
                // The remaining code is hidden to hide the fact that it can run concurrently with the task's continuation
            }

            bodyBuilder.Add(GenerateHoistedLocalsCleanup(rootScopeHoistedLocals));

            bodyBuilder.Add(GenerateSetResultCall());

            // this code is hidden behind a hidden sequence point.
            bodyBuilder.Add(F.Label(_exitLabel));
            bodyBuilder.Add(F.Return());

            var newStatements = bodyBuilder.ToImmutableAndFree();

            var locals = ArrayBuilder<LocalSymbol>.GetInstance();
            locals.Add(cachedState);
            if ((object)cachedThis != null) locals.Add(cachedThis);
            if ((object)_exprRetValue != null) locals.Add(_exprRetValue);
            // DOTNExT: Add persistence service local
            if (_persistenceServiceLocal != null) locals.Add(_persistenceServiceLocal);

            var newBody =
                F.SequencePoint(
                    body.Syntax,
                    F.Block(
                        locals.ToImmutableAndFree(),
                        newStatements));

            if (rootScopeHoistedLocals.Length > 0)
            {
                newBody = MakeStateMachineScope(rootScopeHoistedLocals, newBody);
            }

            newBody = F.Instrument(newBody, instrumentation);

            F.CloseMethod(newBody);
        }

        protected virtual BoundStatement GenerateTopLevelTry(BoundBlock tryBlock, ImmutableArray<BoundCatchBlock> catchBlocks)
            => F.Try(tryBlock, catchBlocks);

        protected virtual BoundStatement GenerateSetResultCall()
        {
            // builder.SetResult([RetVal])
            return F.ExpressionStatement(
                F.Call(
                    F.Field(F.This(), _asyncMethodBuilderField),
                    _asyncMethodBuilderMemberCollection.SetResult,
                    _method.IsAsyncEffectivelyReturningGenericTask(F.Compilation)
                        ? ImmutableArray.Create<BoundExpression>(F.Local(_exprRetValue))
                        : ImmutableArray<BoundExpression>.Empty));
        }

        protected BoundCatchBlock GenerateExceptionHandling(LocalSymbol exceptionLocal, ImmutableArray<StateMachineFieldSymbol> hoistedLocals)
        {
            // catch (Exception ex)
            // {
            //     _state = finishedState;
            //
            //     for each hoisted local:
            //     <>x__y = default
            //
            //     builder.SetException(ex);  OR  if (this.combinedTokens != null) this.combinedTokens.Dispose(); _promiseOfValueOrEnd.SetException(ex); /* for async-iterator method */
            //     return;
            // }

            // _state = finishedState;
            BoundStatement assignFinishedState =
                F.ExpressionStatement(F.AssignmentExpression(F.Field(F.This(), stateField), F.Literal(StateMachineState.FinishedState)));

            // builder.SetException(ex);  OR  if (this.combinedTokens != null) this.combinedTokens.Dispose(); _promiseOfValueOrEnd.SetException(ex);
            BoundStatement callSetException = GenerateSetExceptionCall(exceptionLocal);

            return new BoundCatchBlock(
                F.Syntax,
                ImmutableArray.Create(exceptionLocal),
                F.Local(exceptionLocal),
                exceptionLocal.Type,
                exceptionFilterPrologueOpt: null,
                exceptionFilterOpt: null,
                body: F.Block(
                    assignFinishedState, // _state = finishedState;
                    GenerateHoistedLocalsCleanup(hoistedLocals),
                    callSetException, // builder.SetException(ex);  OR  _promiseOfValueOrEnd.SetException(ex);
                    GenerateReturn(false)), // return;
                isSynthesizedAsyncCatchAll: true);
        }

        protected BoundStatement GenerateHoistedLocalsCleanup(ImmutableArray<StateMachineFieldSymbol> hoistedLocals)
        {
            var builder = ArrayBuilder<BoundStatement>.GetInstance();

            // Cleanup all hoisted local variables
            // so that they can be collected by GC if needed
            foreach (var hoistedLocal in hoistedLocals)
            {
                var useSiteInfo = new CompoundUseSiteInfo<AssemblySymbol>(F.Diagnostics, F.Compilation.Assembly);
                var isManagedType = hoistedLocal.Type.IsManagedType(ref useSiteInfo);
                F.Diagnostics.Add(hoistedLocal.GetFirstLocationOrNone(), useSiteInfo);
                if (!isManagedType)
                {
                    continue;
                }

                builder.Add(F.Assignment(F.Field(F.This(), hoistedLocal), F.NullOrDefault(hoistedLocal.Type)));
            }

            return F.Block(builder.ToImmutableAndFree());
        }

        protected virtual BoundStatement GenerateSetExceptionCall(LocalSymbol exceptionLocal)
        {
            Debug.Assert(!CurrentMethod.IsIterator); // an override handles async-iterators

            // builder.SetException(ex);
            return F.ExpressionStatement(
                F.Call(
                    F.Field(F.This(), _asyncMethodBuilderField),
                    _asyncMethodBuilderMemberCollection.SetException,
                    F.Local(exceptionLocal)));
        }

        protected sealed override BoundStatement GenerateReturn(bool finished)
        {
            return F.Goto(_exitLabel);
        }

        #region Visitors

        protected virtual BoundStatement VisitBody(BoundStatement body)
            => (BoundStatement)Visit(body);

        public sealed override BoundNode VisitExpressionStatement(BoundExpressionStatement node)
        {
            if (node.Expression.Kind == BoundKind.AwaitExpression)
            {
                return VisitAwaitExpression((BoundAwaitExpression)node.Expression, resultPlace: null);
            }
            else if (node.Expression.Kind == BoundKind.AssignmentOperator)
            {
                var expression = (BoundAssignmentOperator)node.Expression;
                if (expression.Right.Kind == BoundKind.AwaitExpression)
                {
                    return VisitAwaitExpression((BoundAwaitExpression)expression.Right, resultPlace: expression.Left);
                }
            }

            BoundExpression expr = (BoundExpression)this.Visit(node.Expression);
            return (expr != null) ? node.Update(expr) : (BoundStatement)F.StatementList();
        }

        public sealed override BoundNode VisitAwaitExpression(BoundAwaitExpression node)
        {
            // await expressions must, by now, have been moved to the top level.
            throw ExceptionUtilities.Unreachable();
        }

        public sealed override BoundNode VisitBadExpression(BoundBadExpression node)
        {
            // Cannot recurse into BadExpression
            return node;
        }

        private BoundBlock VisitAwaitExpression(BoundAwaitExpression node, BoundExpression resultPlace)
        {
            var expression = (BoundExpression)Visit(node.Expression);
            var awaitablePlaceholder = node.AwaitableInfo.AwaitableInstancePlaceholder;

            if (awaitablePlaceholder != null)
            {
                _placeholderMap.Add(awaitablePlaceholder, expression);
            }

            var getAwaiter = node.AwaitableInfo.IsDynamic ?
                MakeCallMaybeDynamic(expression, null, WellKnownMemberNames.GetAwaiter) :
                (BoundExpression)Visit(node.AwaitableInfo.GetAwaiter);

            resultPlace = (BoundExpression)Visit(resultPlace);
            MethodSymbol getResult = VisitMethodSymbol(node.AwaitableInfo.GetResult);
            MethodSymbol isCompletedMethod = ((object)node.AwaitableInfo.IsCompleted != null) ? VisitMethodSymbol(node.AwaitableInfo.IsCompleted.GetMethod) : null;
            TypeSymbol type = VisitType(node.Type);

            if (awaitablePlaceholder != null)
            {
                _placeholderMap.Remove(awaitablePlaceholder);
            }

            // The awaiter temp facilitates EnC method remapping and thus have to be long-lived.
            // It transfers the awaiter objects from the old version of the MoveNext method to the new one.
            Debug.Assert(node.Syntax.IsKind(SyntaxKind.AwaitExpression) || node.WasCompilerGenerated);

            var awaiterTemp = F.SynthesizedLocal(getAwaiter.Type, syntax: node.Syntax, kind: SynthesizedLocalKind.Awaiter);
            var awaitIfIncomplete = F.Block(
                    // temp $awaiterTemp = <expr>.GetAwaiter();
                    F.Assignment(
                        F.Local(awaiterTemp),
                        getAwaiter),

                    // hidden sequence point facilitates EnC method remapping, see explanation on SynthesizedLocalKind.Awaiter:
                    F.HiddenSequencePoint(),

                    // if(!($awaiterTemp.IsCompleted)) { ... }
                    F.If(
                        condition: F.Not(GenerateGetIsCompleted(awaiterTemp, isCompletedMethod)),
                        thenClause: GenerateAwaitForIncompleteTask(awaiterTemp, node.DebugInfo)));
            BoundExpression getResultCall = MakeCallMaybeDynamic(
                F.Local(awaiterTemp),
                getResult,
                WellKnownMemberNames.GetResult,
                resultsDiscarded: resultPlace == null);

            // [$resultPlace = ] $awaiterTemp.GetResult();
            BoundStatement getResultStatement = resultPlace != null && !type.IsVoidType() ?
                F.Assignment(resultPlace, getResultCall) :
                F.ExpressionStatement(getResultCall);

            return F.Block(
                ImmutableArray.Create(awaiterTemp),
                awaitIfIncomplete,
                getResultStatement);
        }

        public override BoundNode VisitAwaitableValuePlaceholder(BoundAwaitableValuePlaceholder node)
        {
            return _placeholderMap[node];
        }

        private BoundExpression MakeCallMaybeDynamic(
            BoundExpression receiver,
            MethodSymbol methodSymbol = null,
            string methodName = null,
            bool resultsDiscarded = false)
        {
            if ((object)methodSymbol != null)
            {
                // non-dynamic:
                Debug.Assert(receiver != null);

                return methodSymbol.IsStatic
                    ? F.StaticCall(methodSymbol.ContainingType, methodSymbol, receiver)
                    : F.Call(receiver, methodSymbol);
            }

            // dynamic:
            Debug.Assert(methodName != null);
            return _dynamicFactory.MakeDynamicMemberInvocation(
                methodName,
                receiver,
                typeArgumentsWithAnnotations: ImmutableArray<TypeWithAnnotations>.Empty,
                loweredArguments: ImmutableArray<BoundExpression>.Empty,
                argumentNames: ImmutableArray<string>.Empty,
                refKinds: ImmutableArray<RefKind>.Empty,
                hasImplicitReceiver: false,
                resultDiscarded: resultsDiscarded).ToExpression();
        }

        private BoundExpression GenerateGetIsCompleted(LocalSymbol awaiterTemp, MethodSymbol getIsCompletedMethod)
        {
            if (awaiterTemp.Type.IsDynamic())
            {
                return _dynamicFactory.MakeDynamicConversion(
                    _dynamicFactory.MakeDynamicGetMember(
                        F.Local(awaiterTemp),
                        WellKnownMemberNames.IsCompleted,
                        false).ToExpression(),
                    isExplicit: true,
                    isArrayIndex: false,
                    isChecked: false,
                    resultType: F.SpecialType(SpecialType.System_Boolean)).ToExpression();
            }

            return F.Call(F.Local(awaiterTemp), getIsCompletedMethod);
        }

        private BoundBlock GenerateAwaitForIncompleteTask(LocalSymbol awaiterTemp, BoundAwaitExpressionDebugInfo debugInfo)
        {
            var awaitSyntax = awaiterTemp.GetDeclaratorSyntax();
            AddResumableState(awaitSyntax, debugInfo.AwaitId, out StateMachineState stateNumber, out GeneratedLabelSymbol resumeLabel);

            TypeSymbol awaiterFieldType = awaiterTemp.Type.IsVerifierReference()
                ? F.SpecialType(SpecialType.System_Object)
                : awaiterTemp.Type;

            FieldSymbol awaiterField = GetAwaiterField(awaiterFieldType);

            var blockBuilder = ArrayBuilder<BoundStatement>.GetInstance();

            blockBuilder.Add(
                // this.state = cachedState = stateForLabel
                GenerateSetBothStates(stateNumber));

            blockBuilder.Add(
                    // Emit await yield point to be injected into PDB
                    F.NoOp(NoOpStatementFlavor.AwaitYieldPoint));

            blockBuilder.Add(
                    // this.<>t__awaiter = $awaiterTemp
                    F.Assignment(
                    F.Field(F.This(), awaiterField),
                    (TypeSymbol.Equals(awaiterField.Type, awaiterTemp.Type, TypeCompareKind.ConsiderEverything2))
                        ? F.Local(awaiterTemp)
                        : F.Convert(awaiterFieldType, F.Local(awaiterTemp))));

            // DOTNExT: Add checkpoint call before suspension for [Persistable] methods
            // Only if persistence types were resolved (persistenceServiceLocal was created)
            if (_enablePersistence && _persistenceServiceLocal is not null)
            {
                Log($"[DOTNExT-Roslyn] GenerateAwaitForIncompleteTask: Adding checkpoint call for state {stateNumber}");
                blockBuilder.Add(GenerateCheckpointCall(stateNumber));
            }
            else if (_enablePersistence)
            {
                Log($"[DOTNExT-Roslyn] GenerateAwaitForIncompleteTask: SKIPPING checkpoint - _persistenceServiceLocal is null!");
            }

            blockBuilder.Add(awaiterTemp.Type.IsDynamic()
                ? GenerateAwaitOnCompletedDynamic(awaiterTemp)
                : GenerateAwaitOnCompleted(awaiterTemp.Type, awaiterTemp));

            blockBuilder.Add(
                GenerateReturn(false));

            if (F.Compilation.Options.EnableEditAndContinue)
            {
                for (int i = 0; i < debugInfo.ReservedStateMachineCount; i++)
                {
                    AddResumableState(awaitSyntax, new AwaitDebugId((byte)(debugInfo.AwaitId.RelativeStateOrdinal + 1 + i)), out _, out var dummyResumeLabel);
                    blockBuilder.Add(F.Label(dummyResumeLabel));
                }
            }

            blockBuilder.Add(
                F.Label(resumeLabel));

            blockBuilder.Add(
                    // Emit await resume point to be injected into PDB
                    F.NoOp(NoOpStatementFlavor.AwaitResumePoint));

            blockBuilder.Add(
                    // $awaiterTemp = this.<>t__awaiter   or   $awaiterTemp = (AwaiterType)this.<>t__awaiter
                    // $this.<>t__awaiter = null;
                    F.Assignment(
                    F.Local(awaiterTemp),
                    TypeSymbol.Equals(awaiterTemp.Type, awaiterField.Type, TypeCompareKind.ConsiderEverything2)
                        ? F.Field(F.This(), awaiterField)
                        : F.Convert(awaiterTemp.Type, F.Field(F.This(), awaiterField))));

            blockBuilder.Add(
                F.Assignment(F.Field(F.This(), awaiterField), F.NullOrDefault(awaiterField.Type)));

            blockBuilder.Add(
                    // this.state = cachedState = NotStartedStateMachine
                    GenerateSetBothStates(StateMachineState.NotStartedOrRunningState));

            return F.Block(blockBuilder.ToImmutableAndFree());
        }

        private BoundStatement GenerateAwaitOnCompletedDynamic(LocalSymbol awaiterTemp)
        {
            //  temp $criticalNotifyCompletedTemp = $awaiterTemp as ICriticalNotifyCompletion
            //  if ($criticalNotifyCompletedTemp != null)
            //  {
            //    this.builder.AwaitUnsafeOnCompleted<ICriticalNotifyCompletion,TSM>(
            //      ref $criticalNotifyCompletedTemp,
            //      ref this)
            //  }
            //  else
            //  {
            //    temp $notifyCompletionTemp = (INotifyCompletion)$awaiterTemp
            //    this.builder.AwaitOnCompleted<INotifyCompletion,TSM>(ref $notifyCompletionTemp, ref this)
            //    free $notifyCompletionTemp
            //  }
            //  free $criticalNotifyCompletedTemp

            var criticalNotifyCompletedTemp = F.SynthesizedLocal(
                F.WellKnownType(WellKnownType.System_Runtime_CompilerServices_ICriticalNotifyCompletion),
                null);

            var notifyCompletionTemp = F.SynthesizedLocal(
                F.WellKnownType(WellKnownType.System_Runtime_CompilerServices_INotifyCompletion),
                null);

            LocalSymbol thisTemp = (F.CurrentType.TypeKind == TypeKind.Class) ? F.SynthesizedLocal(F.CurrentType) : null;

            var blockBuilder = ArrayBuilder<BoundStatement>.GetInstance();

            blockBuilder.Add(
                F.Assignment(
                    F.Local(criticalNotifyCompletedTemp),
                        // Use reference conversion rather than dynamic conversion:
                        F.As(F.Local(awaiterTemp), criticalNotifyCompletedTemp.Type)));

            if (thisTemp != null)
            {
                blockBuilder.Add(F.Assignment(F.Local(thisTemp), F.This()));
            }

            blockBuilder.Add(
                F.If(
                    condition: F.ObjectEqual(F.Local(criticalNotifyCompletedTemp), F.Null(criticalNotifyCompletedTemp.Type)),

                    thenClause: F.Block(
                        ImmutableArray.Create(notifyCompletionTemp),
                        F.Assignment(
                            F.Local(notifyCompletionTemp),
                                // Use reference conversion rather than dynamic conversion:
                                F.Convert(notifyCompletionTemp.Type, F.Local(awaiterTemp), Conversion.ExplicitReference)),
                        F.ExpressionStatement(
                            F.Call(
                                F.Field(F.This(), _asyncMethodBuilderField),
                                _asyncMethodBuilderMemberCollection.AwaitOnCompleted.Construct(
                                    notifyCompletionTemp.Type,
                                    F.This().Type),
                                F.Local(notifyCompletionTemp), F.This(thisTemp))),
                        F.Assignment(
                            F.Local(notifyCompletionTemp),
                            F.NullOrDefault(notifyCompletionTemp.Type))),

                    elseClauseOpt: F.Block(
                        F.ExpressionStatement(
                            F.Call(
                                F.Field(F.This(), _asyncMethodBuilderField),
                                _asyncMethodBuilderMemberCollection.AwaitUnsafeOnCompleted.Construct(
                                    criticalNotifyCompletedTemp.Type,
                                    F.This().Type),
                                F.Local(criticalNotifyCompletedTemp), F.This(thisTemp))))));

            blockBuilder.Add(
                F.Assignment(
                    F.Local(criticalNotifyCompletedTemp),
                    F.NullOrDefault(criticalNotifyCompletedTemp.Type)));

            return F.Block(
                SingletonOrPair(criticalNotifyCompletedTemp, thisTemp),
                blockBuilder.ToImmutableAndFree());
        }

        private BoundStatement GenerateAwaitOnCompleted(TypeSymbol loweredAwaiterType, LocalSymbol awaiterTemp)
        {
            // this.builder.AwaitOnCompleted<TAwaiter,TSM>(ref $awaiterTemp, ref this)
            //    or
            // this.builder.AwaitOnCompleted<TAwaiter,TSM>(ref $awaiterArrayTemp[0], ref this)

            LocalSymbol thisTemp = (F.CurrentType.TypeKind == TypeKind.Class) ? F.SynthesizedLocal(F.CurrentType) : null;

            var discardedUseSiteInfo = CompoundUseSiteInfo<AssemblySymbol>.Discarded;
            var useUnsafeOnCompleted = F.Compilation.Conversions.ClassifyImplicitConversionFromType(
                loweredAwaiterType,
                F.Compilation.GetWellKnownType(WellKnownType.System_Runtime_CompilerServices_ICriticalNotifyCompletion),
                ref discardedUseSiteInfo).IsImplicit;

            var onCompleted = (useUnsafeOnCompleted ?
                _asyncMethodBuilderMemberCollection.AwaitUnsafeOnCompleted :
                _asyncMethodBuilderMemberCollection.AwaitOnCompleted).Construct(loweredAwaiterType, F.This().Type);
            if (_asyncMethodBuilderMemberCollection.CheckGenericMethodConstraints)
            {
                onCompleted.CheckConstraints(new ConstraintsHelper.CheckConstraintsArgs(F.Compilation, F.Compilation.Conversions, includeNullability: false, F.Syntax.Location, this.Diagnostics));
            }

            BoundExpression result =
                F.Call(
                    F.Field(F.This(), _asyncMethodBuilderField),
                    onCompleted,
                    F.Local(awaiterTemp), F.This(thisTemp));

            if (thisTemp != null)
            {
                result = F.Sequence(
                    ImmutableArray.Create(thisTemp),
                    ImmutableArray.Create<BoundExpression>(F.AssignmentExpression(F.Local(thisTemp), F.This())),
                    result);
            }

            return F.ExpressionStatement(result);
        }

        private static ImmutableArray<LocalSymbol> SingletonOrPair(LocalSymbol first, LocalSymbol secondOpt)
        {
            return (secondOpt == null) ? ImmutableArray.Create(first) : ImmutableArray.Create(first, secondOpt);
        }

        public sealed override BoundNode VisitReturnStatement(BoundReturnStatement node)
        {
            if (node.ExpressionOpt != null)
            {
                Debug.Assert(_method.IsAsyncEffectivelyReturningGenericTask(F.Compilation));
                return F.Block(
                    F.Assignment(F.Local(_exprRetValue), (BoundExpression)Visit(node.ExpressionOpt)),
                    F.Goto(_exprReturnLabel));
            }

            return F.Goto(_exprReturnLabel);
        }
        #endregion Visitors

        #region DOTNExT Persistence Methods
        /// <summary>
        /// Initializes the persistence service local variable early, before body processing.
        /// This must be called before VisitBody so that await expression processing can use
        /// _persistenceServiceLocal for checkpoint injection.
        /// </summary>
        private void InitializePersistenceServiceLocal()
        {
            Debug.Assert(_enablePersistence);

            var persistenceServiceType = GetPersistenceServiceType();
            if (persistenceServiceType is null)
            {
                Log($"[DOTNExT-Roslyn] InitializePersistenceServiceLocal: FAILED - GetPersistenceServiceType returned null");
                return;
            }

            _persistenceServiceLocal = F.SynthesizedLocal(
                persistenceServiceType,
                syntax: F.Syntax,
                kind: SynthesizedLocalKind.LoweringTemp);

            Log($"[DOTNExT-Roslyn] InitializePersistenceServiceLocal: Created _persistenceServiceLocal for {_persistenceMethodId}");
        }

        /// <summary>
        /// Generates code to check for and apply persisted state restoration at MoveNext start.
        ///
        /// Generated code pattern:
        /// <code>
        /// var persistenceService = AsyncPersistenceContext.Current;
        /// if (persistenceService != null &amp;&amp; cachedState == -1)
        /// {
        ///     var restoredState = persistenceService.TryRestore(this, methodId);
        ///     if (restoredState >= 0)
        ///     {
        ///         cachedState = restoredState;
        ///         this.&lt;&gt;1__state = restoredState;
        ///     }
        /// }
        /// </code>
        /// </summary>
        private BoundStatement GeneratePersistenceRestorationCheck()
        {
            Debug.Assert(_enablePersistence);
            Debug.Assert(_persistenceMethodId != null);

            Log($"[DOTNExT-Roslyn] GeneratePersistenceRestorationCheck called for {_persistenceMethodId}");

            // _persistenceServiceLocal should already be created by InitializePersistenceServiceLocal
            if (_persistenceServiceLocal is null)
            {
                Log($"[DOTNExT-Roslyn]   FAILED: _persistenceServiceLocal was not initialized");
                return F.StatementList();
            }
            Log($"[DOTNExT-Roslyn]   Using pre-initialized _persistenceServiceLocal");

            var restoredStateLocal = F.SynthesizedLocal(
                F.SpecialType(SpecialType.System_Int32),
                syntax: F.Syntax,
                kind: SynthesizedLocalKind.LoweringTemp);

            var asyncPersistenceContextType = GetAsyncPersistenceContextType();
            if (asyncPersistenceContextType is null)
            {
                Log($"[DOTNExT-Roslyn]   FAILED: GetAsyncPersistenceContextType returned null");
                return F.StatementList();
            }
            Log($"[DOTNExT-Roslyn]   Got asyncPersistenceContextType: {asyncPersistenceContextType.ToDisplayString()}");

            // Get the Current property getter
            var currentProperty = asyncPersistenceContextType.GetMembers("Current")
                .OfType<PropertySymbol>()
                .FirstOrDefault();

            if (currentProperty?.GetMethod is null)
            {
                Log($"[DOTNExT-Roslyn]   FAILED: Current property or getter not found");
                return F.StatementList();
            }
            Log($"[DOTNExT-Roslyn]   Got Current property getter");

            // Try to get the generic TryRestore<T>(ref T, string) method first (preferred - no boxing)
            // BUT: Only use generic method for STRUCT state machines - classes don't support ref this
            var genericTryRestoreMethod = GetGenericTryRestoreMethod();
            MethodSymbol? tryRestoreMethod;
            bool useGenericMethod = false;

            // Check if state machine is a value type (struct) - only then can we use ref this
            bool isStructStateMachine = F.CurrentType.IsValueType;
            Log($"[DOTNExT-Roslyn]   State machine is {(isStructStateMachine ? "STRUCT" : "CLASS")}");

            if (genericTryRestoreMethod is not null && isStructStateMachine)
            {
                // Construct the closed generic method with the state machine type
                tryRestoreMethod = genericTryRestoreMethod.Construct(F.CurrentType);
                useGenericMethod = true;
                Log($"[DOTNExT-Roslyn]   Got GENERIC TryRestore<{F.CurrentType.Name}> method - no struct boxing!");
            }
            else
            {
                // Fall back to old non-generic method
                // For classes: this is fine - no boxing issue since classes are reference types
                // For structs: this has boxing issues but is fallback if generic not available
#pragma warning disable CS0618 // Type or member is obsolete
                tryRestoreMethod = GetTryRestoreMethod();
#pragma warning restore CS0618
                if (isStructStateMachine)
                {
                    Log($"[DOTNExT-Roslyn]   WARNING: Using non-generic TryRestore for STRUCT - boxing will occur!");
                }
                else
                {
                    Log($"[DOTNExT-Roslyn]   Using non-generic TryRestore for CLASS - no boxing issue");
                }
            }

            if (tryRestoreMethod is null)
            {
                Log($"[DOTNExT-Roslyn]   FAILED: TryRestore method not found");
                return F.StatementList();
            }
            Log($"[DOTNExT-Roslyn]   Got TryRestore method - persistence restoration check will be generated");

            var statements = ArrayBuilder<BoundStatement>.GetInstance();

            // var persistenceService = AsyncPersistenceContext.Current;
            statements.Add(F.Assignment(
                F.Local(_persistenceServiceLocal),
                F.Call(null, currentProperty.GetMethod)));

            // Build the inner restore block
            var restoreStatements = ArrayBuilder<BoundStatement>.GetInstance();

            if (useGenericMethod)
            {
                // Generic method: var restoredState = persistenceService.TryRestore<TStateMachine>(ref this, methodId);
                // This avoids boxing for struct state machines
                // Note: F.Call handles ref parameters automatically based on the method symbol's parameter definitions
                restoreStatements.Add(F.Assignment(
                    F.Local(restoredStateLocal),
                    F.Call(
                        F.Local(_persistenceServiceLocal),
                        tryRestoreMethod,
                        F.This(),
                        F.Literal(_persistenceMethodId))));
            }
            else
            {
                // Old non-generic method: var restoredState = persistenceService.TryRestore((object)this, methodId);
                restoreStatements.Add(F.Assignment(
                    F.Local(restoredStateLocal),
                    F.Call(
                        F.Local(_persistenceServiceLocal),
                        tryRestoreMethod,
                        F.Convert(F.SpecialType(SpecialType.System_Object), F.This()),
                        F.Literal(_persistenceMethodId))));
            }

            // DOTNExT: After restoration, we must NOT set cachedState to the restored value.
            // If we did, the switch statement would jump to the awaiter continuation (case N),
            // which expects awaiter.GetResult() - but awaiters can't be serialized.
            // Instead: reset state to -1 (not started) so workflow re-runs from beginning.
            // The restored field values (intermediate results) are preserved, so idempotent
            // operations will produce the same results quickly.
            restoreStatements.Add(F.If(
                condition: F.Binary(
                    BinaryOperatorKind.IntGreaterThanOrEqual,
                    F.SpecialType(SpecialType.System_Boolean),
                    F.Local(restoredStateLocal),
                    F.Literal(0)),
                thenClause: F.Block(
                    // Reset state to -1 so workflow starts from beginning
                    // (Don't update cachedState - leave it at -1 to trigger fresh start)
                    F.Assignment(F.Field(F.This(), stateField), F.Literal(StateMachineState.NotStartedOrRunningState)))));

            var restoreBlock = F.Block(
                ImmutableArray.Create(restoredStateLocal),
                restoreStatements.ToImmutableAndFree());

            // if (persistenceService != null && cachedState == -1) { ... }
            statements.Add(F.If(
                condition: F.Binary(
                    BinaryOperatorKind.LogicalBoolAnd,
                    F.SpecialType(SpecialType.System_Boolean),
                    F.Binary(
                        BinaryOperatorKind.ObjectNotEqual,
                        F.SpecialType(SpecialType.System_Boolean),
                        F.Local(_persistenceServiceLocal),
                        F.Null(_persistenceServiceLocal.Type)),
                    F.Binary(
                        BinaryOperatorKind.IntEqual,
                        F.SpecialType(SpecialType.System_Boolean),
                        F.Local(cachedState),
                        F.Literal(StateMachineState.NotStartedOrRunningState))),
                thenClause: restoreBlock));

            // Log detailed description of generated restoration code
            string restorationDesc;
            if (useGenericMethod)
            {
                restorationDesc = $@"Generated GENERIC restoration check (STRUCT state machine):
  var persistenceService = AsyncPersistenceContext.Current;
  if (persistenceService != null && cachedState == -1)
  {{
      var restoredState = persistenceService.TryRestore<{F.CurrentType.Name}>(ref this, ""{_persistenceMethodId}"");
      if (restoredState >= 0)
      {{
          // Reset state to -1 so workflow re-runs from beginning
          // (cachedState stays -1 - don't jump to awaiter continuation!)
          this.<>1__state = -1;
      }}
  }}
  NOTE: Using generic TryRestore<T>(ref T) - NO STRUCT BOXING!
  NOTE: Workflow re-runs from start with restored field values (awaiters can't be serialized).";
            }
            else if (isStructStateMachine)
            {
                restorationDesc = $@"Generated NON-GENERIC restoration check (STRUCT - BOXING ISSUE!):
  var persistenceService = AsyncPersistenceContext.Current;
  if (persistenceService != null && cachedState == -1)
  {{
      var restoredState = persistenceService.TryRestore((object)this, ""{_persistenceMethodId}"");
      if (restoredState >= 0)
      {{
          // Reset state to -1 so workflow re-runs from beginning
          this.<>1__state = -1;
      }}
  }}
  WARNING: Using non-generic TryRestore on STRUCT - boxing will lose restored values!
  NOTE: Workflow re-runs from start (awaiters can't be serialized).";
            }
            else
            {
                restorationDesc = $@"Generated NON-GENERIC restoration check (CLASS state machine):
  var persistenceService = AsyncPersistenceContext.Current;
  if (persistenceService != null && cachedState == -1)
  {{
      var restoredState = persistenceService.TryRestore((object)this, ""{_persistenceMethodId}"");
      if (restoredState >= 0)
      {{
          // Reset state to -1 so workflow re-runs from beginning
          // (cachedState stays -1 - don't jump to awaiter continuation!)
          this.<>1__state = -1;
      }}
  }}
  NOTE: CLASS state machines work fine with object boxing - no data loss.
  NOTE: Workflow re-runs from start with restored field values (awaiters can't be serialized).";
            }
            LogGeneratedCodeDescription("Restoration Check", restorationDesc);

            return F.Block(statements.ToImmutableAndFree());
        }

        /// <summary>
        /// Generates checkpoint call before await suspension.
        ///
        /// Generated code pattern:
        /// <code>
        /// if (persistenceService != null)
        /// {
        ///     persistenceService.Checkpoint(this, stateNumber, methodId);
        /// }
        /// </code>
        /// </summary>
        private BoundStatement GenerateCheckpointCall(StateMachineState stateNumber)
        {
            Debug.Assert(_enablePersistence);
            Debug.Assert(_persistenceServiceLocal != null);
            Debug.Assert(_persistenceMethodId != null);

            var checkpointMethod = GetCheckpointMethod();
            if (checkpointMethod is null)
            {
                return F.StatementList();
            }

            var persistenceServiceType = GetPersistenceServiceType();
            if (persistenceServiceType is null)
            {
                return F.StatementList();
            }

            // Log checkpoint generation
            LogGeneratedCodeDescription("Checkpoint", $@"Generated checkpoint call at state {(int)stateNumber}:
  if (persistenceService != null)
  {{
      persistenceService.Checkpoint((object)this, {(int)stateNumber}, ""{_persistenceMethodId}"");
  }}
  NOTE: Checkpoint uses object boxing (acceptable for checkpoint - data is serialized)");

            return F.If(
                condition: F.Binary(
                    BinaryOperatorKind.ObjectNotEqual,
                    F.SpecialType(SpecialType.System_Boolean),
                    F.Local(_persistenceServiceLocal),
                    F.Null(persistenceServiceType)),
                thenClause: F.ExpressionStatement(
                    F.Call(
                        F.Local(_persistenceServiceLocal),
                        checkpointMethod,
                        F.Convert(F.SpecialType(SpecialType.System_Object), F.This()),
                        F.Literal((int)stateNumber),
                        F.Literal(_persistenceMethodId))));
        }

        /// <summary>
        /// Gets the DOTNExT.Persistence.AsyncPersistenceContext type.
        /// </summary>
        private NamedTypeSymbol? GetAsyncPersistenceContextType()
        {
            return _asyncPersistenceContextType ??= F.Compilation.GetTypeByMetadataName(
                "DOTNExT.Persistence.AsyncPersistenceContext");
        }

        /// <summary>
        /// Gets the DOTNExT.Persistence.IAsyncPersistenceService type.
        /// </summary>
        private NamedTypeSymbol? GetPersistenceServiceType()
        {
            return _persistenceServiceType ??= F.Compilation.GetTypeByMetadataName(
                "DOTNExT.Persistence.IAsyncPersistenceService");
        }

        /// <summary>
        /// Gets the generic TryRestore&lt;TStateMachine&gt;(ref TStateMachine, string) method from IAsyncPersistenceService.
        /// This is the preferred method that avoids struct boxing issues.
        /// </summary>
        private MethodSymbol? GetGenericTryRestoreMethod()
        {
            var serviceType = GetPersistenceServiceType();
            // Find the generic TryRestore method (has 1 type parameter, 2 parameters with first being ref)
            return serviceType?.GetMembers("TryRestore")
                .OfType<MethodSymbol>()
                .FirstOrDefault(m => m.Arity == 1 && m.Parameters.Length == 2 &&
                                     m.Parameters[0].RefKind == RefKind.Ref);
        }

        /// <summary>
        /// Gets the old non-generic TryRestore(object, string) method from IAsyncPersistenceService.
        /// This method has struct boxing issues and is deprecated.
        /// </summary>
        [System.Obsolete("Use GetGenericTryRestoreMethod instead")]
        private MethodSymbol? GetTryRestoreMethod()
        {
            var serviceType = GetPersistenceServiceType();
            return serviceType?.GetMembers("TryRestore")
                .OfType<MethodSymbol>()
                .FirstOrDefault(m => m.Arity == 0); // Non-generic version
        }

        /// <summary>
        /// Gets the Checkpoint method from IAsyncPersistenceService.
        /// </summary>
        private MethodSymbol? GetCheckpointMethod()
        {
            var serviceType = GetPersistenceServiceType();
            return serviceType?.GetMembers("Checkpoint")
                .OfType<MethodSymbol>()
                .FirstOrDefault();
        }
        #endregion DOTNExT Persistence Methods
    }
}
