using System.Diagnostics.CodeAnalysis;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum STUNMessageTypesEnum : ushort
{
    BindingRequest = 0x0001,
    BindingSuccessResponse = 0x0101,
    BindingErrorResponse = 0x0111,

    // New methods defined in TURN (RFC5766).
    Allocate = 0x0003,
    Refresh = 0x0004,
    Send = 0x0006,
    Data = 0x0007,
    CreatePermission = 0x0008,
    ChannelBind = 0x0009,

    SendIndication = 0x0016,
    DataIndication = 0x0017,

    AllocateSuccessResponse = 0x0103,
    RefreshSuccessResponse = 0x0104,
    CreatePermissionSuccessResponse = 0x0108,
    ChannelBindSuccessResponse = 0x0109,
    AllocateErrorResponse = 0x0113,
    RefreshErrorResponse = 0x0114,
    CreatePermissionErrorResponse = 0x0118,
    ChannelBindErrorResponse = 0x0119,

    // New methods defined in TURN (RFC6062).
    Connect = 0x000a,
    ConnectionBind = 0x000b,
    ConnectionAttempt = 0x000c
}