using MomsLove.Core;

namespace MomsLove.Tests;

public sealed class AppLoggerTests
{
    [Fact]
    public void Write_CreatesLogFileWithMessageAndException()
    {
        var root = Path.Combine(Path.GetTempPath(), "MomsLoveTests", Guid.NewGuid().ToString("N"));
        var logger = new AppLogger(root);

        logger.Write("测试事件", new InvalidOperationException("测试异常"));

        Assert.True(Directory.Exists(Path.Combine(root, "logs")));
        var file = Directory.GetFiles(Path.Combine(root, "logs"), "*.log").Single();
        var content = File.ReadAllText(file);
        Assert.Contains("测试事件", content);
        Assert.Contains("测试异常", content);
        Assert.Contains("InvalidOperationException", content);
    }
}
