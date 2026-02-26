namespace UnitTests.DtosRefScynapse
{
    [Serializable]
    [GenerateSerializer]
    public class ClassReferencingScynapseTypeDto
    {
        static ClassReferencingScynapseTypeDto()
        {
            _ = typeof(IGrain).ToString();
        }

        [Id(0)]
        public string MyProperty { get; set; }
    }
}