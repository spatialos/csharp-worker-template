using System.Collections.Generic;
using CommandLine;
using Improbable.Stdlib;

namespace GameLogic
{
    [Verb("receptionist", HelpText = "Connect to a deployment using the receptionist")]
    internal class ReceptionistOptions : IReceptionistOptions
    {
        public string? WorkerName { get; set; }
        public string? LogFileName { get; set; }

        [Value(0, Hidden = true)] // Stops "pos value 0" being shown when help is ran
        public IEnumerable<string> UnknownPositionalArguments { get; set; } = null!;
        public string SpatialOsHost { get; set; } = null!;
        public ushort SpatialOsPort { get; set; }
    }
}
