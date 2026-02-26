namespace Scynapse.Journaling;

public interface IStateMachineStorageProvider
{
    IStateMachineStorage Create(IGrainContext grainContext);
}
