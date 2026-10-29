namespace Shared.ServiceKit
{
    /// <summary>
    /// Marker interface for microservice template implementations.
    /// Used for discovery and validation — a project that implements
    /// both IServiceDefinition and IMicroserviceTemplate is a fully
    /// conformant microservice in the Landscape Studio system.
    /// </summary>
    public interface IMicroserviceTemplate : IServiceDefinition
    {
        /// <summary>
        /// Returns the default port this microservice listens on.
        /// </summary>
        int DefaultPort { get; }

        /// <summary>
        /// Returns the health check path for this microservice (e.g. "/health").
        /// </summary>
        string HealthCheckPath { get; }
    }
}
