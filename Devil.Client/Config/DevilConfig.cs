using BepInEx.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;

namespace Devil.Client.Config
{
    public sealed class DevilConfig
    {
        public double NightChance { get; set; } = 20;
        public double CultistChance { get; set; } = 20;
        public double WithCultGangChance { get; set; } = 20;

        public static DevilConfig Load(ManualLogSource logger)
        {
            string configDirectory = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "config"
            );

            Directory.CreateDirectory(configDirectory);

            string path = Path.Combine(
                configDirectory,
                "DevilCultistOperator.json"
            );

            var config = new DevilConfig();

            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);

                    config = JsonConvert.DeserializeObject<DevilConfig>(json)
                             ?? new DevilConfig();
                }
                else
                {
                    logger.LogInfo("[Devil] Config not found. Creating default config.");
                }

                config.NightChance =
                    Clamp(config.NightChance, 20);

                config.CultistChance =
                    Clamp(config.CultistChance, 20);

                config.WithCultGangChance =
                    Clamp(config.WithCultGangChance, 20);

                File.WriteAllText(
                    path,
                    JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented)
                );

                logger.LogInfo("[Devil] Configuration loaded.");
            }
            catch (Exception ex)
            {
                logger.LogError($"[Devil] Config loading failed: {ex}");

                config = new DevilConfig();
            }
            logger.LogInfo(
                $"[Devil Config] Loaded: " +
                $"CultistChance={config.CultistChance}, " +
                $"WithCultGangChance={config.WithCultGangChance}, " +
                $"NightChance={config.NightChance}"
            );
            return config;
        }

        private static double Clamp(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return fallback;

            return Math.Max(0, Math.Min(100, value));
        }
    }
}