using Microsoft.CodeAnalysis;
using System;

namespace Scynapse.CodeGenerator
{
    public class ScynapseGeneratorDiagnosticAnalysisException : Exception
    {
        public ScynapseGeneratorDiagnosticAnalysisException(Diagnostic diagnostic) : base(diagnostic.GetMessage())
        {
            Diagnostic = diagnostic;
        }

        public Diagnostic Diagnostic { get; }
    }
}
