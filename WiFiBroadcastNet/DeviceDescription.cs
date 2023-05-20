using System.Net.NetworkInformation;

namespace WiFiBroadcastNet;

public class DeviceDescription
{
    public NetworkInterface Interface { get; internal init; }

    public int PhyIndex { get; internal init; }
    
    public UeventDescription UeventDescription { get; internal init; }
}