namespace TestExtensions
{
    public abstract class ScynapseTestingBase
    {
        public static long GetRandomGrainId() => Random.Shared.Next();
    }
}