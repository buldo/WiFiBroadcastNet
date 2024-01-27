//-----------------------------------------------------------------------------
// Filename: MediaStreamTrack.cs
//
// Description: Represents a one-way audio or video stream. In a typical call
// a media session could have 4 tracks, local and remote audio and video.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 26 Aug 2020	Aaron Clauson	Refactored from RTPSession.
// 15 Oct 2020  Aaron Clauson   Added media format map lookup class.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;
using SIPSorceryMedia.Abstractions;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;

internal class MediaStreamTrack
{
    // The value used in the RTP Sequence Number header field for media packets.
    // Although valid values are all in the range of ushort, the underlying field is of type int, because Interlocked.CompareExchange is used to increment in a fast and thread-safe manner and there is no overload for ushort.
    private int _seqNum;

    /// <summary>
    /// Add a local video track.
    /// </summary>
    /// <param name="format">The video format that the local application supports.</param>
    /// <param name="streamStatus">Optional. The stream status for the video track, e.g. whether
    /// send and receive or only one of.</param>
    public MediaStreamTrack(
        VideoFormat format,
        MediaStreamStatusEnum streamStatus)
        : this(SDPMediaTypesEnum.video, new List<SDPAudioVideoMediaFormat> { new(format) }, streamStatus)
    {
    }

    /// <summary>
    /// Creates a lightweight class to track a media stream track within an RTP session
    /// When supporting RFC3550 (the standard RTP specification) the relationship between
    /// an RTP stream and session is 1:1. For WebRTC and RFC8101 there can be multiple
    /// streams per session.
    /// </summary>
    /// <param name="kind">The type of media for this stream. There can only be one
    /// stream per media type.</param>
    /// <param name="capabilities">The capabilities for the track being added. Where the same media
    /// type is supported locally and remotely only the mutual capabilities can be used. This will
    /// occur if we receive an SDP offer (add track initiated by the remote party) and we need
    /// to remove capabilities we don't support.</param>
    /// <param name="streamStatus">The initial stream status for the media track. Defaults to
    /// send receive.</param>
    private MediaStreamTrack(
        SDPMediaTypesEnum kind,
        List<SDPAudioVideoMediaFormat> capabilities,
        MediaStreamStatusEnum streamStatus)
    {
        Kind = kind;
        Capabilities = capabilities;
        StreamStatus = streamStatus;
        DefaultStreamStatus = streamStatus;

        Ssrc = Convert.ToUInt32(Random.Shared.Next(0, int.MaxValue));
        _seqNum = Convert.ToUInt16(Random.Shared.Next(0, ushort.MaxValue));
    }

    /// <summary>
    /// The type of media stream represented by this track. Must be audio or video.
    /// </summary>
    public SDPMediaTypesEnum Kind { get; }

    /// <summary>
    /// The value used in the RTP Synchronisation Source header field for media packets
    /// sent using this media stream.
    /// Be careful that the RTP Synchronisation Source header field should not be changed
    /// unless specific implementations require it. By default this value is chosen randomly,
    /// with the intent that no two synchronization sources within the same RTP session
    /// will have the same SSRC.
    /// </summary>
    public uint Ssrc { get; }

    /// <summary>
    /// The media capabilities supported by this track.
    /// </summary>
    public List<SDPAudioVideoMediaFormat> Capabilities { get; }

    /// <summary>
    /// Represents the original and default stream status for the track. This is set
    /// when the track is created and does not change. It allows tracks to be set back to
    /// their original state after being put on hold etc. For example if a track is
    /// added as receive only video source then when after on and off hold it needs to
    /// be known that the track reverts receive only rather than sendrecv.
    /// </summary>
    public MediaStreamStatusEnum DefaultStreamStatus { get; }

    /// <summary>
    /// Holds the stream state of the track.
    /// </summary>
    public MediaStreamStatusEnum StreamStatus { get; internal set; }

    public string Cname { get;} = Guid.NewGuid().ToString();

    /// <summary>
    /// Returns the next SeqNum to be used in the RTP Sequence Number header field for media packets
    /// sent using this media stream.
    /// </summary>
    /// <returns></returns>
    public ushort GetNextSeqNum()
    {
        var actualSeqNum = _seqNum;
        int expectedSeqNum;
        var attempts = 0;
        do
        {
            if (++attempts > 10)
            {
                throw new ApplicationException("GetNextSeqNum did not return an the next SeqNum due to concurrent updates from other threads within 10 attempts.");
            }
            expectedSeqNum = actualSeqNum;
            var nextSeqNum = actualSeqNum >= ushort.MaxValue ? 0 : (ushort)(actualSeqNum + 1);
            actualSeqNum = Interlocked.CompareExchange(ref _seqNum, nextSeqNum, expectedSeqNum);
        } while (expectedSeqNum != actualSeqNum); // Try as long as compare-exchange was not successful; in most cases, only one iteration should be needed

        return (ushort)expectedSeqNum;
    }
}