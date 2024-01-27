using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN.STUNAttributes;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum STUNAttributeTypesEnum : ushort
{
    Unknown = 0,
    MappedAddress = 0x0001,
    ResponseAddress = 0x0002,       // Not used in RFC5389.
    ChangeRequest = 0x0003,         // Not used in RFC5389.
    SourceAddress = 0x0004,         // Not used in RFC5389.
    ChangedAddress = 0x0005,        // Not used in RFC5389.
    Username = 0x0006,
    Password = 0x0007,              // Not used in RFC5389.
    MessageIntegrity = 0x0008,
    ErrorCode = 0x0009,
    UnknownAttributes = 0x000A,
    ReflectedFrom = 0x000B,         // Not used in RFC5389.
    Realm = 0x0014,
    Nonce = 0x0015,
    XORMappedAddress = 0x0020,

    Software = 0x8022,              // Added in RFC5389.
    AlternateServer = 0x8023,       // Added in RFC5389.
    FingerPrint = 0x8028,           // Added in RFC5389.

    IceControlled = 0x8029,         // Added in RFC8445.
    IceControlling = 0x802a,        // Added in RFC8445.
    Priority = 0x0024,              // Added in RFC8445.

    UseCandidate = 0x0025,          // Added in RFC5245.

    // New attributes defined in TURN (RFC5766).
    ChannelNumber = 0x000C,
    Lifetime = 0x000D,
    XORPeerAddress = 0x0012,
    Data = 0x0013,
    XORRelayedAddress = 0x0016,
    EvenPort = 0x0018,
    RequestedTransport = 0x0019,
    DontFragment = 0x001A,
    ReservationToken = 0x0022,

    ConnectionId = 0x002a // Added in RFC6062.
}