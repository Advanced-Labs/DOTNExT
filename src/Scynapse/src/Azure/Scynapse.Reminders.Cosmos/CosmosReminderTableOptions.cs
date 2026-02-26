namespace Scynapse.Reminders.Cosmos;

/// <summary>
/// Options for Azure Cosmos DB Reminder Storage.
/// </summary>
public class CosmosReminderTableOptions : CosmosOptions
{
    private const string SCYNAPSE_REMINDERS_CONTAINER = "ScynapseReminders";

    /// <summary>
    /// Initializes a new <see cref="CosmosReminderTableOptions"/> instance.
    /// </summary>
    public CosmosReminderTableOptions()
    {
        ContainerName = SCYNAPSE_REMINDERS_CONTAINER;
    }
}
