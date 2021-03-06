using System;
using Npgsql.Logging;
using Serilog;
using Serilog.Events;

namespace Improbable.Postgres
{
    public class SerilogNpgqslLoggingProvider : INpgsqlLoggingProvider
    {
        private readonly NpgsqlLogLevel minLevel;

        public SerilogNpgqslLoggingProvider(NpgsqlLogLevel minLevel)
        {
            this.minLevel = minLevel;
        }

        public NpgsqlLogger CreateLogger(string name)
        {
            return new SerilogNpgsqlLog(minLevel);
        }
    }

    internal class SerilogNpgsqlLog : NpgsqlLogger
    {
        private readonly ILogger logger;
        private readonly NpgsqlLogLevel minLevel;

        public SerilogNpgsqlLog(NpgsqlLogLevel minLevel)
        {
            this.minLevel = minLevel;
            logger = Serilog.Log.Logger.ForContext("Source", "Npgsql");
        }

        public override bool IsEnabled(NpgsqlLogLevel level)
        {
            if ((int) level < (int) minLevel)
            {
                return false;
            }

            return level switch
            {
                NpgsqlLogLevel.Trace => logger.IsEnabled(LogEventLevel.Verbose),
                NpgsqlLogLevel.Debug => logger.IsEnabled(LogEventLevel.Debug),
                NpgsqlLogLevel.Info => logger.IsEnabled(LogEventLevel.Information),
                NpgsqlLogLevel.Warn => logger.IsEnabled(LogEventLevel.Warning),
                NpgsqlLogLevel.Error => logger.IsEnabled(LogEventLevel.Error),
                NpgsqlLogLevel.Fatal => logger.IsEnabled(LogEventLevel.Fatal),
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
            };
        }

        public override void Log(NpgsqlLogLevel level, int connectorId, string msg, Exception? exception = null)
        {
            if ((int) level < (int) minLevel)
            {
                return;
            }

            switch (level)
            {
                case NpgsqlLogLevel.Trace:
                    if (exception != null)
                    {
                        logger.Verbose(exception, msg);
                    }
                    else
                    {
                        logger.Verbose(msg);
                    }

                    break;
                case NpgsqlLogLevel.Debug:
                    if (exception != null)
                    {
                        logger.Debug(exception, msg);
                    }
                    else
                    {
                        logger.Debug(msg);
                    }

                    break;
                case NpgsqlLogLevel.Info:
                    if (exception != null)
                    {
                        logger.Information(exception, msg);
                    }
                    else
                    {
                        logger.Information(msg);
                    }

                    break;
                case NpgsqlLogLevel.Warn:
                    if (exception != null)
                    {
                        logger.Warning(exception, msg);
                    }
                    else
                    {
                        logger.Warning(msg);
                    }

                    break;
                case NpgsqlLogLevel.Error:
                    if (exception != null)
                    {
                        logger.Error(exception, msg);
                    }
                    else
                    {
                        logger.Error(msg);
                    }

                    break;
                case NpgsqlLogLevel.Fatal:
                    if (exception != null)
                    {
                        logger.Fatal(exception, msg);
                    }
                    else
                    {
                        logger.Fatal(msg);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }
    }
}
