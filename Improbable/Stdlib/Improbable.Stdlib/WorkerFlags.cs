using System.Linq;
using Improbable.Worker.CInterop;

namespace Improbable.Stdlib
{
    public static class WorkerFlags
    {
        public static bool TryGetWorkerFlagChange(this OpList opList, string flagName, out string newValue)
        {
            newValue = string.Empty;
            var found = false;
            foreach (var op in opList.OfOpType<FlagUpdateOp>().Where(op => op.Name == flagName))
            {
                newValue = op.Value;
                // Don't break early,  ensure that the last received flag update is the one that's applied.
                found = true;
            }

            return found;
        }
    }
}
