using TimeReport.Web.Auth;

namespace TimeReport.Web.Tests;

public sealed class Base32Tests
{
    [Fact]
    public void EncodesAndDecodesRoundTrip()
    {
        var bytes = Enumerable.Range(0, 64).Select(index => (byte)index).ToArray();
        var encoded = Base32.Encode(bytes);
        var decoded = Base32.Decode(encoded);
        Assert.Equal(bytes, decoded);
    }

    [Fact]
    public void NormalizesSpacesPaddingAndCase()
    {
        Assert.Equal("ABCDEF", Base32.Normalize("ab cd ef===="));
        Assert.Equal("ABCDEF", Base32.Normalize("AB-CD-EF"));
    }
}
