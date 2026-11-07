using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace ClassFund.IntegrationTests.Infrastructure;

/// <summary>Giữ mọi log event để kiểm tra không có mật khẩu / hash / cookie trong log (NFR-10).</summary>
public sealed class CapturedLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<string> _events = new();

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        logEvent.RenderMessage(writer);
        foreach (var property in logEvent.Properties)
        {
            writer.Write(' ');
            writer.Write(property.Key);
            writer.Write('=');
            property.Value.Render(writer);
        }

        if (logEvent.Exception is not null)
        {
            writer.Write(' ');
            writer.Write(logEvent.Exception);
        }

        _events.Enqueue(writer.ToString());
    }

    public IReadOnlyCollection<string> Events => _events.ToArray();
}
