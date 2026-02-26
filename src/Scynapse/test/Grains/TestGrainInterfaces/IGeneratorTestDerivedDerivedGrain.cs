namespace UnitTests.GrainInterfaces
{
    [Serializable]
    [Scynapse.GenerateSerializer]
    public class ReplaceArguments
    {
        [Scynapse.Id(0)]
        public string OldString { get; private set; }
        [Scynapse.Id(1)]
        public string NewString { get; private set; }

        public ReplaceArguments(string oldStr, string newStr)
        {
            OldString = oldStr;
            NewString = newStr;
        }
    }

    public interface IGeneratorTestDerivedDerivedGrain : IGeneratorTestDerivedGrain2
    {
        Task<string> StringNConcat(string[] strArray);
        Task<string> StringReplace(ReplaceArguments strs);
    }
}