using System.Threading.Tasks;
using Scynapse.Concurrency;
using Scynapse.Dashboard.Model;

namespace Scynapse.Dashboard.Core;

internal interface IDashboardRemindersGrain : IGrainWithIntegerKey
{
    Task<Immutable<ReminderResponse>> GetReminders(int pageNumber, int pageSize);
}
