#nullable enable
using System.Net;
using System.Net.NetworkInformation;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Networking;

/// <summary>
/// Helper class to provide network services.
/// </summary>
internal static class NetServices
{
    /// <summary>
    /// Determines the local IP address to use to connection a remote address and
    /// returns all the local addresses (IPv4 and IPv6) that are bound to the same
    /// interface. The main (and probably sole) use case for this method is
    /// gathering host candidates for a WebRTC ICE session. Rather than selecting
    /// ALL local IP addresses only those on the interface needed to connect to
    /// the destination are returned.
    /// </summary>
    /// <returns>A list of local IP addresses on the identified interface(s).</returns>
    public static List<IPAddress> GetLocalAddressesOnInterface()
    {
        var localAddresses = new List<IPAddress>();

        var adapters = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var n in adapters)
        {
            // AC 5 Jun 2020: Network interface status is reported as Unknown on WSL.
            if (n.OperationalStatus == OperationalStatus.Up || n.OperationalStatus == OperationalStatus.Unknown)
            {
                var ipProps = n.GetIPProperties();
                localAddresses.AddRange(ipProps.UnicastAddresses.Select(x => x.Address));
            }
        }

        return localAddresses;
    }
}