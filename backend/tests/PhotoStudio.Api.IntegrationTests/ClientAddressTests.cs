using System.Net;
using PhotoStudio.Api.Auth;

namespace PhotoStudio.Api.IntegrationTests;

/// <summary>
/// Tests of <see cref="ClientAddress"/>: the form an address takes when its failed logins are counted. They need no database.
/// </summary>
public sealed class ClientAddressTests
{
    /// <summary>
    /// An IPv4 address is counted as it is, and so is the one an IPv6 socket reports for it.
    /// </summary>
    [Fact]
    public void Normalize_AnIpv4Address_IsKeptAndTheMappedFormIsTheSame()
    {
        ClientAddress.Normalize(IPAddress.Parse("203.0.113.5")).ShouldBe("203.0.113.5");
        ClientAddress.Normalize(IPAddress.Parse("::ffff:203.0.113.5")).ShouldBe("203.0.113.5");
    }

    /// <summary>
    /// Every address of the same IPv6 /64 network counts as one, so rotating the last 64 bits does not give a guesser a fresh count.
    /// </summary>
    [Fact]
    public void Normalize_AnIpv6Address_BecomesItsSlash64Network()
    {
        var first = ClientAddress.Normalize(IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:dddd"));
        var second = ClientAddress.Normalize(IPAddress.Parse("2001:db8:1:2::1"));
        var otherNetwork = ClientAddress.Normalize(IPAddress.Parse("2001:db8:1:3::1"));

        first.ShouldBe("2001:db8:1:2::/64");
        second.ShouldBe(first);
        otherNetwork.ShouldNotBe(first);
    }

    /// <summary>
    /// With no address there is nothing to count under.
    /// </summary>
    [Fact]
    public void Normalize_WithoutAnAddress_IsNull()
    {
        ClientAddress.Normalize(null).ShouldBeNull();
    }
}
