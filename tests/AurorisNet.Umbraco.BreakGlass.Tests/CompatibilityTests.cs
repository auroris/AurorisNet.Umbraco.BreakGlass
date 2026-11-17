using BreakGlass.Compatibility;

namespace BreakGlass.Tests;

/// <summary>New CMS majors need explicit adapters rather than silently using an untested implementation.</summary>
public sealed class CompatibilityTests
{
    /// <summary>The supported range has a lower and upper bound.</summary>
    [Theory]
    [InlineData(16)]
    [InlineData(19)]
    public void Unsupported_majors_require_their_own_adapter(int major)
        => Assert.Throws<NotSupportedException>(() => UmbracoCompatibility.ImplementationType(major));
}
