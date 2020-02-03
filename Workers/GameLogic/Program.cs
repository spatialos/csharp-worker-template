using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Improbable.Stdlib;
using Improbable.Worker.CInterop;
using McMaster.Extensions.CommandLineUtils;
using Serilog;
using OpList = Improbable.Stdlib.OpList;

namespace GameLogic
{
    internal class Program : IReceptionistOptions
    {
        private const string WorkerType = "GameLogic";

        [Option("--worker-name", Description = "The name of the worker connecting to SpatialOS.")]
        public string? WorkerName { get; set; }

        [Option("--logfile", Description = "The full path to a logfile.")]
        public string? LogFileName { get; set; }

        [Option("spatialos-host", Description = "The host to use to connect to SpatialOS.")]
        public string SpatialOsHost { get; set; } = "localhost";

        [Option("spatialos-port", Description = "The port to use to connect to SpatialOS.")]
        public ushort SpatialOsPort { get; set; } = 7777;

        private static Task<int> Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled exception");

                if (eventArgs.IsTerminating)
                {
                    Log.CloseAndFlush();
                }
            };

            try
            {
                return CommandLineApplication.ExecuteAsync<Program>(args);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private async Task RunAsync(CommandLineApplication app, CancellationToken token)
        {
            if (string.IsNullOrEmpty(LogFileName))
            {
                LogFileName = Path.Combine(Environment.CurrentDirectory, WorkerName ?? $"{WorkerType}.log");
            }

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(LogFileName)
                .CreateLogger();

            Log.Debug($"Opened logfile {LogFileName}");

            var connectionParameters = new ConnectionParameters
            {
                EnableProtocolLoggingAtStartup = true,
                ProtocolLogging = new ProtocolLoggingParameters
                {
                    LogPrefix = Path.ChangeExtension(LogFileName, string.Empty) + "-protocol"
                },
                WorkerType = WorkerType,
                DefaultComponentVtable = new ComponentVtable()
            };

            using (var connection = await WorkerConnection.ConnectAsync(this, connectionParameters, token).ConfigureAwait(false))
            {
                connection.StartSendingMetrics();

                foreach (var opList in connection.GetOpLists(token))
                {
                    ProcessOpList(opList);
                }
            }

            Log.Information("Disconnected from SpatialOS");
        }

        private static void ProcessOpList(OpList opList)
        {
            foreach (var disconnectOp in opList.OfOpType<DisconnectOp>())
            {
                Log.Information(disconnectOp.Reason);
            }
        }
    }
}
