//-----------------------------------------------------------------------------
// Filename: SDPConnectionInformation.cs
//
// Description:
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// ??	Aaron Clauson	Created, Hobart, Australia.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

internal class SDPConnectionInformation
{
    private const string CONNECTION_ADDRESS_TYPE_IPV4 = "IP4";
    private const string CONNECTION_ADDRESS_TYPE_IPV6 = "IP6";

    private const string m_CRLF = "\r\n";

    /// <summary>
    /// Type of network, IN = Internet.
    /// </summary>
    private string _connectionNetworkType = "IN";

    /// <summary>
    /// Session level address family.
    /// </summary>
    private string _connectionAddressType = CONNECTION_ADDRESS_TYPE_IPV4;

    /// <summary>
    /// IP or multicast address for the media connection.
    /// </summary>
    public string ConnectionAddress;

    private SDPConnectionInformation()
    { }

    public SDPConnectionInformation(IPAddress connectionAddress)
    {
        ConnectionAddress = connectionAddress.ToString();
        _connectionAddressType = connectionAddress.AddressFamily == AddressFamily.InterNetworkV6 ? CONNECTION_ADDRESS_TYPE_IPV6 : CONNECTION_ADDRESS_TYPE_IPV4;
    }

    public static SDPConnectionInformation ParseConnectionInformation(string connectionLine)
    {
        var connectionInfo = new SDPConnectionInformation();
        var connectionFields = connectionLine.Substring(2).Trim().Split(' ');
        connectionInfo._connectionNetworkType = connectionFields[0].Trim();
        connectionInfo._connectionAddressType = connectionFields[1].Trim();
        connectionInfo.ConnectionAddress = connectionFields[2].Trim();
        return connectionInfo;
    }

    public override string ToString()
    {
        return "c=" + _connectionNetworkType + " " + _connectionAddressType + " " + ConnectionAddress + m_CRLF;
    }
}