namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// The states an ICE session transitions through.
/// </summary>
/// <remarks>
/// As specified in https://www.w3.org/TR/webrtc/#rtciceconnectionstate-enum.
/// </remarks>
internal enum RTCIceConnectionState
{
    /// <summary>
    /// The connection has been closed. All checks stop.
    /// </summary>
    closed,

    /// <summary>
    /// The connection attempt has failed or connection checks on an established
    /// connection have failed.
    /// </summary>
    failed,

    /// <summary>
    /// Connection attempts on an established connection have failed. Attempts
    /// will continue until the state transitions to failure.
    /// </summary>
    disconnected,

    /// <summary>
    /// The initial state.
    /// </summary>
    @new,

    /// <summary>
    /// Checks are being carried out in an attempt to establish a connection.
    /// </summary>
    checking,

    /// <summary>
    /// The checks have been successful and the connection has been established.
    /// </summary>
    connected
}