namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// If ICE servers (STUN or TURN) are being used with the session this class is used to track
/// the connection state for each server that gets used.
/// </summary>
internal static class IceServerConsts
{
    /// <summary>
    /// The maximum number of requests to send to an ICE server without getting
    /// a response.
    /// </summary>
    internal const int MAX_REQUESTS = 25;

    /// <summary>
    /// The STUN error code response indicating an authenticated request is required.
    /// </summary>
    internal const int STUN_UNAUTHORISED_ERROR_CODE = 401;

    /// <summary>
    /// The STUN error code response indicating a stale nonce
    /// </summary>
    internal const int STUN_STALE_NONCE_ERROR_CODE = 438;
}