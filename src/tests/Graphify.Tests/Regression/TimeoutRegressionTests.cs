using System.Reflection;
using Xunit;

namespace Graphify.Tests.Regression;

/// <summary>
/// Regression tests for Bug 3: CI Test Hanging.
/// Root cause: Tests with deadlocks ran indefinitely — no timeout configured.
/// Fix: Added --blame-hang-timeout 30s and timeout-minutes: 5.
/// These meta-tests verify our test infrastructure catches hangs.
/// </summary>
[Trait("Category", "Regression")]
public sealed class TimeoutRegressionTests
{
    /// <summary>
    /// Meta-test guardrail: All async test methods (returning Task) in Regression/EdgeCase
    /// test classes should have a Timeout configured on [Fact] or [Theory]. This prevents
    /// any future deadlock from hanging CI indefinitely.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task AllAsyncTests_HaveTimeoutAttribute_RegressionBug3()
    {
        await Task.CompletedTask;
        var assembly = typeof(TimeoutRegressionTests).Assembly;

        // Scope: only check test classes in our Regression namespace
        var regressionTypes = assembly.GetTypes()
            .Where(t => t.Namespace is not null &&
                        t.Namespace.Contains("Regression", StringComparison.Ordinal));

        var testMethods = regressionTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType))
            .Where(m => m.GetCustomAttributes()
                .Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute"));

        var violatingMethods = new List<string>();

        foreach (var method in testMethods)
        {
            var factAttr = method.GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute");

            if (factAttr is null)
                continue;

            // Check if Timeout property is set (non-zero)
            var timeoutProp = factAttr.GetType().GetProperty("Timeout");
            if (timeoutProp is null)
                continue;

            var timeout = (int)(timeoutProp.GetValue(factAttr) ?? 0);
            if (timeout <= 0)
            {
                violatingMethods.Add($"{method.DeclaringType?.Name}.{method.Name}");
            }
        }

        Assert.True(
            violatingMethods.Count == 0,
            $"The following async test methods lack a Timeout attribute (risks CI hang):\n" +
            string.Join("\n  - ", violatingMethods));
    }

    /// <summary>
    /// Root cause verification: SemaphoreSlim(1,1) double-acquire correctly times out
    /// instead of deadlocking. This tests our understanding of the original bug mechanism.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SemaphoreSlim_DoubleAcquire_DetectedByTimeout_RegressionBug3()
    {
        var semaphore = new SemaphoreSlim(1, 1);

        // First acquire succeeds
        var acquired1 = await semaphore.WaitAsync(1000);
        Assert.True(acquired1, "First acquire should succeed");

        // Second acquire on the same (non-reentrant) semaphore should timeout, NOT deadlock
        var acquired2 = await semaphore.WaitAsync(500);
        Assert.False(acquired2, "Second acquire should timeout — SemaphoreSlim is not reentrant");

        semaphore.Release();
    }
}
