using Bld.RtpToWebRtcRestreamer.RtpNg.Networking;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtp;

internal class VideoStream : MediaStream
{
    public VideoStream(MediaStreamTrack track, MultiplexedRtpChannel rtpChannel)
        : base(track, rtpChannel)
    {
    }

    /// <summary>
    /// Indicates whether this session is using video.
    /// </summary>
    public bool HasVideo
    {
        get
        {
            return LocalTrack != null && LocalTrack.StreamStatus != MediaStreamStatusEnum.Inactive;
        }
    }

    public override SDPMediaTypesEnum MediaType => SDPMediaTypesEnum.video;
}