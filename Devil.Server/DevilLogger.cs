using MoreBotsServer;

namespace Devil.Server
{
    public static class DevilLogger
    {
        private static MoreBotsLogger? _logger;

        public static void Initialize(MoreBotsLogger logger)
        {
            _logger = logger;

            Info("DevilLogger initialized successfully.");
        }

        public static void Info(string message)
        {
            _logger?.Info($"[Devil] {message}");
        }

        public static void Warning(string message)
        {
            _logger?.Warning($"[Devil] {message}");
        }

        public static void Error(string message)
        {
            if (_logger == null)
            {
                return;
            }

            _logger.Error($"[Devil] {message}");
        }

        public static void Exception(string context, System.Exception ex)
        {
            if (_logger == null)
            {
                return;
            }

            _logger.Error($"[Devil] EXCEPTION in {context}: {ex}");
        }
    }
}