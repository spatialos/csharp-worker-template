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
using Locator = Improbable.Worker.CInterop.Alpha.Locator;
using LocatorParameters = Improbable.Worker.CInterop.Alpha.LocatorParameters;

namespace Improbable.Stdlib
{
    public class WorkerConnection : IDisposable
    {
        private readonly ConcurrentDictionary<uint, TaskHandler> requestsToComplete = new ConcurrentDictionary<uint, TaskHandler>();
        private Connection connection;
        private Task metricsTask;
        private CancellationTokenSource metricsTcs = new CancellationTokenSource();
        private string workerId;
        private object connectionLock = new object();

        private WorkerConnection(Connection connection)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public string WorkerId
        {
            get
            {
                if (string.IsNullOrEmpty(workerId))
                {
                    // ReSharper disable once InconsistentlySynchronizedField
                    workerId = connection.GetWorkerId();
                }

                return workerId;
            }
        }

        public void Dispose()
        {
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

        public static Task<WorkerConnection> ConnectAsync(string host, ushort port, string workerName, ConnectionParameters connectionParameters, CancellationToken cancellation = default)
        {
            var tcs = new TaskCompletionSource<WorkerConnection>();
            if (cancellation.CanBeCanceled)
            {
                cancellation.Register(() => tcs.TrySetCanceled(cancellation));
            }

            Task.Factory.StartNew(() =>
            {
                try
                {
                    using (var future = Connection.ConnectAsync(host, port, workerName, connectionParameters))
                    {
                        Connection connection;
                        while (true)
                        {
                            cancellation.ThrowIfCancellationRequested();

                            if (future.TryGet(out connection, 50))
                            {
                                break;
                            }
                        }


                        if (connection.GetConnectionStatusCode() != ConnectionStatusCode.Success)
                        {
                            tcs.SetException(new Exception($"{connection.GetConnectionStatusCode()}: {connection.GetConnectionStatusCodeDetailString()}"));
                        }
                        else
                        {
                            tcs.SetResult(new WorkerConnection(connection));
                        }
                    }
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            }, cancellation);

            return tcs.Task;
        }

        private static Task<WorkerConnection> ConnectAsync(ILocatorOptions options, ConnectionParameters connectionParameters, CancellationToken cancellation = default)
        {
            var tcs = new TaskCompletionSource<WorkerConnection>();
            if (cancellation.CanBeCanceled)
            {
                cancellation.Register(() => tcs.TrySetCanceled(cancellation));
            }

            Task.Factory.StartNew(() =>
            {
                try
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
                        EnableLogging = connectionParameters.EnableProtocolLoggingAtStartup
                    };

                    using (var locator = new Locator(options.SpatialOsHost, options.SpatialOsPort, locatorParameters))
                    using (var future = locator.ConnectAsync(connectionParameters))
                    {
                        Connection connection;
                        while (true)
                        {
                            cancellation.ThrowIfCancellationRequested();

                            if (future.TryGet(out connection, 50))
                            {
                                break;
                            }
                        }

                        if (connection != null && connection.GetConnectionStatusCode() != ConnectionStatusCode.Success)
                        {
                            tcs.SetException(new Exception($"{connection.GetConnectionStatusCode()}: {connection.GetConnectionStatusCodeDetailString()}"));
                        }
                        else
                        {
                            tcs.SetResult(new WorkerConnection(connection));
                        }
                    }
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            }, cancellation);

            return tcs.Task;
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

                while (true)
                {
                    metricsTcs.Token.ThrowIfCancellationRequested();

                    foreach (var updater in updaterList)
                    {
                        updater.Invoke(metrics);
                    }

                    metrics.Load = await GetCpuUsageForProcess(metricsTcs.Token) / 100.0;

                    lock (connectionLock)
                    {
                        connection.SendMetrics(metrics);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), metricsTcs.Token);
                }
            }, metricsTcs.Token);
        }

        public void StopSendingMetrics()
        {
            metricsTcs?.Cancel();
            try
            {
                metricsTask?.Wait();
            }
            catch
            {
                // Do nothing
            }

            metricsTcs?.Dispose();

            metricsTask = null;
            metricsTcs = null;
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

        public void ProcessOpList(OpList opList)
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

        public void Send(EntityId entityId, SchemaCommandRequest request, uint? timeout, CommandParameters? parameters, Action<CommandResponses> complete, Action<StatusCode, string> fail)
        {
            ThrowCommandFailedIfNotConnected();

            uint requestId;
            lock (connectionLock)
            {
                requestId = connection.SendCommandRequest(entityId.Value, new CommandRequest(request), 1, timeout, parameters);
            }

            if (!requestsToComplete.TryAdd(requestId, new TaskHandler { Complete = complete, Fail = fail }))
            {
                throw new InvalidOperationException("Key already exists");
            }
        }

        public Task<ReserveEntityIdsResult> SendReserveEntityIdsRequest(uint numberOfEntityIds, uint? timeoutMillis = null, CancellationToken cancellation = default, TaskCreationOptions taskOptions = TaskCreationOptions.RunContinuationsAsynchronously)
        {
            ThrowCommandFailedIfNotConnected();

            lock (connectionLock)
            {
                return RecordTask(connection.SendReserveEntityIdsRequest(numberOfEntityIds, timeoutMillis), responses => new ReserveEntityIdsResult
                {
                    FirstEntityId = responses.ReserveEntityIds.FirstEntityId,
                    NumberOfEntityIds = responses.ReserveEntityIds.NumberOfEntityIds
                }, cancellation, taskOptions);
            }
        }

        public Task<EntityId?> SendCreateEntityRequest(Entity entity, EntityId? entityId = null, uint? timeoutMillis = null, CancellationToken cancellation = default, TaskCreationOptions taskOptions = TaskCreationOptions.RunContinuationsAsynchronously)
        {
            ThrowCommandFailedIfNotConnected();

            lock (connectionLock)
            {
                return RecordTask(connection.SendCreateEntityRequest(entity, entityId?.Value, timeoutMillis),
                    responses => responses.CreateEntity.EntityId.HasValue ? new EntityId(responses.CreateEntity.EntityId.Value) : (EntityId?) null
                    , cancellation, taskOptions);
            }
        }

        public Task<EntityId> SendDeleteEntityRequest(EntityId entityId, uint? timeoutMillis = null, CancellationToken cancellation = default, TaskCreationOptions taskOptions = TaskCreationOptions.RunContinuationsAsynchronously)
        {
            ThrowCommandFailedIfNotConnected();

            lock (connectionLock)
            {
                return RecordTask(connection.SendDeleteEntityRequest(entityId.Value, timeoutMillis),
                    responses => new EntityId(responses.DeleteEntity.EntityId)
                    , cancellation, taskOptions);
            }
        }

        public Task<EntityQueryResult> SendEntityQueryRequest(EntityQuery entityQuery, uint? timeoutMillis = null, CancellationToken cancellation = default, TaskCreationOptions taskOptions = TaskCreationOptions.RunContinuationsAsynchronously)
        {
            ThrowCommandFailedIfNotConnected();

            lock (connectionLock)
            {
                return RecordTask(connection.SendEntityQueryRequest(entityQuery, timeoutMillis), responses => new EntityQueryResult
                {
                    Results = responses.EntityQuery.Result.ToDictionary(kv => new EntityId(kv.Key), kv => kv.Value.DeepCopy()),
                    ResultCount = responses.EntityQuery.ResultCount
                }, cancellation, taskOptions);
            }
        }

        private Task<TResultType> RecordTask<TResultType>(uint id, Func<CommandResponses, TResultType> getResult, CancellationToken cancellation, TaskCreationOptions taskOptions)
        {
            var completion = new TaskCompletionSource<TResultType>(taskOptions);

            if (cancellation.CanBeCanceled)
            {
                cancellation.Register(() => completion.TrySetCanceled(cancellation));
            }

            void Complete(CommandResponses r)
            {
                completion.TrySetResult(getResult(r));
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

        public void SendCommandResponse(uint id, SchemaCommandResponse response)
        {
            if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
            {
                throw new Exception("Not connected to SpatialOS");
            }

            lock (connectionLock)
            {
                connection.SendCommandResponse(id, new CommandResponse(response));
            }
        }

        public void SendCommandFailure(uint requestId, string message)
        {
            if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
            {
                throw new Exception("Not connected to SpatialOS");
            }

            lock (connectionLock)
            {
                connection.SendCommandFailure(requestId, message);
            }
        }

        public void SendComponentUpdate(EntityId entityId, SchemaComponentUpdate update, UpdateParameters? updateParameters = null)
        {
            if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
            {
                throw new Exception("Not connected to SpatialOS");
            }

            lock (connectionLock)
            {
                connection.SendComponentUpdate(entityId.Value, new ComponentUpdate(update), updateParameters);
            }
        }

        public void SendMetrics(Metrics metrics)
        {
            if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
            {
                throw new Exception("Not connected to SpatialOS");
            }

            lock (connectionLock)
            {
                connection.SendMetrics(metrics);
            }
        }

        public ConnectionStatusCode GetConnectionStatusCode()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            return connection?.GetConnectionStatusCode() ?? ConnectionStatusCode.Cancelled;
        }

        /// <summary>
        ///     Returns an OpList
        /// </summary>
        /// <param name="timeout">
        ///     An empty OpList will be returned after the specified duration. Use <see cref="TimeSpan.Zero" />
        ///     to block.
        /// </param>
        public OpList GetOpList(TimeSpan timeout)
        {
            if (connection == null || GetConnectionStatusCode() != ConnectionStatusCode.Success)
            {
                CancelCommands();
                throw new Exception("Not connected to SpatialOS");
            }

            lock (connectionLock)
            {
                return new OpList(connection.GetOpList((uint) timeout.TotalMilliseconds));
            }
        }

        /// <summary>
        ///     Returns OpLists for as long as connected to SpatialOS.
        /// </summary>
        /// <param name="timeout">
        ///     An empty OpList will be returned after the specified duration. Use <see cref="TimeSpan.Zero" />
        ///     to block until new ops are available.
        /// </param>
        /// <param name="cancellation">Cancellation token.</param>
        public IEnumerable<OpList> GetOpLists(TimeSpan timeout, CancellationToken cancellation = default)
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();

                if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
                {
                    yield break;
                }

                OpList opList = null;

                try
                {
                    lock (connectionLock)
                    {
                        opList = new OpList(connection.GetOpList((uint) timeout.TotalMilliseconds));
                    }

                    yield return opList;
                }
                finally
                {
                    opList?.Dispose();

                    if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
                    {
                        CancelCommands();
                    }
                }
            }
        }

        public string GetWorkerFlag(string flagName)
        {
            if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
            {
                throw new Exception("Not connected to SpatialOS");
            }

            lock (connectionLock)
            {
                return connection.GetWorkerFlag(flagName);
            }
        }

        private void ThrowCommandFailedIfNotConnected()
        {
            if (GetConnectionStatusCode() != ConnectionStatusCode.Success)
            {
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
            await Task.Delay(500, cancellationToken);

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
