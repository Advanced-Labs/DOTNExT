using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Scynapse.Configuration;
using Scynapse.Reminders.AdoNet.Storage;

namespace Scynapse.Runtime.ReminderService
{
    internal sealed class AdoNetReminderTable : IReminderTable
    {
        private readonly AdoNetReminderTableOptions options;
        private readonly string serviceId;
        private RelationalScynapseQueries scynapseQueries;

        public AdoNetReminderTable(
            IOptions<ClusterOptions> clusterOptions, 
            IOptions<AdoNetReminderTableOptions> storageOptions)
        {
            this.serviceId = clusterOptions.Value.ServiceId;
            this.options = storageOptions.Value;
        }

        public async Task Init()
        {
            this.scynapseQueries = await RelationalScynapseQueries.CreateInstance(this.options.Invariant, this.options.ConnectionString);
        }

        public Task<ReminderTableData> ReadRows(GrainId grainId)
        {
            return this.scynapseQueries.ReadReminderRowsAsync(this.serviceId, grainId);
        }

        public Task<ReminderTableData> ReadRows(uint beginHash, uint endHash)
        {
            return this.scynapseQueries.ReadReminderRowsAsync(this.serviceId, beginHash, endHash);
        }

        public Task<ReminderEntry> ReadRow(GrainId grainId, string reminderName)
        {
            return this.scynapseQueries.ReadReminderRowAsync(this.serviceId, grainId, reminderName);
        }   
        
        public Task<string> UpsertRow(ReminderEntry entry)
        {
            if (entry.StartAt.Kind is DateTimeKind.Unspecified)
            {
                entry.StartAt = new DateTime(entry.StartAt.Ticks, DateTimeKind.Utc);
            }

            return this.scynapseQueries.UpsertReminderRowAsync(this.serviceId, entry.GrainId, entry.ReminderName, entry.StartAt, entry.Period);            
        }

        public Task<bool> RemoveRow(GrainId grainId, string reminderName, string eTag)
        {
            return this.scynapseQueries.DeleteReminderRowAsync(this.serviceId, grainId, reminderName, eTag);            
        }

        public Task TestOnlyClearTable()
        {
            return this.scynapseQueries.DeleteReminderRowsAsync(this.serviceId);
        }
    }
}
