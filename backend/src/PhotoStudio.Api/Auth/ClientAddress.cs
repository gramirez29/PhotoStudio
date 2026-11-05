using System.Net;
using System.Net.Sockets;

namespace PhotoStudio.Api.Auth;

/// <summary>
/// Tells which network address a request came from, in the form used to count failed logins. The address is the one the forwarded
/// headers middleware settled on (the last hop of the proxy), the same one the rate limiter uses.
/// </summary>
public static class ClientAddress
{
    /// <summary>
    /// Number of leading bytes of an IPv6 address that identify a network: the first 64 bits. Anyone on the network can use any
    /// address inside it, so counting them separately would let one person try endlessly by rotating addresses.
    /// </summary>
    private const int Ipv6NetworkBytes = 8;

    /// <summary>
    /// Gets the address of the caller of a request.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The address as text, or <see langword="null"/> when it is not known.</returns>
    public static string? Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Normalize(context.Connection.RemoteIpAddress);
    }

    /// <summary>
    /// Turns an address into the text its failed logins are counted under: an IPv4 address as it is (an IPv4 address inside IPv6 is
    /// treated as IPv4), and an IPv6 address as its /64 network.
    /// </summary>
    /// <param name="address">Address to normalize.</param>
    /// <returns>The text, or <see langword="null"/> when there is no address.</returns>
    public static string? Normalize(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var network = new byte[16];
        address.GetAddressBytes().AsSpan(0, Ipv6NetworkBytes).CopyTo(network);
        return $"{new IPAddress(network)}/64";
    }
}
