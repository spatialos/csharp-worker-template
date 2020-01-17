using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Improbable.Worker.CInterop;
using Improbable.Worker.CInterop.Alpha;
using Improbable.Worker.CInterop.Query;
using Locator = Improbable.Worker.CInterop.Locator;
using LocatorParameters = Improbable.Worker.CInterop.LocatorParameters;

namespace Improbable.Stdlib
{
    public class WorkerConnection : IDisposable
    {
        private readonly ConcurrentDictionary<long, TaskHandler> requestsToComplete = new ConcurrentDictionary<long, TaskHandler>();
        private Connection connection;
        private Task metricsTask;
        private CancellationTokenSource metricsCts = new CancellationTokenSource();
        private readonly object connectionLock = new object();
        private readonly CancellationTokenSource processOpsCts = new CancellationTokenSource();
        private readonly BlockingCollection<OpList> ops = new BlockingCollection<OpList>();
        private Task processOpsTask;

        private WorkerConnection(Connection connection)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            WorkerId = connection.GetWorkerId();

            StartBackgroundOpsGathering();
        }

        private void StartBackgroundOpsGathering()
        {
            processOpsTask = Task.Factory.StartNew(() =>
            {
                // We have to resort to polling here since connection.GetOpList doesn't provide a means of cancellation
                try
                {
                    while (!processOpsCts.Token.IsCancellationRequested)
                    {
                        if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
                        {
                            break;
                        }

                        OpList opList;
                        lock (connectionLock)
                        {
                            var rawOps = connection.GetOpList(1);
                            if (rawOps.GetOpCount() == 0)
                            {
                                continue;
                            }

                            opList = new OpList(rawOps);
                        }

                        // NB: finish processing the OpList before adding it to the queue, since it will be Disposed on whatever thread is enumerating the ops collection.
                        CompleteCommands(opList);
                        ops.Add(opList);
                    }
                }
                finally
                {
                    ops.CompleteAdding();
                }
            }, TaskCreationOptions.LongRunning);
        }

        public string WorkerId { get; }

        public void Dispose()
        {
            processOpsCts.Cancel();
            processOpsTask.Wait();

            // Cleanup pending ops
            while (ops.TryTake(out var op))
            {
                op.Dispose();
            }

            ops.Dispose();

            lock (connectionLock)
            {
                StopSendingMetrics();
                CancelCommands();

                connection?.Dispose();
                connection = null;
            }
        }

        public static Task<WorkerConnection> ConnectAsync(IWorkerOptions workerOptions, ConnectionParameters connectionParameters, CancellationToken cancellation = default)
        {
            switch (workerOptions)
            {
                case IReceptionistOptions receptionistOptions:
                    var workerName = workerOptions.WorkerName ?? $"{connectionParameters.WorkerType}-{Guid.NewGuid().ToString()}";
                    return ConnectAsync(receptionistOptions.SpatialOsHost, receptionistOptions.SpatialOsPort, workerName, connectionParameters, cancellation);

                case ILocatorOptions locatorOptions:
                    connectionParameters.Network.UseExternalIp = true;
                    return ConnectAsync(locatorOptions, connectionParameters, cancellation);

                default:
                    throw new NotImplementedException("Unrecognized option type: " + workerOptions.GetType());
            }
        }

        public static async Task<WorkerConnection> ConnectAsync(string host, ushort port, string workerName, ConnectionParameters connectionParameters, CancellationToken cancellation = default)
        {
            using (var future = Connection.ConnectAsync(host, port, workerName, connectionParameters))
            {
                var connection = await future.ToTask(cancellation).ConfigureAwait(false);

                if (connection.GetConnectionStatusCode() != ConnectionStatusCode.Success)
                {
                    throw new Exception($"{connection.GetConnectionStatusCode()}: {connection.GetConnectionStatusCodeDetailString()}");
                }

                return new WorkerConnection(connection);
            }
        }

        public static async Task<WorkerConnection> ConnectAsync(ILocatorOptions options, ConnectionParameters connectionParameters, CancellationToken cancellation = default)
        {
            var pit = GetDevelopmentPlayerIdentityToken(options.SpatialOsHost, options.SpatialOsPort, options.UseInsecureConnection, options.DevToken, options.PlayerId, options.DisplayName);
            var loginTokens = GetDevelopmentLoginTokens(options.SpatialOsHost, options.SpatialOsPort, options.UseInsecureConnection, connectionParameters.WorkerType, pit);
            var loginToken = loginTokens.First().LoginToken;

            var locatorParameters = new LocatorParameters
            {
                PlayerIdentity = new PlayerIdentityCredentials
                {
                    LoginToken = loginToken,
                    PlayerIdentityToken = pit
                },
                UseInsecureConnection = options.UseInsecureConnection,
                Logging = connectionParameters.ProtocolLogging,
                EnableLogging = connectionParameters.EnableProtocolLoggingAtStartup,
                CredentialsType = LocatorCredentialsType.PlayerIdentity,
                ProjectName = options.ProjectName
            };

            using (var locator = new Locator(options.SpatialOsHost, options.SpatialOsPort, locatorParameters))
            using (var future = locator.ConnectAsync(connectionParameters))
            {
                var connection = await future.ToTask(cancellation).ConfigureAwait(false);

                if (connection != null && connection.GetConnectionStatusCode() != ConnectionStatusCode.Success)
                {
                    throw new Exception($"{connection.GetConnectionStatusCode()}: {connection.GetConnectionStatusCodeDetailString()}");
                }

                return new WorkerConnection(connection);
            }
        }

        public void StartSendingMetrics(params Action<Metrics>[] updaterList)
        {
            if (metricsTask != null)
            {
                throw new InvalidOperationException("Metrics are already being sent");
            }

            metricsTask = Task.Factory.StartNew(async unused =>
            {
                var metrics = new Metrics();

                while (!metricsCts.IsCancellationRequested)
                {
                    foreach (var updater in updaterList)
                    {
                        updater.Invoke(metrics);
                    }

                    metrics.Load = await GetCpuUsageForProcess(metricsCts.Token).ConfigureAwait(false) / 100.0;

                    lock (connectionLock)
                    {
                        if (connection == null)
                        {
                            break;
                        }

                        connection.SendMetrics(metrics);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), metricsCts.Token).ConfigureAwait(false);
                }
            }, metricsCts.Token, TaskCreationOptions.LongRunning);
        }

        public void StopSendingMetrics()
        {
            metricsCts?.Cancel();
            metricsCts?.Dispose();

            metricsTask = null;
            metricsCts = null;
        }

        private static string GetDevelopmentPlayerIdentityToken(string host, ushort port, bool useInsecureConnection, string authToken, string playerId, string displayName)
        {
            using (var pit = DevelopmentAuthentication.CreateDevelopmentPlayerIdentityTokenAsync(
                host, port,
                new PlayerIdentityTokenRequest
                {
                    DevelopmentAuthenticationToken = authToken,
                    PlayerId = playerId,
                    DisplayName = displayName,
                    UseInsecureConnection = useInsecureConnection
                }))
            {
                var value = pit.Get();

                if (!value.HasValue)
                {
                    throw new AuthenticationException("Error received while retrieving a Player Identity Token: null result");
                }

                if (value.Value.Status.Code != ConnectionStatusCode.Success)
                {
                    throw new AuthenticationException($"Error received while retrieving a Player Identity Token: {value.Value.Status.Detail}");
                }

                return value.Value.PlayerIdentityToken;
            }
        }

        private static List<LoginTokenDetails> GetDevelopmentLoginTokens(string host, ushort port, bool useInsecureConnection, string workerType, string pit)
        {
            using (var tokens = DevelopmentAuthentication.CreateDevelopmentLoginTokensAsync(host, port,
                new LoginTokensRequest
                {
                    PlayerIdentityToken = pit,
                    WorkerType = workerType,
                    UseInsecureConnection = useInsecureConnection,
                }))
            {
                var value = tokens.Get();

                if (!value.HasValue)
                {
                    throw new AuthenticationException("Error received while retrieving Login Tokens: null result");
                }

                if (value.Value.Status.Code != ConnectionStatusCode.Success)
                {
                    throw new AuthenticationException($"Error received while retrieving Login Tokens: {value.Value.Status.Detail}");
                }

                if (value.Value.LoginTokens.Count == 0)
                {
                    throw new Exception("No deployment returned for this project.");
                }

                return value.Value.LoginTokens;
            }
        }

        private void CompleteCommands(OpList opList)
        {
            foreach (var op in opList.Ops)
            {
                switch (op.OpType)
                {
                    case OpType.ReserveEntityIdsResponse:
                        CompleteCommand(op.ReserveEntityIdsResponseOp);
                        break;
                    case OpType.CreateEntityResponse:
                        CompleteCommand(op.CreateEntityResponseOp);
                        break;
                    case OpType.DeleteEntityResponse:
                        CompleteCommand(op.DeleteEntityResponseOp);
                        break;
                    case OpType.EntityQueryResponse:
                        CompleteCommand(op.EntityQueryResponseOp);
                        break;
                    case OpType.CommandResponse:
                        CompleteCommand(op.CommandResponseOp);
                        break;
                }
            }
        }

        public void Send(EntityId entityId, uint componentId, uint commandIndex, SchemaCommandRequest request, uint? timeout, CommandParameters? parameters, Action<CommandResponses> complete, Action<StatusCode, string> fail)
        {
            long requestId;
            lock (connectionLock)
            {
                ThrowCommandFailedIfNotConnected();

                requestId = connection.SendCommandRequest(entityId.Value, new CommandRequest(componentId, commandIndex, request), timeout, parameters);
            }

            if (!requestsToComplete.TryAdd(requestId, new TaskHandler { Complete = complete, Fail = fail }))
            {
                throw new InvalidOperationException("Key already exists");
            }
        }

        public Task<ReserveEntityIdsResult> SendReserveEntityIdsRequest(uint numberOfEntityIds, uint? timeoutMillis = null, CancellationToken cancellation = default, TaskCreationOptions taskOptions = TaskCreationOptions.RunContinuationsAsynchronously)
        {
            lock (connectionLock)
            {
                ThrowCommandFailedIfNotConnected();

                return RecordTask(connection.SendReserveEntityIdsRequest(numberOfEntityIds, timeoutMillis), responses => new ReserveEntityIdsResult
                {
                    FirstEntityId = responses.ReserveEntityIds.FirstEntityId,
                    NumberOfEntityIds = responses.ReserveEntityIds.NumberOfEntityIds
                }, cancellation, taskOptions);
            }
        }

        public Task<EntityId?> SendCreateEntityRequest(Entity entity, EntityId? entityId = null, uint? timeoutMillis = null, CancellationToken cancellation = default, TaskCreationOptions taskOptions = TaskCreationOptions.RunContinuationsAsynchronously)
        {
            lock (connectionLock)
            {
                ThrowCommandFailedIfNotConnected();

                return RecordTask(connection.SendCreateEntityRequest(entity, entityId?.Value, timeoutMillis),
                    responses => responses.CreateEntity.EntityId.HasValue ? new EntityId(responses.CreateEntity.EntityId.Value) : (EntityId?) null
                    , cancellation, taskOptions);
            }
        }

        public Task<EntityId> SendDeleteEntityRequest(EntityId entityId, uint? timeoutMillis = null, CancellationToken cancellation = default, TaskCreationOptions taskOptions = TaskCreationOptions.RunContinuationsAsynchronously)
        {
            lock (connectionLock)
            {
                ThrowCommandFailedIfNotConnected();

                return RecordTask(connection.SendDeleteEntityRequest(entityId.Value, timeoutMillis),
                    responses => new EntityId(responses.DeleteEntity.EntityId)
                    , cancellation, taskOptions);
            }
        }

        public Task<EntityQueryResult> SendEntityQueryRequest(EntityQuery entityQuery, uint? timeoutMillis = null, CancellationToken cancellation = default, TaskCreationOptions taskOptions = TaskCreationOptions.RunContinuationsAsynchronously)
        {

            lock (connectionLock)
            {
                ThrowCommandFailedIfNotConnected();

                return RecordTask(connection.SendEntityQueryRequest(entityQuery, timeoutMillis), responses => new EntityQueryResult
                {
                    Results = responses.EntityQuery.Result.ToDictionary(kv => new EntityId(kv.Key), kv => kv.Value.DeepCopy()),
                    ResultCount = responses.EntityQuery.ResultCount
                }, cancellation, taskOptions);
            }
        }

        private Task<TResultType> RecordTask<TResultType>(long id, Func<CommandResponses, TResultType> getResult, CancellationToken cancellation, TaskCreationOptions taskOptions)
        {
            var completion = new TaskCompletionSource<TResultType>(taskOptions);

            if (cancellation.CanBeCanceled)
            {
                cancellation.Register(() => completion.TrySetCanceled(cancellation));
            }

            void Complete(CommandResponses r)
            {
                try
                {
                    completion.TrySetResult(getResult(r));
                }
                catch (Exception e)
                {
                    completion.TrySetException(new CommandFailedException(StatusCode.ApplicationError, e.Message));
                }
            }

            void Fail(StatusCode code, string message)
            {
                completion.TrySetException(new CommandFailedException(code, message));
            }

            if (!requestsToComplete.TryAdd(id, new TaskHandler { Complete = Complete, Fail = Fail }))
            {
                throw new InvalidOperationException("Key already exists");
            }

            return completion.Task;
        }

        private void CompleteCommand(ReserveEntityIdsResponseOp r)
        {
            if (requestsToComplete.TryRemove(r.RequestId, out var completer))
            {
                switch (r.StatusCode)
                {
                    case StatusCode.Success:
                        completer.Complete(new CommandResponses { ReserveEntityIds = r });
                        break;
                    default:
                        completer.Fail(r.StatusCode, r.Message);
                        break;
                }
            }
        }

        private void CompleteCommand(EntityQueryResponseOp r)
        {
            if (requestsToComplete.TryRemove(r.RequestId, out var completer))
            {
                switch (r.StatusCode)
                {
                    case StatusCode.Success:
                        completer.Complete(new CommandResponses { EntityQuery = r });
                        break;
                    default:
                        completer.Fail(r.StatusCode, r.Message);
                        break;
                }
            }
        }

        private void CompleteCommand(CommandResponseOp r)
        {
            if (requestsToComplete.TryRemove(r.RequestId, out var completer))
            {
                switch (r.StatusCode)
                {
                    case StatusCode.Success:
                        if (!r.Response.SchemaData.HasValue)
                        {
                            throw new ArgumentNullException(nameof(r.Response.SchemaData));
                        }

                        completer.Complete(new CommandResponses { UserCommand = r });
                        break;
                    default:
                        completer.Fail(r.StatusCode, r.Message);
                        break;
                }
            }
        }

        private void CompleteCommand(CreateEntityResponseOp r)
        {
            if (requestsToComplete.TryRemove(r.RequestId, out var completer))
            {
                switch (r.StatusCode)
                {
                    case StatusCode.Success:
                        completer.Complete(new CommandResponses { CreateEntity = r });
                        break;
                    default:
                        completer.Fail(r.StatusCode, r.Message);
                        break;
                }
            }
        }

        private void CompleteCommand(DeleteEntityResponseOp r)
        {
            if (requestsToComplete.TryRemove(r.RequestId, out var completer))
            {
                switch (r.StatusCode)
                {
                    case StatusCode.Success:
                        completer.Complete(new CommandResponses { DeleteEntity = r });
                        break;
                    default:
                        completer.Fail(r.StatusCode, r.Message);
                        break;
                }
            }
        }

        public void SendCommandResponse(long requestId, uint componentId, uint commandIndex, SchemaCommandResponse response)
        {
            lock (connectionLock)
            {
                ThrowIfNotConnected();

                connection.SendCommandResponse(requestId, new CommandResponse(componentId, commandIndex, response));
            }
        }

        public void SendCommandFailure(long requestId, string message)
        {
            lock (connectionLock)
            {
                ThrowIfNotConnected();

                connection.SendCommandFailure(requestId, message);
            }
        }

        public void SendComponentUpdate(EntityId entityId, uint componentId, SchemaComponentUpdate update, UpdateParameters? updateParameters = null)
        {
            lock (connectionLock)
            {
                ThrowIfNotConnected();

                connection.SendComponentUpdate(entityId.Value, new ComponentUpdate(componentId, update), updateParameters);
            }
        }

        public void SendMetrics(Metrics metrics)
        {
            lock (connectionLock)
            {
                ThrowIfNotConnected();

                connection.SendMetrics(metrics);
            }
        }

        public ConnectionStatusCode GetConnectionStatusCode()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            return connection?.GetConnectionStatusCode() ?? ConnectionStatusCode.Cancelled;
        }

        /// <summary>
        ///     Returns an <see cref="OpList"/> if available, or an empty <see cref="OpList"/> if not.
        /// </summary>
        public OpList GetOpList(TimeSpan timeout, CancellationToken cancellation = default)
        {
            var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellation, processOpsCts.Token);

            if (ops.TryTake(out var opList, timeout.Milliseconds, combined.Token))
            {
                return opList;
            }

            return OpList.Empty;
        }

        /// <summary>
        ///     Returns OpLists for as long as connected to SpatialOS.
        /// </summary>
        /// <param name="cancellation">Cancellation token.</param>
        public IEnumerable<OpList> GetOpLists(CancellationToken cancellation = default)
        {
            var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellation, processOpsCts.Token);

            foreach (var opList in ops.GetConsumingEnumerable(combined.Token))
            {
                if (cancellation.IsCancellationRequested)
                {
                    yield break;
                }

                if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
                {
                    CancelCommands();
                    yield break;
                }

                try
                {
                    yield return opList;
                }
                finally
                {
                    opList?.Dispose();
                }
            }
        }

        public string GetWorkerFlag(string flagName)
        {
            lock (connectionLock)
            {
                ThrowIfNotConnected();

                return connection.GetWorkerFlag(flagName);
            }
        }

        private void ThrowIfNotConnected()
        {
            if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
            {
                CancelCommands();

                throw new InvalidOperationException("Not connected to SpatialOS");
            }
        }

        private void ThrowCommandFailedIfNotConnected()
        {
            if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
            {
                CancelCommands();

                throw new CommandFailedException(StatusCode.Timeout, "Not connected to SpatialOS");
            }
        }

        private void CancelCommands()
        {
            while (!requestsToComplete.IsEmpty)
            {
                var keys = requestsToComplete.Keys.ToList();
                foreach (var k in keys)
                {
                    if (requestsToComplete.TryRemove(k, out var request))
                    {
                        request.Fail(StatusCode.ApplicationError, "Canceled");
                    }
                }
            }
        }

        private static async Task<double> GetCpuUsageForProcess(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            return cpuUsageTotal * 100;
        }

        public struct ReserveEntityIdsResult
        {
            public EntityId? FirstEntityId;
            public int NumberOfEntityIds;
        }

        public struct EntityQueryResult : IDisposable
        {
            public int ResultCount;
            public Dictionary<EntityId, Entity> Results;

            public void Dispose()
            {
                foreach (var pair in Results)
                {
                    pair.Value.Free();
                }
            }
        }

        public struct CommandResponses
        {
            public CreateEntityResponseOp CreateEntity;
            public ReserveEntityIdsResponseOp ReserveEntityIds;
            public DeleteEntityResponseOp DeleteEntity;
            public EntityQueryResponseOp EntityQuery;
            public CommandResponseOp UserCommand;
        }

        private class TaskHandler
        {
            public Action<CommandResponses> Complete;
            public Action<StatusCode, string> Fail;
        }
    }
}
