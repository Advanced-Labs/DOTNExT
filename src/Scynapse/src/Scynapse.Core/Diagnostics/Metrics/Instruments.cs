using System.Diagnostics.Metrics;

namespace Scynapse.Runtime;

public static class Instruments
{
    public static readonly Meter Meter = new("Genesa.Scynapse");
}
