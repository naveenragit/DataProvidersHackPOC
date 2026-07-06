using Microsoft.Extensions.Logging;

namespace FinancialServices.Tests.TestSupport;

/// <summary>
/// Minimal <see cref="ILogger{T}"/> that records every entry so tests can assert a warning path was
/// taken (e.g. a pending provider source that degrades to no-coverage) without a mocking framework.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}
