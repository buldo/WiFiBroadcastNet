//-----------------------------------------------------------------------------
// Filename: SDPTypes.cs
//
// Description: Contains enums and helper classes for common definitions
// and attributes used in SDP payloads.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// Jacek Dzija
// Mateusz Greczek
//
// History:
// ??	Aaron Clauson	Created, Hobart, Australia.
// 30 Mar 2021 Jacek Dzija,Mateusz Greczek Added MSRP
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

public static class SDPMediaTypes
{
    public static SDPMediaTypesEnum GetSDPMediaType(string mediaType)
    {
        return (SDPMediaTypesEnum)Enum.Parse(typeof(SDPMediaTypesEnum), mediaType, true);
    }
}