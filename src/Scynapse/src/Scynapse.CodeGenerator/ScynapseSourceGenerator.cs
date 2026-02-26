using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Scynapse.CodeGenerator.Diagnostics;
using Scynapse.CodeGenerator.Model;

#pragma warning disable RS1035 // Do not use APIs banned for analyzers
namespace Scynapse.CodeGenerator
{
    [Generator]
    public class ScynapseSerializationSourceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                // DOTNExT: Allow generator to run in VS2022 for proper IntelliSense
                // The original Scynapse code skipped IDE execution for performance,
                // but this causes IntelliSense errors for generated partial members.
                // var processName = Process.GetCurrentProcess().ProcessName.ToLowerInvariant();
                // if (processName.Contains("devenv") || processName.Contains("servicehub"))
                // {
                //     return;
                // }

                if (!Debugger.IsAttached &&
                    context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.scynapse_designtimebuild", out var isDesignTimeBuild)
                    && string.Equals("true", isDesignTimeBuild, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.scynapse_attachdebugger", out var attachDebuggerOption)
                    && string.Equals("true", attachDebuggerOption, StringComparison.OrdinalIgnoreCase))
                {
                    Debugger.Launch();
                }

                var options = new CodeGeneratorOptions();
                if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.scynapse_immutableattributes", out var immutableAttributes) && immutableAttributes is { Length: > 0 })
                {
                    options.ImmutableAttributes.AddRange(immutableAttributes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList());
                }

                if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.scynapse_aliasattributes", out var aliasAttributes) && aliasAttributes is { Length: > 0 })
                {
                    options.AliasAttributes.AddRange(aliasAttributes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList());
                }

                if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.scynapse_idattributes", out var idAttributes) && idAttributes is { Length: > 0 })
                {
                    options.IdAttributes.AddRange(idAttributes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList());
                }

                if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.scynapse_generateserializerattributes", out var generateSerializerAttributes) && generateSerializerAttributes is { Length: > 0 })
                {
                    options.GenerateSerializerAttributes.AddRange(generateSerializerAttributes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList());
                }

                if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.scynapse_generatefieldids", out var generateFieldIds) && generateFieldIds is { Length: > 0 })
                {
                    if (Enum.TryParse(generateFieldIds, out GenerateFieldIds fieldIdOption))
                    {
                        options.GenerateFieldIds = fieldIdOption;
                    }
                }

                if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.scynapsegeneratecompatibilityinvokers", out var generateCompatInvokersValue)
                    && bool.TryParse(generateCompatInvokersValue, out var genCompatInvokers))
                {
                    options.GenerateCompatibilityInvokers = genCompatInvokers;
                }

                var codeGenerator = new CodeGenerator(context.Compilation, options);
                var syntax = codeGenerator.GenerateCode(context.CancellationToken);
                var sourceString = syntax.NormalizeWhitespace().ToFullString();
                var sourceText = SourceText.From(sourceString, Encoding.UTF8);
                context.AddSource($"{context.Compilation.AssemblyName ?? "assembly"}.nscynapse.g.cs", sourceText);
            }
            catch (Exception exception)
            {
                if (!HandleException(context, exception))
                {
                    throw;
                }
            }

            static bool HandleException(GeneratorExecutionContext context, Exception exception)
            {
                if (exception is ScynapseGeneratorDiagnosticAnalysisException analysisException)
                {
                    context.ReportDiagnostic(analysisException.Diagnostic);
                    return true;
                }

                context.ReportDiagnostic(UnhandledCodeGenerationExceptionDiagnostic.CreateDiagnostic(exception));
                Console.WriteLine(exception);
                Console.WriteLine(exception.StackTrace);
                return false;
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }
}
#pragma warning restore RS1035 // Do not use APIs banned for analyzers