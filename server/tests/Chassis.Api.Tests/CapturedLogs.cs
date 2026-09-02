using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Chassis.Api.Tests;

/// <summary>In-memory sink so a failing test can print what the server logged.</summary>
internal static class CapturedLogs
{
    private static readonly ConcurrentQueue<string> Lines = new();

    public static void Add(string line) => Lines.Enqueue(line);

    public static void Clear() => Lines.Clear();

    public static string Dump() => string.Join(Environment.NewLine, Lines);

    public static IReadOnlyList<string> Snapshot() => Lines.ToArray();
}

internal sealed class CapturedLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturedLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class CapturedLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            CapturedLogs.Add($"[{logLevel}] {category}: {message}");
            if (exception is not null)
            {
                CapturedLogs.Add(exception.ToString());
            }
        }
    }
}
