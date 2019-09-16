using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Improbable.Worker.CInterop;

namespace Improbable.Stdlib
{
    public class EventProcessor<T> : IOpProcessor where T : struct
    {
        private readonly uint componentId;
        private readonly GetEventsHandler getEvents;
        private readonly ConcurrentQueue<T> eventBuffer = new ConcurrentQueue<T>();

        public delegate bool GetEventsHandler(SchemaComponentUpdate update, out ImmutableArray<T> events);

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
                    eventBuffer.Enqueue(evt);
                }
            }
        }

        public IEnumerable<T> GetEvents(TimeSpan timeout, CancellationToken cancellation = default)
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                while (eventBuffer.TryDequeue(out var evt))
                {
                    yield return evt;
                }

                Task.Delay(timeout, cancellation).Wait(cancellation);
            }
        }
    }
}
