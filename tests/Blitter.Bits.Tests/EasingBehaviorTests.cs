using Blitter.Bits;

namespace Blitter.Tests;

// Endpoint and InOut-midpoint coverage already lives in EasingTests
// (MathGTests.cs). These add behavioral coverage: monotonicity, the
// deliberate overshoot/undershoot families, and bounded ranges.
public class EasingBehaviorTests
{
    public static IEnumerable<object[]> MonotonicEasings()
    {
        var fns = new (string Name, Func<float, float> Fn)[]
        {
            (nameof(Easing.InSine), Easing.InSine),
            (nameof(Easing.OutSine), Easing.OutSine),
            (nameof(Easing.InOutSine), Easing.InOutSine),
            (nameof(Easing.InQuad), Easing.InQuad),
            (nameof(Easing.OutQuad), Easing.OutQuad),
            (nameof(Easing.InOutQuad), Easing.InOutQuad),
            (nameof(Easing.InCubic), Easing.InCubic),
            (nameof(Easing.OutCubic), Easing.OutCubic),
            (nameof(Easing.InOutCubic), Easing.InOutCubic),
            (nameof(Easing.InQuart), Easing.InQuart),
            (nameof(Easing.OutQuart), Easing.OutQuart),
            (nameof(Easing.InQuint), Easing.InQuint),
            (nameof(Easing.OutQuint), Easing.OutQuint),
            (nameof(Easing.InExpo), Easing.InExpo),
            (nameof(Easing.OutExpo), Easing.OutExpo),
            (nameof(Easing.InCirc), Easing.InCirc),
            (nameof(Easing.OutCirc), Easing.OutCirc),
        };
        foreach (var (name, fn) in fns)
            yield return new object[] { name, fn };
    }

    [Theory]
    [MemberData(nameof(MonotonicEasings))]
    public void MonotonicFamilies_NeverDecrease(string name, Func<float, float> fn)
    {
        var prev = fn(0f);
        for (var t = 0.02f; t <= 1f; t += 0.02f)
        {
            var cur = fn(t);
            Assert.True(cur >= prev - 1e-4f, $"{name} decreased at t={t} ({cur} < {prev})");
            prev = cur;
        }
    }

    [Theory]
    [MemberData(nameof(MonotonicEasings))]
    public void MidpointIsFinite(string name, Func<float, float> fn)
    {
        var v = fn(0.5f);
        Assert.False(float.IsNaN(v), $"{name}(0.5) was NaN");
        Assert.False(float.IsInfinity(v), $"{name}(0.5) was infinite");
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    public void InOutSine_SymmetricAboutMidpoint(float t)
    {
        // In/out curves are rotationally symmetric: f(t) + f(1-t) == 1.
        var a = Easing.InOutSine(t);
        var b = Easing.InOutSine(1f - t);
        Assert.Equal(1f, a + b, 3);
    }

    [Fact]
    public void OutQuad_MatchesReferenceValue()
    {
        // 1 - (1-0.5)^2 = 0.75
        Assert.Equal(0.75f, Easing.OutQuad(0.5f), 4);
    }

    [Fact]
    public void InQuad_MatchesReferenceValue()
    {
        Assert.Equal(0.25f, Easing.InQuad(0.5f), 4);
    }

    [Fact]
    public void InBack_UndershootsBelowZero()
    {
        // The "back" family dips past the start before moving forward.
        Assert.True(Easing.InBack(0.2f) < 0f);
    }

    [Fact]
    public void OutBack_OvershootsAboveOne()
    {
        Assert.True(Easing.OutBack(0.8f) > 1f);
    }

    [Fact]
    public void OutBounce_StaysWithinUnitRange()
    {
        for (var t = 0f; t <= 1f; t += 0.05f)
        {
            var v = Easing.OutBounce(t);
            Assert.InRange(v, 0f, 1f);
        }
    }
}
