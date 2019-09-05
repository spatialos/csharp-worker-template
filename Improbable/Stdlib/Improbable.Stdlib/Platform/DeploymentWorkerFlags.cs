using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Improbable.Stdlib.Platform
{
    public readonly struct DeploymentWorkerFlags
    {
        [JsonProperty("workerType")]
        public readonly string WorkerType;

        [JsonProperty("flags")]
        public readonly ImmutableArray<Flag> Flags;

        public DeploymentWorkerFlags(string workerType, IEnumerable<Flag> flags)
        {
            if (flags == null)
            {
                throw new ArgumentNullException(nameof(flags));
            }

            WorkerType = workerType;
            Flags = ImmutableArray<Flag>.Empty;
            Flags = Flags.AddRange(flags);
        }
    }
}
