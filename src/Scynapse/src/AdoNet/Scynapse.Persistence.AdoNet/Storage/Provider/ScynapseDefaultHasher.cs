namespace Scynapse.Storage
{
    /// <summary>
    /// A default implementation uses the same hash as Scynapse in grains placement.
    /// </summary>
    public sealed class ScynapseDefaultHasher: IHasher
    {
        /// <summary>
        /// <see cref="IHasher.Description"/>
        /// </summary>
        public string Description => $"The default Scynapse hash function ({nameof(StableHash)}).";

        /// <summary>
        /// <see cref="IHasher.Hash(byte[])"/>.
        /// </summary>
        public int Hash(byte[] data) => (int)StableHash.ComputeHash(data);
    }
}
