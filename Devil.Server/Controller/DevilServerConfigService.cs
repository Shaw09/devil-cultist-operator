using System.Reflection;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace Devil.Server.Controller;

[Injectable(InjectionType = InjectionType.Singleton)]
public sealed class DevilServerConfigService(
    ISptLogger<DevilServerConfigService> logger)
{
    public static readonly object WaveLock = new();

    private const string Log = "[Devil/Config]";
    private const string ConfigFileName = "config.json";
    private const string LegacyConfigFileName = "server.json";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static string ConfigDir =>
        Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".",
            "config");

    private static string ConfigPath =>
        Path.Combine(ConfigDir, ConfigFileName);

    private static string LegacyConfigPath =>
        Path.Combine(ConfigDir, LegacyConfigFileName);

    private readonly object _initLock = new();

    private bool _initialized;
    private FileSystemWatcher? _watcher;
    private int _reloadTicket;

    private DevilServerConfig _current = new();

    public DevilServerConfig Current => Volatile.Read(ref _current);

    public event Action<DevilServerConfig>? Changed;

    public DevilServerConfig Initialize()
    {
        lock (_initLock)
        {
            if (_initialized)
            {
                return Current;
            }

            var config = Load(isStartup: true) ?? new DevilServerConfig();

            Interlocked.Exchange(ref _current, config);

            _initialized = true;

            StartWatcher();

            return Current;
        }
    }

    public DevilClientSettings GetClientSettings()
    {
        var config = Current;

        return new DevilClientSettings
        {
            SpawnChanceDevil = config.SpawnChanceDevil,
            DevilHarbingerBonusChance = config.DevilHarbingerBonusChance,
            SpawnChanceCultGang = config.SpawnChanceCultGang
        };
    }

    private DevilServerConfig? Load(bool isStartup)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);

            if (!File.Exists(ConfigPath))
            {
                if (File.Exists(LegacyConfigPath))
                {
                    File.Move(LegacyConfigPath, ConfigPath);

                    logger.Info(
                        $"{Log} Migrated {LegacyConfigFileName} to {ConfigFileName}.");
                }
                else
                {
                    var defaults = new DevilServerConfig();

                    File.WriteAllText(
                        ConfigPath,
                        JsonSerializer.Serialize(defaults, WriteOptions));

                    logger.Info(
                        $"{Log} {ConfigFileName} not found. Created default configuration.");

                    return defaults;
                }
            }
            else if (File.Exists(LegacyConfigPath))
            {
                logger.Warning(
                    $"{Log} Both configuration files exist. " +
                    $"Using {ConfigFileName}; legacy file ignored.");
            }

            string json = ReadWithRetry(ConfigPath);

            using var document = JsonDocument.Parse(json, ReadOptions);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException(
                    "Configuration root must be a JSON object.");
            }

            var config = ParseConfig(document.RootElement);

            logger.Info(
                $"{Log} {ConfigFileName} loaded from: {ConfigPath}");

            return config;
        }
        catch (Exception ex)
        {
            if (isStartup)
            {
                logger.Warning(
                    $"{Log} Configuration loading failed: {ex.Message}. " +
                    "Using default values.");

                return new DevilServerConfig();
            }

            logger.Warning(
                $"{Log} Reload failed: {ex.Message}. " +
                "Keeping previous configuration.");

            return null;
        }
    }

    private DevilServerConfig ParseConfig(JsonElement root)
    {
        var defaults = new DevilServerConfig();

        return new DevilServerConfig
        {
            SpawnChanceDevil = ReadPercentage(
                root,
                nameof(DevilServerConfig.SpawnChanceDevil),
                defaults.SpawnChanceDevil),

            DevilHarbingerBonusChance = ReadPercentage(
                root,
                nameof(DevilServerConfig.DevilHarbingerBonusChance),
                defaults.DevilHarbingerBonusChance),

            SpawnChanceCultGang = ReadPercentage(
                root,
                nameof(DevilServerConfig.SpawnChanceCultGang),
                defaults.SpawnChanceCultGang),

            Oni = ReadBoolean(root, nameof(DevilServerConfig.Oni), defaults.Oni),

            Prizrak = ReadBoolean(
                root,
                nameof(DevilServerConfig.Prizrak),
                defaults.Prizrak),

            CultGangEscortSpawnChance = ReadChanceDictionary(
                root,
                nameof(DevilServerConfig.CultGangEscortSpawnChance),
                defaults.CultGangEscortSpawnChance),

            DevilEscortSpawnChance = ReadChanceDictionary(
                root,
                nameof(DevilServerConfig.DevilEscortSpawnChance),
                defaults.DevilEscortSpawnChance)
        };
    }

    private double ReadPercentage(
        JsonElement root,
        string propertyName,
        double fallback)
    {
        if (!TryGetProperty(root, propertyName, out var value))
        {
            return fallback;
        }

        return ReadPercentageValue(
            value,
            fallback,
            propertyName);
    }

    private double ReadPercentageValue(
        JsonElement value,
        double fallback,
        string propertyPath)
    {
        // Integers and decimals are both valid JSON numbers.
        // Strings, null, booleans and other types are rejected.
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out double number) ||
            !double.IsFinite(number))
        {
            logger.Warning(
                $"{Log} WARNING: {propertyPath} has an invalid value. " +
                $"Using default: {fallback}%.");

            return fallback;
        }

        double clamped = Math.Clamp(number, 0, 100);

        if (clamped != number)
        {
            logger.Warning(
                $"{Log} WARNING: {propertyPath} value {number}% " +
                $"was clamped to {clamped}%.");
        }

        return clamped;
    }

    private bool ReadBoolean(
        JsonElement root,
        string propertyName,
        bool fallback)
    {
        if (!TryGetProperty(root, propertyName, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        logger.Warning(
            $"{Log} WARNING: {propertyName} has an invalid value. " +
            $"Using default: {fallback}.");

        return fallback;
    }

    private Dictionary<string, double> ReadChanceDictionary(
        JsonElement root,
        string propertyName,
        Dictionary<string, double> defaults)
    {
        var result = new Dictionary<string, double>(
            defaults,
            StringComparer.OrdinalIgnoreCase);

        if (!TryGetProperty(root, propertyName, out var value))
        {
            return result;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            logger.Warning(
                $"{Log} WARNING: {propertyName} must be an object. " +
                "Using default map values.");

            return result;
        }

        foreach (var entry in value.EnumerateObject())
        {
            double fallback = result.TryGetValue(
                entry.Name,
                out double existingValue)
                    ? existingValue
                    : 100;

            result[entry.Name] = ReadPercentageValue(
                entry.Value,
                fallback,
                $"{propertyName}.{entry.Name}");
        }

        return result;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(
                property.Name,
                propertyName,
                StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string ReadWithRetry(string path)
    {
        IOException? lastException = null;

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                lastException = ex;

                if (attempt < 4)
                {
                    Thread.Sleep(250);
                }
            }
        }

        throw lastException ?? new IOException(
            $"Could not read configuration file: {path}");
    }

    private void StartWatcher()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);

            _watcher = new FileSystemWatcher(ConfigDir, ConfigFileName)
            {
                NotifyFilter =
                    NotifyFilters.LastWrite |
                    NotifyFilters.Size |
                    NotifyFilters.FileName,

                EnableRaisingEvents = true
            };

            _watcher.Changed += (_, _) => OnFileChanged();
            _watcher.Created += (_, _) => OnFileChanged();
            _watcher.Renamed += (_, _) => OnFileChanged();

            logger.Info(
                $"{Log} Watching {ConfigPath}.");
        }
        catch (Exception ex)
        {
            logger.Warning(
                $"{Log} Watcher could not start: {ex.Message}. " +
                "Restart required for configuration changes.");
        }
    }

    private void OnFileChanged()
    {
        int ticket = Interlocked.Increment(ref _reloadTicket);

        _ = Task.Run(async () =>
        {
            await Task.Delay(500);

            if (ticket != Volatile.Read(ref _reloadTicket))
            {
                return;
            }

            var config = Load(isStartup: false);

            if (config == null)
            {
                return;
            }

            Interlocked.Exchange(ref _current, config);

            logger.Info(
                $"{Log} {ConfigFileName} reloaded successfully.");

            NotifyChanged(config);
        });
    }

    private void NotifyChanged(DevilServerConfig config)
    {
        var handlers = Changed?.GetInvocationList();

        if (handlers == null)
        {
            return;
        }

        foreach (var callback in handlers)
        {
            if (callback is not Action<DevilServerConfig> handler)
            {
                continue;
            }

            try
            {
                handler(config);
            }
            catch (Exception ex)
            {
                logger.Warning(
                    $"{Log} Configuration update handler failed: {ex.Message}");
            }
        }
    }
}