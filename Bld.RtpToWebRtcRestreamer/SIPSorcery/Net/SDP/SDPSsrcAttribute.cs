namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

/// <summary>
/// An attribute used to defined additional properties about
/// a media source and the relationship between them.
/// As specified in RFC5576, https://tools.ietf.org/html/rfc5576.
/// </summary>
internal class SDPSsrcAttribute
{
    public const string MEDIA_CNAME_ATTRIBUE_PREFIX = "cname";

    public uint SSRC { get; }

    public string Cname { get; set; }

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <param name="ssrc">The SSRC that should match an RTP stream.</param>
    /// <param name="cname">Optional. The CNAME value to use in RTCP SDES sections.</param>
    /// group this is the group ID.</param>
    public SDPSsrcAttribute(uint ssrc, string cname)
    {
        SSRC = ssrc;
        Cname = cname;
    }
}