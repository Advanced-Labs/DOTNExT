namespace TestGrainInterfaces
{
    public enum GenderType
    {
        Male,
        Female
    }

    [Serializable]
    [Scynapse.GenerateSerializer]
    public class PersonAttributes
    {
        [Scynapse.Id(0)]
        public string FirstName { get; set; }
        [Scynapse.Id(1)]
        public string LastName { get; set; }
        [Scynapse.Id(2)]
        public GenderType Gender { get; set; }
    }

    /// <summary>
    /// Scynapse grain communication interface IPerson
    /// </summary>
    public interface IPersonGrain : Scynapse.IGrainWithGuidKey
    {
        Task RegisterBirth(PersonAttributes person);
        Task Marry(IPersonGrain spouse);

        Task<PersonAttributes> GetTentativePersonalAttributes();

        // Tests

        Task RunTentativeConfirmedStateTest();
    }
}
