namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN;

public static class STUNMessageTypes
{
    public static STUNMessageTypesEnum GetSTUNMessageTypeForId(int stunMessageTypeId)
    {
        return (STUNMessageTypesEnum)Enum.Parse(typeof(STUNMessageTypesEnum), stunMessageTypeId.ToString(), true);
    }
}