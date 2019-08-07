using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NpgsqlTypes;
using Serilog;

namespace Improbable.Postgres
{
    public class MetricsPusher : IDisposable
    {
        private CancellationTokenSource metricsCancellationTokenSource;
        private readonly PostgresOptions postgresOptions;
        private Task metricsPusherTask;

        public MetricsPusher(PostgresOptions options)
        {
            postgresOptions = options;
        }

        public void Dispose()
        {
            metricsCancellationTokenSource?.Cancel();
            metricsPusherTask?.Wait();

            metricsCancellationTokenSource?.Dispose();
            metricsPusherTask?.Dispose();

            metricsCancellationTokenSource = null;
            metricsPusherTask = null;

            GC.SuppressFinalize(this);
        }

        private void Scrape()
        {
            var currentCounts = Metrics.GetCounts();
            var currentTimings = Metrics.GetTimingStats();
            var observationTime = DateTime.UtcNow;

            try
            {
                using (var connection = new ConnectionWrapper(postgresOptions.ConnectionString))
                {
                    foreach (var metrics in currentCounts.Concat(currentTimings))
                    {
                        using (var cmd = connection.Command("INSERT INTO metrics (time, name, value) VALUES (@time, @name, @value);"))
                        {
                            cmd.Command.Parameters.AddWithValue("time", NpgsqlDbType.Timestamp, observationTime);
                            cmd.Command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, metrics.Key);
                            cmd.Command.Parameters.AddWithValue("value", NpgsqlDbType.Integer, metrics.Value);
                            cmd.Command.Prepare();
                            cmd.Command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Scraping metrics");
            }
        }

        public void StartPushingMetrics(TimeSpan interval)
        {
            metricsCancellationTokenSource = new CancellationTokenSource();

            metricsPusherTask = Task.Run(() => Run(interval, metricsCancellationTokenSource.Token));
        }

        private async Task Run(TimeSpan interval, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Scrape();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Uploading metrics");
                }

                await Task.Delay(interval, cancellationToken);
            }
        }
    }
}
