namespace UnitTests.GrainInterfaces
{
    [Serializable]
    [Scynapse.GenerateSerializer]
    public class TestTypeA
    {
        [Scynapse.Id(0)]
        public ICollection<TestTypeA> Collection { get; set; }
    }
}
