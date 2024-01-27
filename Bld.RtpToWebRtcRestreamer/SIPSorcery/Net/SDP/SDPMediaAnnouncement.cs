//-----------------------------------------------------------------------------
// Filename: SDPMediaAnnouncement.cs
//
// Description:
//
// Remarks:
//
// An example of an "application" type media announcement use is negotiating
// SCTP-over-DTLS which acts as the transport for WebRTC data channels.
// https://tools.ietf.org/html/rfc8841
// "Session Description Protocol (SDP) Offer/Answer Procedures for Stream
// Control Transmission Protocol (SCTP) over Datagram Transport Layer
// Security (DTLS) Transport"
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// Jacek Dzija
// Mateusz Greczek
//
// History:
// ??	Aaron Clauson	Created, Hobart, Australia.
// rj2: add SDPSecurityDescription parser
// 30 Mar 2021 Jacek Dzija,Mateusz Greczek Added MSRP
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

internal class SDPMediaAnnouncement
{
    public const string MEDIA_EXTENSION_MAP_ATTRIBUE_PREFIX = "a=extmap:";
    public const string MEDIA_FORMAT_ATTRIBUE_PREFIX = "a=rtpmap:";
    public const string MEDIA_FORMAT_PARAMETERS_ATTRIBUE_PREFIX = "a=fmtp:";
    public const string MEDIA_FORMAT_SSRC_ATTRIBUE_PREFIX = "a=ssrc:";
    public const string MEDIA_FORMAT_SSRC_GROUP_ATTRIBUE_PREFIX = "a=ssrc-group:";
    public const string MEDIA_FORMAT_SCTP_MAP_ATTRIBUE_PREFIX = "a=sctpmap:";
    public const string MEDIA_FORMAT_SCTP_PORT_ATTRIBUE_PREFIX = "a=sctp-port:";
    public const string MEDIA_FORMAT_MAX_MESSAGE_SIZE_ATTRIBUE_PREFIX = "a=max-message-size:";
    public const string MEDIA_FORMAT_PATH_MSRP_PREFIX = "a=path:msrp:";
    public const string MEDIA_FORMAT_PATH_ACCEPT_TYPES_PREFIX = "a=accept-types:";
    public const string TIAS_BANDWIDTH_ATTRIBUE_PREFIX = "b=TIAS:";
    private const MediaStreamStatusEnum DEFAULT_STREAM_STATUS = MediaStreamStatusEnum.SendRecv;

    private const string m_CRLF = "\r\n";

    private static readonly ILogger logger = Log.Logger;

    public SDPConnectionInformation Connection { get; set; }

    // Media Announcement fields.
    public SDPMediaTypesEnum Media = SDPMediaTypesEnum.audio;   // Media type for the stream.
    public int Port;                        // For UDP transports should be in the range 1024 to 65535 and for RTP compliance should be even (only even ports used for data).
    public string Transport = "RTP/AVP";    // Defined types RTP/AVP (RTP Audio Visual Profile) and udp.
    public string IceUfrag;                 // If ICE is being used the username for the STUN requests.
    public string IcePwd;                   // If ICE is being used the password for the STUN requests.
    public string IceOptions;               // Optional attribute to specify support ICE options, e.g. "trickle".
    public IceRolesEnum? IceRole = null;
    public string DtlsFingerprint;          // If DTLS handshake is being used this is the fingerprint or our DTLS certificate.
    public int MLineIndex = 0;

    /// <summary>
    /// If being used in a bundle this the ID for the announcement.
    /// Example: a=mid:audio or a=mid:video.
    /// </summary>
    public string MediaID { get; set; }

    /// <summary>
    /// The "ssrc" attributes group ID as specified in RFC5576.
    /// </summary>
    public string SsrcGroupID { get; set; }

    /// <summary>
    /// The "sctpmap" attribute defined in https://tools.ietf.org/html/draft-ietf-mmusic-sctp-sdp-26 for
    /// use in WebRTC data channels.
    /// </summary>
    public string SctpMap { get; set; }

    /// <summary>
    /// The "sctp-port" attribute defined in https://tools.ietf.org/html/draft-ietf-mmusic-sctp-sdp-26 for
    /// use in WebRTC data channels.
    /// </summary>
    public ushort? SctpPort { get; set; } = null;

    /// <summary>
    /// The "max-message-size" attribute defined in https://tools.ietf.org/html/draft-ietf-mmusic-sctp-sdp-26 for
    /// use in WebRTC data channels.
    /// </summary>
    public long MaxMessageSize;

    /// <summary>
    /// If the RFC5576 is being used this is the list of "ssrc" attributes
    /// supplied.
    /// </summary>
    public List<SDPSsrcAttribute> SsrcAttributes { get; }= new();

    /// <summary>
    /// Optional Transport Independent Application Specific Maximum (TIAS) bandwidth.
    /// </summary>
    public uint TIASBandwidth { get; set; } = 0;

    public List<string> BandwidthAttributes { get; } = new();

    /// <summary>
    /// In media definitions, "i=" fields are primarily intended for labelling media streams https://tools.ietf.org/html/rfc4566#page-12
    /// </summary>
    public string MediaDescription { get; set; }

    /// <summary>
    ///  For AVP these will normally be a media payload type as defined in the RTP Audio/Video Profile.
    /// </summary>
    public Dictionary<int, SDPAudioVideoMediaFormat> MediaFormats { get; }= new();

    /// <summary>
    ///  a=extmap - Mapping for RTP header extensions
    /// </summary>
    public Dictionary<int, RTPHeaderExtension> HeaderExtensions { get; } = new();

    /// <summary>
    ///  For AVP these will normally be a media payload type as defined in the RTP Audio/Video Profile.
    /// </summary>
    public SDPMessageMediaFormat MessageMediaFormat { get; } = new();

    /// <summary>
    /// List of media formats for "application media announcements. Application media announcements have different
    /// semantics to audio/video announcements. They can also use aribtrary strings as the format ID.
    /// </summary>
    public Dictionary<string, SDPApplicationMediaFormat> ApplicationMediaFormats { get; }= new();

    private readonly List<string> _extraMediaAttributes = new();          // Attributes that were not recognised.
    private readonly List<SDPSecurityDescription> _securityDescriptions = new(); //2018-12-21 rj2: add a=crypto parsing etc.
    public List<string> IceCandidates { get; } = new();

    /// <summary>
    /// The stream status of this media announcement.
    /// </summary>
    public MediaStreamStatusEnum? MediaStreamStatus { get; set; }

    public SDPMediaAnnouncement()
    { }

    public SDPMediaAnnouncement(SDPMediaTypesEnum mediaType, int port, List<SDPAudioVideoMediaFormat> mediaFormats)
    {
        Media = mediaType;
        Port = port;
        MediaStreamStatus = DEFAULT_STREAM_STATUS;

        if (mediaFormats != null)
        {
            foreach (var fmt in mediaFormats)
            {
                if (!MediaFormats.ContainsKey(fmt.ID))
                {
                    MediaFormats.Add(fmt.ID, fmt);
                }
            }
        }
    }

    public void ParseMediaFormats(string formatList)
    {
        if (!string.IsNullOrWhiteSpace(formatList))
        {
            var formatIDs = Regex.Split(formatList, @"\s");
            foreach (var formatID in formatIDs)
            {
                if (Media == SDPMediaTypesEnum.application)
                {
                    ApplicationMediaFormats.Add(formatID, new SDPApplicationMediaFormat(formatID));
                }
                else if (Media == SDPMediaTypesEnum.message)
                {
                    //TODO
                }
                else
                {
                    if (int.TryParse(formatID, out var id)
                        && !MediaFormats.ContainsKey(id)
                        && id < SDPAudioVideoMediaFormat.DYNAMIC_ID_MIN)
                    {
                        if (Enum.IsDefined(typeof(SDPWellKnownMediaFormatsEnum), id) &&
                            Enum.TryParse<SDPWellKnownMediaFormatsEnum>(formatID, out var wellKnown))
                        {
                            MediaFormats.Add(id, new SDPAudioVideoMediaFormat(wellKnown));
                        }
                        else
                        {
                            logger.LogWarning($"Excluding unrecognised well known media format ID {id}.");
                        }
                    }
                }
            }
        }
    }

    public override string ToString()
    {
        var announcement = "m=" + Media + " " + Port + " " + Transport + " " + GetFormatListToString() + m_CRLF;

        announcement += !string.IsNullOrWhiteSpace(MediaDescription) ? "i=" + MediaDescription + m_CRLF : null;

        announcement += Connection == null ? null : Connection.ToString();

        if (TIASBandwidth > 0)
        {
            announcement += TIAS_BANDWIDTH_ATTRIBUE_PREFIX + TIASBandwidth + m_CRLF;
        }

        foreach (var bandwidthAttribute in BandwidthAttributes)
        {
            announcement += "b=" + bandwidthAttribute + m_CRLF;
        }

        announcement += !string.IsNullOrWhiteSpace(IceUfrag) ? "a=" + SDP.ICE_UFRAG_ATTRIBUTE_PREFIX + ":" + IceUfrag + m_CRLF : null;
        announcement += !string.IsNullOrWhiteSpace(IcePwd) ? "a=" + SDP.ICE_PWD_ATTRIBUTE_PREFIX + ":" + IcePwd + m_CRLF : null;
        announcement += !string.IsNullOrWhiteSpace(DtlsFingerprint) ? "a=" + SDP.DTLS_FINGERPRINT_ATTRIBUTE_PREFIX + ":" + DtlsFingerprint + m_CRLF : null;
        announcement += IceRole != null ? $"a={SDP.ICE_SETUP_ATTRIBUTE_PREFIX}:{IceRole}{m_CRLF}" : null;

        if (IceCandidates?.Count > 0)
        {
            foreach (var candidate in IceCandidates)
            {
                announcement += $"a={SDP.ICE_CANDIDATE_ATTRIBUTE_PREFIX}:{candidate}{m_CRLF}";
            }
        }

        if (IceOptions != null)
        {
            announcement += $"a={SDP.ICE_OPTIONS}:" + IceOptions + m_CRLF;
        }

        announcement += !string.IsNullOrWhiteSpace(MediaID) ? "a=" + SDP.MEDIA_ID_ATTRIBUTE_PREFIX + ":" + MediaID + m_CRLF : null;

        announcement += GetFormatListAttributesToString();

        announcement += string.Join("", HeaderExtensions.Select(x => $"{MEDIA_EXTENSION_MAP_ATTRIBUE_PREFIX}{x.Value.Id} {x.Value.Uri}{m_CRLF}"));
        foreach (var extra in _extraMediaAttributes)
        {
            announcement += string.IsNullOrWhiteSpace(extra) ? null : extra + m_CRLF;
        }

        foreach (var desc in _securityDescriptions)
        {
            announcement += desc + m_CRLF;
        }

        if (MediaStreamStatus != null)
        {
            announcement += MediaStreamStatusType.GetAttributeForMediaStreamStatus(MediaStreamStatus.Value) + m_CRLF;
        }

        if (SsrcGroupID != null && SsrcAttributes.Count > 0)
        {
            announcement += MEDIA_FORMAT_SSRC_GROUP_ATTRIBUE_PREFIX + SsrcGroupID;
            foreach (var ssrcAttr in SsrcAttributes)
            {
                announcement += $" {ssrcAttr.SSRC}";
            }
            announcement += m_CRLF;
        }

        if (SsrcAttributes.Count > 0)
        {
            foreach (var ssrcAttr in SsrcAttributes)
            {
                if (!string.IsNullOrWhiteSpace(ssrcAttr.Cname))
                {
                    announcement += $"{MEDIA_FORMAT_SSRC_ATTRIBUE_PREFIX}{ssrcAttr.SSRC} {SDPSsrcAttribute.MEDIA_CNAME_ATTRIBUE_PREFIX}:{ssrcAttr.Cname}" + m_CRLF;
                }
                else
                {
                    announcement += $"{MEDIA_FORMAT_SSRC_ATTRIBUE_PREFIX}{ssrcAttr.SSRC}" + m_CRLF;
                }
            }
        }

        // If the "sctpmap" attribute is set, use it instead of the separate "sctpport" and "max-message-size"
        // attributes. They both contain the same information. The "sctpmap" is the legacy attribute and if
        // an application sets it then it's likely to be for a specific reason.
        if (SctpMap != null)
        {
            announcement += $"{MEDIA_FORMAT_SCTP_MAP_ATTRIBUE_PREFIX}{SctpMap}" + m_CRLF;
        }
        else
        {
            if (SctpPort != null)
            {
                announcement += $"{MEDIA_FORMAT_SCTP_PORT_ATTRIBUE_PREFIX}{SctpPort}" + m_CRLF;
            }

            if (MaxMessageSize != 0)
            {
                announcement += $"{MEDIA_FORMAT_MAX_MESSAGE_SIZE_ATTRIBUE_PREFIX}{MaxMessageSize}" + m_CRLF;
            }
        }

        return announcement;
    }

    public string GetFormatListToString()
    {
        if (Media == SDPMediaTypesEnum.application)
        {
            var sb = new StringBuilder();
            foreach (var appFormat in ApplicationMediaFormats)
            {
                sb.Append(appFormat.Key);
                sb.Append(" ");
            }

            return sb.ToString().Trim();
        }

        if (Media == SDPMediaTypesEnum.message)
        {
            return "*";
        }

        string mediaFormatList = null;
        foreach (var mediaFormat in MediaFormats)
        {
            mediaFormatList += mediaFormat.Key + " ";
        }

        return mediaFormatList != null ? mediaFormatList.Trim() : null;
    }

    private string GetFormatListAttributesToString()
    {
        if (Media == SDPMediaTypesEnum.application)
        {
            if (ApplicationMediaFormats.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var appFormat in ApplicationMediaFormats)
                {
                    if (appFormat.Value.Rtpmap != null)
                    {
                        sb.Append($"{MEDIA_FORMAT_ATTRIBUE_PREFIX}{appFormat.Key} {appFormat.Value.Rtpmap}{m_CRLF}");
                    }

                    if (appFormat.Value.Fmtp != null)
                    {
                        sb.Append($"{MEDIA_FORMAT_PARAMETERS_ATTRIBUE_PREFIX}{appFormat.Key} {appFormat.Value.Fmtp}{m_CRLF}");
                    }
                }

                return sb.ToString();
            }

            return null;
        }

        if (Media == SDPMediaTypesEnum.message)
        {
            var sb = new StringBuilder();

            var mediaFormat = MessageMediaFormat;
            var acceptTypes = mediaFormat.AcceptTypes;
            if (acceptTypes != null && acceptTypes.Count >0)
            {
                sb.Append(MEDIA_FORMAT_PATH_ACCEPT_TYPES_PREFIX);
                foreach (var type in acceptTypes)
                {
                    sb.Append($"{type} ");
                }

                sb.Append($"{m_CRLF}");
            }

            if (mediaFormat.Endpoint != null )
            {
                sb.Append($"{MEDIA_FORMAT_PATH_MSRP_PREFIX}//{Connection.ConnectionAddress}:{Port}/{mediaFormat.Endpoint}{m_CRLF}");
            }

            return sb.ToString();
        }

        string formatAttributes = null;

        if (MediaFormats != null)
        {
            foreach (var mediaFormat in MediaFormats.Select(y => y.Value))
            {
                if (mediaFormat.Rtpmap == null)
                {
                    // Well known media formats are not required to add an rtpmap but we do so any way as some SIP
                    // stacks don't work without it.
                    formatAttributes += MEDIA_FORMAT_ATTRIBUE_PREFIX + mediaFormat.ID + " " + mediaFormat.Name() + "/" + mediaFormat.ClockRate() + m_CRLF;
                }
                else
                {
                    formatAttributes += MEDIA_FORMAT_ATTRIBUE_PREFIX + mediaFormat.ID + " " + mediaFormat.Rtpmap + m_CRLF;
                }

                if (mediaFormat.Fmtp != null)
                {
                    formatAttributes += MEDIA_FORMAT_PARAMETERS_ATTRIBUE_PREFIX + mediaFormat.ID + " " + mediaFormat.Fmtp + m_CRLF;
                }
            }
        }

        return formatAttributes;
    }

    public void AddExtra(string attribute)
    {
        if (!string.IsNullOrWhiteSpace(attribute))
        {
            _extraMediaAttributes.Add(attribute);
        }
    }

    public void AddCryptoLine(string crypto)
    {
        _securityDescriptions.Add(SDPSecurityDescription.Parse(crypto));
    }
}