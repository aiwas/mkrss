using System;

namespace MkRss.Infrastructure.Tests;

/// <summary>常に同じ時刻を返す <see cref="TimeProvider"/>。</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
    public override DateTimeOffset GetUtcNow() => now;
}
