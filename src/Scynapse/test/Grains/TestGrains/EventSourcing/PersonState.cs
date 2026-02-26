using TestGrainInterfaces;

namespace TestGrains
{
    [Serializable]
    [Scynapse.GenerateSerializer]
    public class PersonState
    {
        [Scynapse.Id(0)]
        public string FirstName { get; set; }
        [Scynapse.Id(1)]
        public string LastName { get; set; }
        [Scynapse.Id(2)]
        public GenderType Gender { get; set; }
        [Scynapse.Id(3)]
        public bool IsMarried { get; set; }

        public void Apply(PersonRegistered @event)
        {
            this.FirstName = @event.FirstName;
            this.LastName = @event.LastName;
            this.Gender = @event.Gender;
        }

        public void Apply(PersonMarried @event)
        {
            this.IsMarried = true;
        }

        public void Apply(PersonLastNameChanged @event)
        {
            this.LastName = @event.LastName;
        }
    }
}
