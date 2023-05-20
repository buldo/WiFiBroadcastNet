using WiFiBroadcastNet.SystemHelpers;

namespace WiFiBroadcastNet;

public class DeviceDescription
{
    internal DeviceDescription(
        NlInterface nlInterface,
        UeventDescription ueventDescription)
    {
        NlInterface = nlInterface;
        UeventDescription = ueventDescription;
    }

    public int PhyIndex => NlInterface.WiPhy;

    public string Name => NlInterface.IfName;

    public UeventDescription UeventDescription { get; }

    internal NlInterface NlInterface { get; }
}