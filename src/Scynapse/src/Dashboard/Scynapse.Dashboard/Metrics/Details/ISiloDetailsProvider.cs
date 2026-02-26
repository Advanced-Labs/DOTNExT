using System.Threading.Tasks;
using Scynapse.Dashboard.Model;

namespace Scynapse.Dashboard.Metrics.Details;

internal interface ISiloDetailsProvider
{
    Task<SiloDetails[]> GetSiloDetails();
}
