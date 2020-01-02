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

#pragma warning disable 8618 // Non-nullable property '' is uninitialized.Consider declaring the property as nullable.
        [Value(0, Hidden = true)] // Stops "pos value 0" being shown when help is ran
        public IEnumerable<string> UnknownPositionalArguments { get; set; }
        public string SpatialOsHost { get; set; }
#pragma warning restore 8618
        public ushort SpatialOsPort { get; set; }
    }
}
