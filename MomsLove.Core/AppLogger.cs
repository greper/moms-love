using System.Text;

namespace MomsLove.Core;

public sealed class AppLogger
{
    private readonly string _logsDirectory;
    private readonly object _sync = new();

    public AppLogger(string? baseDirectory = null)
    {
        var root = baseDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".MomsLove");
        _logsDirectory = Path.Combine(root, "logs");
        Directory.CreateDirectory(_logsDirectory);
    }

    public string LogsDirectory => _logsDirectory;

    public void Write(string message, Exception? exception = null)
    {
        try
        {
            var line = new StringBuilder().Append(DateTimeOffset.Now.ToString("O"))
                .Append(" [").Append(Environment.CurrentManagedThreadId).Append("] ").Append(message);
            if (exception is not null) line.AppendLine().Append(exception);
            lock (_sync)
            {
                Directory.CreateDirectory(_logsDirectory);
                File.AppendAllText(Path.Combine(_logsDirectory, $"{DateTime.Now:yyyy-MM-dd}.log"), line.AppendLine().ToString(), Encoding.UTF8);
            }
        }
        catch { }
    }
}
