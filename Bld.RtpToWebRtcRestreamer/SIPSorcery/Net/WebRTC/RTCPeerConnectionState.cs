namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;

/// <summary>
/// The states a peer connection transitions through.
/// The difference between the IceConnectionState and the PeerConnectionState is somewhat subtle:
/// - IceConnectionState: applies to the connection checks amongst ICE candidates and is
///   set as completed as soon as a local and remote candidate have set their nominated candidate,
/// - PeerConnectionState: takes into account the IceConnectionState but also includes the DTLS
///   handshake and actions at the application layer such as a request to close the peer connection.
/// </summary>
/// <remarks>
/// As specified in https://www.w3.org/TR/webrtc/#rtcpeerconnectionstate-enum.
/// </remarks>
public enum RTCPeerConnectionState
{
    closed,
    failed,
    disconnected,
    @new,
    connecting,
    connected
}