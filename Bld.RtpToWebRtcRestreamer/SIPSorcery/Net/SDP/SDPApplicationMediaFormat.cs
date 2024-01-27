//-----------------------------------------------------------------------------
// Filename: SDPApplicationMediaFormat.cs
//
// Description: An SDP media format for an "application" media announcement.
// These media formats differ from those used with "audio" and "video"
// announcements.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 17 OCt 2020	Aaron Clauson	Created, Dublin, Ireland.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

internal struct SDPApplicationMediaFormat
{
    private readonly string _id;

    public string Rtpmap { get; }

    public string Fmtp { get; }

    public SDPApplicationMediaFormat(string id)
    {
        _id = id;
        Rtpmap = null;
        Fmtp = null;
    }

    public SDPApplicationMediaFormat(string id, string rtpmap, string fmtp)
    {
        _id = id;
        Rtpmap = rtpmap;
        Fmtp = fmtp;
    }

    public SDPApplicationMediaFormat WithUpdatedRtpmap(string rtpmap) => new(_id, rtpmap, Fmtp);

    public SDPApplicationMediaFormat WithUpdatedFmtp(string fmtp) => new(_id, Rtpmap, fmtp);
}