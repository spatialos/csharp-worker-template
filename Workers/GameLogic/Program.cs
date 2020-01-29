using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Improbable.Stdlib;
using Improbable.Worker.CInterop;
using Serilog;
using OpList = Improbable.Stdlib.OpList;

namespace GameLogic
{
    internal class Program
    {
        private const string WorkerType = "GameLogic";

        private static async Task<int> Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled exception");

                if (eventArgs.IsTerminating)
                {
                    Log.CloseAndFlush();
                }
            };

            IWorkerOptions? options = null;

            Parser.Default.ParseArguments<ReceptionistOptions>(args)
                .WithParsed(opts => options = opts);

            if (options == null)
            {
                return 1;
            }

            if (options.UnknownPositionalArguments.Any())
            {
                Console.Error.WriteLine($@"Unknown positional arguments: [{string.Join(", ", options.UnknownPositionalArguments)}]");
                return 1;
            }

            try
            {
                await RunAsync(options);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to run");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }

            return 0;
        }

        private static async Task RunAsync(IWorkerOptions options)
        {
            if (string.IsNullOrEmpty(options.LogFileName))
            {
                options.LogFileName = Path.Combine(Environment.CurrentDirectory, options.WorkerName ?? WorkerType + ".log");
            }

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(options.LogFileName)
                .CreateLogger();

            Log.Debug($"Opened logfile {options.LogFileName}");

            var connectionParameters = new ConnectionParameters
            {
                EnableProtocolLoggingAtStartup = true,
                ProtocolLogging = new ProtocolLoggingParameters
                {
                    LogPrefix = Path.ChangeExtension(options.LogFileName, string.Empty) + "-protocol"
                },
                WorkerType = WorkerType,
                DefaultComponentVtable = new ComponentVtable()
            };

            using (var connection = await WorkerConnection.ConnectAsync(options, connectionParameters).ConfigureAwait(false))
            {
                connection.StartSendingMetrics();

                foreach (var opList in connection.GetOpLists())
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
