namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.ICE;

/// <summary>
/// The ICE set up roles that a peer can be in. The role determines how the DTLS
/// handshake is performed, i.e. which peer is the client and which is the server.
/// </summary>
internal enum IceImplementationEnum
{
    full,
    lite
}