namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.RTP;

internal enum SetDescriptionResultEnum
{
    /// <summary>
    /// At least one media stream with a compatible format was available.
    /// </summary>
    OK,

    /// <summary>
    /// Both parties had audio but no compatible format was available.
    /// </summary>
    AudioIncompatible,

    /// <summary>
    /// Both parties had video but no compatible format was available.
    /// </summary>
    VideoIncompatible,

    /// <summary>
    /// The remote description did not contain any media announcements.
    /// </summary>
    NoRemoteMedia,

    /// <summary>
    /// Indicates there was no media type match. For example only have audio locally
    /// but video remote or vice-versa.
    /// </summary>
    NoMatchingMediaType,

    /// <summary>
    /// An unknown error.
    /// </summary>
    Error,

    /// <summary>
    /// A required DTLS fingerprint was missing from the session description.
    /// </summary>
    DtlsFingerprintMissing,

    /// <summary>
    /// The DTLS fingerprint was provided with an unsupported digest. It won't
    /// be possible to check that the certificate supplied during the DTLS handshake
    /// matched the fingerprint.
    /// </summary>
    DtlsFingerprintDigestNotSupported,

    /// <summary>
    /// An unsupported data channel transport was requested (at the time of writing only
    /// SCTP over DTLS is supported, no TCP option).
    /// </summary>
    DataChannelTransportNotSupported,

    /// <summary>
    /// An SDP offer was received when the local agent had already entered have local offer state.
    /// </summary>
    WrongSdpTypeOfferAfterOffer
}