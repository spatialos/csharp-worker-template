using System.Collections.Generic;
using CommandLine;

namespace Improbable.Stdlib
{
    public interface IWorkerOptions
    {
        [Option("worker-name", Required = false, HelpText = "The name of the worker connecting to SpatialOS.")]
        string? WorkerName { get; set; }

        [Option("logfile", Required = false, HelpText = "The full path to a logfile.")]
        string? LogFileName { get; set; }

        [Value(0)] IEnumerable<string> UnknownPositionalArguments { get; set; }
    }

    public interface IReceptionistOptions : IWorkerOptions
    {
        [Option("spatialos-host", Default = "localhost", HelpText = "The host to use to connect to SpatialOS.")]
        string SpatialOsHost { get; set; }

        [Option("spatialos-port", Default = (ushort) 7777, HelpText = "The port to use to connect to SpatialOS.")]
        ushort SpatialOsPort { get; set; }
    }

    public interface ILocatorOptions : IWorkerOptions
    {
        [Option("spatialos-host", Default = "locator.improbable.io", HelpText = "The host to use to connect to SpatialOS.")]
        string SpatialOsHost { get; set; }

        [Option("spatialos-port", Default = (ushort) 444, HelpText = "The port to use to connect to SpatialOS.")]
        ushort SpatialOsPort { get; set; }

        [Option("spatialos-use-insecure-connection")]
        bool UseInsecureConnection { get; set; }

        [Option("dev-token", HelpText = "Your Development Auth Token. For more information: \n" +
                                                     "https://docs.improbable.io/reference/14.1/shared/auth/development-authentication#create-a-developmentauthenticationtoken")]
        string DevToken { get; set; }

        [Option("display-name")]
        string DisplayName { get; set; }

        [Option("player-id")]
        string PlayerId { get; set; }

        [Option("project-name")]
        string ProjectName { get; set; }
    }
}
