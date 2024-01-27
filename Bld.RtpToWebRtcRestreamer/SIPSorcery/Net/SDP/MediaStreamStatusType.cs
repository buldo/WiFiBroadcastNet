namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SDP;

public static class MediaStreamStatusType
{
    private const string SEND_RECV_ATTRIBUTE = "a=sendrecv";
    private const string SEND_ONLY_ATTRIBUTE = "a=sendonly";
    private const string RECV_ONLY_ATTRIBUTE = "a=recvonly";
    private const string INACTIVE_ATTRIBUTE = "a=inactive";

    /// <summary>
    /// Checks whether an SDP attribute is one of the four possible media stream attributes.
    /// </summary>
    /// <param name="attributeString">The attribute string to check.</param>
    /// <param name="mediaStreamStatus">If the attribute was recognised as a media stream attribute this will hold it.</param>
    /// <returns>True if the attribute matched or false if not.</returns>
    public static bool IsMediaStreamStatusAttribute(string attributeString, out MediaStreamStatusEnum mediaStreamStatus)
    {
        mediaStreamStatus = MediaStreamStatusEnum.SendRecv;

        if (string.IsNullOrEmpty(attributeString))
        {
            return false;
        }

        switch (attributeString.ToLower())
        {
            case SEND_RECV_ATTRIBUTE:
                mediaStreamStatus = MediaStreamStatusEnum.SendRecv;
                return true;
            case SEND_ONLY_ATTRIBUTE:
                mediaStreamStatus = MediaStreamStatusEnum.SendOnly;
                return true;
            case RECV_ONLY_ATTRIBUTE:
                mediaStreamStatus = MediaStreamStatusEnum.RecvOnly;
                return true;
            case INACTIVE_ATTRIBUTE:
                mediaStreamStatus = MediaStreamStatusEnum.Inactive;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Gets the attribute to include in a serialised SDP payload for a media stream status.
    /// </summary>
    /// <param name="mediaStreamStatus">The status to get the attribute for.</param>
    /// <returns>An attribute string matching the status value.</returns>
    public static string GetAttributeForMediaStreamStatus(MediaStreamStatusEnum mediaStreamStatus)
    {
        switch (mediaStreamStatus)
        {
            case MediaStreamStatusEnum.SendRecv:
                return SEND_RECV_ATTRIBUTE;
            case MediaStreamStatusEnum.SendOnly:
                return SEND_ONLY_ATTRIBUTE;
            case MediaStreamStatusEnum.RecvOnly:
                return RECV_ONLY_ATTRIBUTE;
            case MediaStreamStatusEnum.Inactive:
                return INACTIVE_ATTRIBUTE;
            default:
                // Default is to use sendrecv.
                return SEND_RECV_ATTRIBUTE;
        }
    }
}