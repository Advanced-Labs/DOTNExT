using System;

namespace Scynapse
{
    /// <summary>
    /// Client interface for interacting with an Scynapse cluster.
    /// </summary>
    public interface IClusterClient : IGrainFactory
    {
        /// <summary>
        /// Gets the service provider used by this client.
        /// </summary>
        IServiceProvider ServiceProvider { get; }
    }
}