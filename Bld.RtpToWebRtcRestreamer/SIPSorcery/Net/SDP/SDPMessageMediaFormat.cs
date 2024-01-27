﻿//-----------------------------------------------------------------------------
// Filename: SDPMessageMediaFormat.cs
//
// Description: Contains enums and helper classes for common definitions
// and attributes used in SDP payloads.
//
// Author(s):
// Jacek Dzija
// Mateusz Greczek
//
// History:
// 30 Mar 2021 Jacek Dzija,Mateusz Greczek Added MSRP
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

internal class SDPMessageMediaFormat
{
    public List<string> AcceptTypes;

    public string Endpoint;
}