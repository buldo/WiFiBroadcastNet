using Bld.Libnl.Net.Netlink;

namespace SendSinglePacketDemo;

internal class NlWiPhy
{
    public required uint WiPhy { get; init; }

    public required string WiPhyName { get; init; }

    public required IfMode[] Modes { get; init; }
}