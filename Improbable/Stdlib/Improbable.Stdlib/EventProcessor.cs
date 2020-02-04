using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Improbable.Worker.CInterop;

namespace Improbable.Stdlib
{
    public class EventProcessor<T> : IOpProcessor where T : struct
    {
        public delegate bool GetEventsHandler(SchemaComponentUpdate update, out ImmutableArray<T> events);

        private readonly uint componentId;
        private readonly BlockingCollection<T> events = new BlockingCollection<T>();
        private readonly GetEventsHandler getEvents;

        public EventProcessor(uint componentId, GetEventsHandler getEvents)
        {
            this.componentId = componentId;
            this.getEvents = getEvents;
        }

        public void ProcessOpList(OpList opList)
        {
            foreach (var op in opList.OfOpType<ComponentUpdateOp>().OfComponent(componentId))
            {
                if (!op.Update.SchemaData.HasValue || !getEvents(op.Update.SchemaData.Value, out var newEvents))
                {
                    continue;
                }

                foreach (var evt in newEvents)
                {
                    events.Add(evt);
                }
            }
        }

        public IEnumerable<T> GetEvents(CancellationToken token = default)
        {
            return events.GetConsumingEnumerable(token);
        }
    }
}
