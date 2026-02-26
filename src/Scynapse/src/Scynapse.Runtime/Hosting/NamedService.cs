using System;

namespace Scynapse.Runtime.Hosting
{
    internal class NamedService<TService>(string name)
    {
        public string Name { get; } = name;
    }
}
