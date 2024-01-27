namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.STUN.STUNAttributes;

public static class STUNAttributeTypes
{
    public static STUNAttributeTypesEnum GetSTUNAttributeTypeForId(int stunAttributeTypeId)
    {
        return (STUNAttributeTypesEnum)Enum.Parse(typeof(STUNAttributeTypesEnum), stunAttributeTypeId.ToString(), true);
    }
}