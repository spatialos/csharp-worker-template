using System.Collections.Generic;
using CommandLine;
using Improbable.Stdlib;

namespace GameLogic
{
    [Verb("locator", HelpText = "Connect to a deployment using the locator")]
    internal class LocatorOptions : IWorkerOptions, ILocatorOptions
    {
        public string WorkerName { get; set; }
        public string LogFileName { get; set; }
        [Value(0, Hidden = true)] // Stops "pos value 0" being shown when help is ran
        public IEnumerable<string> UnknownPositionalArguments { get; set; }
        public string SpatialOsHost { get; set; }
        public ushort SpatialOsPort { get; set; }
        public string Token { get; set; }
        public string DisplayName { get; set; }
        public string PlayerId { get; set; }
        public string DeploymentName { get; set; }
    }
}
