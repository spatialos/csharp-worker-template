using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Improbable.Stdlib;

namespace Improbable.Postgres
{
    public class PostgresOptions : IPostgresOptions
    {
        public const string HostFlagName = "postgres_host";
        public const string UserNameFlagName = "postgres_user";
        public const string PasswordFlagName = "postgres_password";
        public const string DatabaseFlagName = "postgres_database";
        public const string AdditionalFlagName = "postgres_additional";

        private string? cachedConnectionString;
        private readonly object rootLock = new object();

        private readonly ConcurrentDictionary<string, string> flagValues = new ConcurrentDictionary<string, string>(
            new Dictionary<string, string>
            {
                {HostFlagName, "127.0.0.1"},
                {UserNameFlagName, "postgres"},
                {PasswordFlagName, "DO_NOT_USE_IN_PRODUCTION"},
                {DatabaseFlagName, "postgres"},
                {AdditionalFlagName, string.Empty}
            }
        );

        private readonly IReadOnlyList<string> keys;

        public delegate string GetStringDelegate(string flagName, string currentFlagValue);

        public PostgresOptions(GetStringDelegate getter)
        {
            keys = flagValues.Keys.ToList();

            foreach (var (key, currentValue) in flagValues)
            {
                var value = getter(key, currentValue);

                if (!string.IsNullOrEmpty(value))
                {
                    flagValues[key] = value;
                }
            }
        }

        public PostgresOptions(IPostgresOptions options)
        : this((key, value) => GetFromIOptions(options, key, value))
        {
        }

        public static string GetFromIOptions(IPostgresOptions options, string key, string value)
        {
            var optionValue = key switch
            {
                HostFlagName => options.PostgresHost,
                UserNameFlagName => options.PostgresUserName,
                PasswordFlagName => options.PostgresPassword,
                DatabaseFlagName => options.PostgresDatabase,
                AdditionalFlagName => options.PostgresAdditionalOptions,
                _ => throw new InvalidOperationException($"Unknown Postgres flag {key}")
            };

            return string.IsNullOrEmpty(optionValue) ? value : optionValue;
        }

        public string PostgresHost
        {
            get
            {
                lock (rootLock)
                {
                    return flagValues[HostFlagName];
                }
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(nameof(PostgresHost));
                }

                flagValues[HostFlagName] = value;

                lock (rootLock)
                {
                    cachedConnectionString = null;
                }
            }
        }

        public string PostgresUserName
        {
            get => flagValues[UserNameFlagName];
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(nameof(PostgresUserName));
                }

                flagValues[UserNameFlagName] = value;

                lock (rootLock)
                {
                    cachedConnectionString = null;
                }
            }
        }

        public string PostgresPassword
        {
            get => flagValues[PasswordFlagName];
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(nameof(PostgresPassword));
                }

                flagValues[PasswordFlagName] = value;

                lock (rootLock)
                {
                    cachedConnectionString = null;
                }
            }
        }

        public string PostgresDatabase
        {
            get => flagValues[DatabaseFlagName];
            set
            {
                flagValues[DatabaseFlagName] = value;

                lock (rootLock)
                {
                    cachedConnectionString = null;
                }
            }
        }

        public string PostgresAdditionalOptions
        {
            get => flagValues[AdditionalFlagName];
            set
            {
                flagValues[AdditionalFlagName] = value;

                lock (rootLock)
                {
                    cachedConnectionString = null;
                }
            }
        }

        public string ConnectionString
        {
            get
            {
                lock (rootLock)
                {
                    return cachedConnectionString ??= AsPostgresConnectionString();
                }
            }
        }

        public void ProcessOpList(OpList opList)
        {
            foreach (var key in keys)
            {
                if (!opList.TryGetWorkerFlagChange(key, out var value))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(value) && !IsEmptyStringAllowed(key))
                {
                    continue;
                }

                flagValues[key] = value;

                lock (rootLock)
                {
                    cachedConnectionString = null;
                }
            }
        }

        private static bool IsEmptyStringAllowed(string key)
        {
            return key switch
            {
                HostFlagName => false,
                UserNameFlagName => false,
                PasswordFlagName => false,
                _ => true
            };
        }

        private string AsPostgresConnectionString()
        {
            lock (rootLock)
            {
                var additional = string.Empty;
                if (!string.IsNullOrEmpty(PostgresDatabase))
                {
                    additional = $";Database={PostgresDatabase}";
                }

                if (!string.IsNullOrEmpty(PostgresAdditionalOptions))
                {
                    additional += $";{PostgresAdditionalOptions}";
                }

                return $"Host={PostgresHost};Username={PostgresUserName};Password={PostgresPassword}{additional}";
            }
        }
    }
}
