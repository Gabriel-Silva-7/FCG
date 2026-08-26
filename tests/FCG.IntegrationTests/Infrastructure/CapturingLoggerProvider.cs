using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FCG.IntegrationTests.Infrastructure;

public sealed record CapturedLogEntry(
    string Category,
    LogLevel Level,
    string Message,
    string? ExceptionText,
    IReadOnlyList<KeyValuePair<string, object?>> State)
{
    public object? Field(string name) =>
        State.FirstOrDefault(pair => pair.Key == name).Value;

    public bool HasField(string name) =>
        State.Any(pair => pair.Key == name);

    public IEnumerable<string> TextValues() =>
        State.Select(pair => pair.Value?.ToString())
            .Append(Message)
            .Append(ExceptionText)
            .Where(text => !string.IsNullOrEmpty(text))
            .Select(text => text!);
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    public IReadOnlyList<CapturedLogEntry> Entries => _entries.ToArray();

    public IEnumerable<string> AllText() => Entries.SelectMany(entry => entry.TextValues());

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Clear() => _entries.Clear();

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(
        string category,
        ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var structuredState = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];

            // O formatter não inclui necessariamente a exceção; providers reais a escrevem à parte.
            entries.Enqueue(new CapturedLogEntry(
                category,
                logLevel,
                formatter(state, exception),
                exception?.ToString(),
                structuredState.ToArray()));
        }
    }
}
