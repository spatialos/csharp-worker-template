using System.Collections.Generic;
using System.Linq;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.Stdlib.Project;

namespace Improbable.Stdlib.Platform
{
    public static class DeploymentWorkerFlagsExtensions
    {
        public static IEnumerable<WorkerFlag> AsPlatformFlags(this IEnumerable<DeploymentWorkerFlags> flags)
        {
            return flags.SelectMany(wf =>
                wf.Flags.Select(f => new WorkerFlag {Key = f.Name, Value = f.Value, WorkerType = wf.WorkerType}));
        }
    }
}
