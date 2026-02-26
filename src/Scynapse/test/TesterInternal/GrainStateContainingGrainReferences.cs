using Scynapse.Runtime;

namespace TesterInternal
{
    [Serializable]
    [Scynapse.GenerateSerializer]
    public class GrainStateContainingGrainReferences
    {
        [Scynapse.Id(0)]
        public IAddressable Grain { get; set; }
        [Scynapse.Id(1)]
        public List<IAddressable> GrainList { get; set; }
        [Scynapse.Id(2)]
        public Dictionary<string, IAddressable> GrainDict { get; set; }

        public GrainStateContainingGrainReferences()
        {
            GrainList = new List<IAddressable>();
            GrainDict = new Dictionary<string, IAddressable>();
        }
    }
}
