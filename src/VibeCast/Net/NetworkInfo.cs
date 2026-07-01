using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace VibeCast.Net;

public static class NetworkInfo
{
    /// <summary>
    /// Returns usable IPv4 addresses of active adapters, most-likely-LAN first
    /// (private ranges before anything else). Loopback is excluded.
    /// </summary>
    public static List<IPAddress> LocalIPv4Addresses()
    {
        var results = new List<IPAddress>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(ua.Address)) continue;
                results.Add(ua.Address);
            }
        }

        return results
            .Distinct()
            .OrderByDescending(IsPrivate)
            .ThenBy(a => a.ToString())
            .ToList();
    }

    private static bool IsPrivate(IPAddress ip)
    {
        byte[] b = ip.GetAddressBytes();
        if (b.Length != 4) return false;
        return b[0] switch
        {
            10 => true,
            192 when b[1] == 168 => true,
            172 when b[1] >= 16 && b[1] <= 31 => true,
            _ => false
        };
    }
}
