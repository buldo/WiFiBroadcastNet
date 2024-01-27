//-----------------------------------------------------------------------------
// Filename: SDPMediaFormat.cs
//
// Description:
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// ??	        Aaron Clauson	Created, Hobart, Australia.
// 18 Oct 2020  Aaron Clauson   Renamed from SDPMediaFormat to SDPAudioVideoMediaFormat.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using SIPSorceryMedia.Abstractions;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

/// <summary>
/// Represents a single media format within a media announcement. Often the whole media format can
/// be represented and described by a single character, e.g. "0" without additional info represents
/// standard "PCMU", "8" represents "PCMA" etc. For other media types that have variable parameters
/// additional attributes can be provided.
/// </summary>
/// <remarks>This struct is designed to be immutable. If new information becomes available for a
/// media format, such as when parsing further into an SDP payload, a new media format should be
/// created.
/// TODO: With C#9 the struct could become a "record" type.
/// </remarks>
internal struct SDPAudioVideoMediaFormat
{
    public const int DYNAMIC_ID_MIN = 96;
    private const int DYNAMIC_ID_MAX = 127;
    private const int DEFAULT_AUDIO_CHANNEL_COUNT = 1;

    /// <summary>
    /// Indicates whether the format is for audio or video.
    /// </summary>
    private SDPMediaTypesEnum Kind { get; }

    /// <summary>
    /// The mandatory ID for the media format. Warning, even though some ID's are normally used to represent
    /// a standard media type, e.g "0" for "PCMU" etc, there is no guarantee that's the case. "0" can be used
    /// for any media format if there is a format attribute describing it. In the absence of a format attribute
    /// then it is required that it represents a standard media type.
    ///
    /// Note (rj2): FormatID MUST be string (not int), in case ID is 't38' and type is 'image'
    /// Note to above: The FormatID is always numeric for profile "RTP/AVP" and "RTP/SAVP", see
    /// https://tools.ietf.org/html/rfc4566#section-5.14 and section on "fmt":
    /// "If the <proto> sub-field is "RTP/AVP" or "RTP/SAVP" the <fmt>
    /// sub-fields contain RTP payload type numbers"
    /// In the case of T38 the format name is "t38" but the formatID must be set as a dynamic ID.
    /// <code>
    /// // Example
    /// // Note in this example "0" is representing a standard format so the format attribute is optional.
    /// m=audio 12228 RTP/AVP 0 101         // "0" and "101" are media format ID's.
    /// a=rtpmap:0 PCMU/8000                // "0" is the media format ID.
    /// a=rtpmap:101 telephone-event/8000   // "101" is the media format ID.
    /// a=fmtp:101 0-16
    /// </code>
    /// <code>
    /// // t38 example from https://tools.ietf.org/html/rfc4612.
    /// m=audio 6800 RTP/AVP 0 98
    /// a=rtpmap:98 t38/8000
    /// a=fmtp:98 T38FaxVersion=2;T38FaxRateManagement=transferredTCF
    /// </code>
    /// </summary>
    public int ID { get; }

    /// <summary>
    /// The optional rtpmap attribute properties for the media format. For standard media types this is not necessary.
    /// <code>
    /// // Example
    /// a=rtpmap:0 PCMU/8000
    /// a=rtpmap:101 telephone-event/8000 <-- "101 telephone-event/8000" is the rtpmap properties.
    /// a=fmtp:101 0-16
    /// </code>
    /// </summary>
    public string Rtpmap { get; }

    /// <summary>
    /// The optional format parameter attribute for the media format. For standard media types this is not necessary.
    /// <code>
    /// // Example
    /// a=rtpmap:0 PCMU/8000
    /// a=rtpmap:101 telephone-event/8000
    /// a=fmtp:101 0-16                     <-- "101 0-16" is the fmtp attribute.
    /// </code>
    /// </summary>
    public string Fmtp { get; }

    /// <summary>
    /// Creates a new SDP media format for a well known media type. Well known type are those that use
    /// ID's less than 96 and don't require rtpmap or fmtp attributes.
    /// </summary>
    public SDPAudioVideoMediaFormat(SDPWellKnownMediaFormatsEnum knownFormat)
    {
        Kind = AudioVideoWellKnown.WellKnownAudioFormats.ContainsKey(knownFormat) ? SDPMediaTypesEnum.audio :
            SDPMediaTypesEnum.video;
        ID = (int)knownFormat;
        Rtpmap = null;
        Fmtp = null;

        if (Kind == SDPMediaTypesEnum.audio)
        {
            var audioFormat = AudioVideoWellKnown.WellKnownAudioFormats[knownFormat];
            Rtpmap = SetRtpmap(audioFormat.FormatName, audioFormat.RtpClockRate, audioFormat.ChannelCount);
        }
        else
        {
            var videoFormat = AudioVideoWellKnown.WellKnownVideoFormats[knownFormat];
            Rtpmap = SetRtpmap(videoFormat.FormatName, videoFormat.ClockRate, 0);
        }
    }

    /// <summary>
    /// Creates a new SDP media format for a dynamic media type. Dynamic media types are those that use
    /// ID's between 96 and 127 inclusive and require an rtpmap attribute and optionally an fmtp attribute.
    /// </summary>
    public SDPAudioVideoMediaFormat(SDPMediaTypesEnum kind, int id, string rtpmap, string fmtp = null)
    {
        if (id < 0 || id > DYNAMIC_ID_MAX)
        {
            throw new ApplicationException($"SDP media format IDs must be between 0 and {DYNAMIC_ID_MAX}.");
        }

        if (string.IsNullOrWhiteSpace(rtpmap))
        {
            throw new ArgumentNullException("rtpmap", "The rtpmap parameter cannot be empty for a dynamic SDPMediaFormat.");
        }

        Kind = kind;
        ID = id;
        Rtpmap = rtpmap;
        Fmtp = fmtp;
    }

    /// <summary>
    /// Creates a new SDP media format from a Video Format instance. The Video Format contains the
    /// equivalent information to the SDP format object but has well defined video properties separate
    /// from the SDP serialisation.
    /// </summary>
    /// <param name="videoFormat">The Video Format to map to an SDP format.</param>
    public SDPAudioVideoMediaFormat(VideoFormat videoFormat)
    {
        Kind = SDPMediaTypesEnum.video;
        ID = videoFormat.FormatID;
        Rtpmap = null;
        Fmtp = videoFormat.Parameters;

        Rtpmap = SetRtpmap(videoFormat.FormatName, videoFormat.ClockRate);
    }

    private string SetRtpmap(string name, int clockRate, int channels = DEFAULT_AUDIO_CHANNEL_COUNT)
        =>
            Kind == SDPMediaTypesEnum.video ? $"{name}/{clockRate}" :
            channels == DEFAULT_AUDIO_CHANNEL_COUNT ? $"{name}/{clockRate}" : $"{name}/{clockRate}/{channels}";
    public int ClockRate() => Kind == SDPMediaTypesEnum.video ? ToVideoFormat().ClockRate : ToAudioFormat().ClockRate;

    public string Name()
    {
        // Rtpmap taks priority over well known media type as ID's can be changed.
        if (Rtpmap != null && TryParseRtpmap(Rtpmap, out var name, out _, out _))
        {
            return name;
        }

        if (Enum.IsDefined(typeof(SDPWellKnownMediaFormatsEnum), ID))
        {
            // If no rtpmap available then it must be a well known format.
            return Enum.ToObject(typeof(SDPWellKnownMediaFormatsEnum), ID).ToString();
        }

        return null;
    }

    public SDPAudioVideoMediaFormat WithUpdatedRtpmap(string rtpmap, SDPAudioVideoMediaFormat format) =>
        new(format.Kind, format.ID, rtpmap, format.Fmtp);

    public SDPAudioVideoMediaFormat WithUpdatedFmtp(string fmtp, SDPAudioVideoMediaFormat format) =>
        new(format.Kind, format.ID, format.Rtpmap, fmtp);

    /// <summary>
    /// Maps an audio SDP media type to a media abstraction layer audio format.
    /// </summary>
    /// <returns>An audio format value.</returns>
    private AudioFormat ToAudioFormat()
    {
        // Rtpmap takes priority over well known media type as ID's can be changed.
        if (Rtpmap != null && TryParseRtpmap(Rtpmap, out var name, out var rtpClockRate, out var channels))
        {
            var clockRate = rtpClockRate;

            // G722 is a special case. It's the only audio format that uses the wrong RTP clock rate.
            // It sets 8000 in the SDP but then expects samples to be sent as 16KHz.
            // See https://tools.ietf.org/html/rfc3551#section-4.5.2.
            if (name == "G722" && rtpClockRate == 8000)
            {
                clockRate = 16000;
            }

            return new AudioFormat(ID, name, clockRate, rtpClockRate, channels, Fmtp);
        }

        if (ID < DYNAMIC_ID_MIN
            && Enum.TryParse<SDPWellKnownMediaFormatsEnum>(Name(), out var wellKnownFormat)
            && AudioVideoWellKnown.WellKnownAudioFormats.ContainsKey(wellKnownFormat))
        {
            return AudioVideoWellKnown.WellKnownAudioFormats[wellKnownFormat];
        }

        return AudioFormat.Empty;
    }

    /// <summary>
    /// Maps a video SDP media type to a media abstraction layer video format.
    /// </summary>
    /// <returns>A video format value.</returns>
    private VideoFormat ToVideoFormat()
    {
        // Rtpmap taks priority over well known media type as ID's can be changed.
        // But we don't currently support any of the well known video types any way.
        if (TryParseRtpmap(Rtpmap, out var name, out var clockRate, out _))
        {
            return new VideoFormat(ID, name, clockRate, Fmtp);
        }

        return VideoFormat.Empty;
    }

    /// <summary>
    /// Sort capabilities array based on another capability array
    /// </summary>
    /// <param name="capabilities"></param>
    /// <param name="priorityOrder"></param>
    public static void SortMediaCapability(List<SDPAudioVideoMediaFormat> capabilities, List<SDPAudioVideoMediaFormat> priorityOrder)
    {
        //Fix Capabilities Order
        if (priorityOrder != null && capabilities != null)
        {
            capabilities.Sort((a, b) =>
            {
                //Sort By Indexes
                var aSort = priorityOrder.FindIndex(c => c.ID == a.ID);
                var bSort = priorityOrder.FindIndex(c => c.ID == b.ID);

                //Sort Values
                if (aSort < 0)
                {
                    aSort = int.MaxValue;
                }
                if (bSort < 0)
                {
                    bSort = int.MaxValue;
                }

                return aSort.CompareTo(bSort);
            });
        }
    }

    /// <summary>
    /// Parses an rtpmap attribute in the form "name/clock" or "name/clock/channels".
    /// </summary>
    private static bool TryParseRtpmap(string rtpmap, out string name, out int clockRate, out int channels)
    {
        name = null;
        clockRate = 0;
        channels = DEFAULT_AUDIO_CHANNEL_COUNT;

        if (string.IsNullOrWhiteSpace(rtpmap))
        {
            return false;
        }

        var fields = rtpmap.Trim().Split('/');

        if (fields.Length >= 2)
        {
            name = fields[0].Trim();
            if (!int.TryParse(fields[1].Trim(), out clockRate))
            {
                return false;
            }

            if (fields.Length >= 3)
            {
                if (!int.TryParse(fields[2].Trim(), out channels))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }
}