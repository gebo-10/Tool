using Serilog;
using Serilog.Core;
namespace BuildSystem
{
    public class TaskLogger : IDisposable
    {
        private readonly ILogger _logger;
        private readonly Logger _fileLogger;

        public TaskLogger(string logPath)
        {
            _fileLogger = new LoggerConfiguration()
                .WriteTo.File(logPath, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _logger = _fileLogger;
        }

        public void Log(string message) => _logger.Information(message);
        public void Warn(string message) => _logger.Warning(message);
        public void Error(string message) => _logger.Error(message);
        public void Dispose() => _fileLogger?.Dispose();
    }
}
