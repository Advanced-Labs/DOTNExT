using Microsoft.CodeAnalysis;

namespace Scynapse.CodeGenerator
{
    internal interface ICopierDescription
    {
        ITypeSymbol UnderlyingType { get; }
    }
}