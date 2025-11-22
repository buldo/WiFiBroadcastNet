using WiFiBroadcastNet.Radio.Common;

namespace WiFiBroadcastNet;

public static class RxFrameExtensions
{
    public static RadioPort get_valid_radio_port(this RxFrame frame)
    {
        return RadioPort.FromByte(frame.MacSrcRadioPort[0]);
    }

    public static bool IsValidWfbFrame(this RxFrame frame)
    {
        if (frame.Data.Length <= 0)
        {
            return false;
        }

        if (!frame.IsDataFrame())
        {
            return false;
        }

        if (frame.PayloadSpan.Length == 0)
        {
            return false;
        }

        if (!frame.HasValidAirGndId())
        {
            return false;
        }

        if (!frame.HasValidRadioPort())
        {
            return false;
        }

        // TODO: add `frame.PayloadSpan.Length > RAW_WIFI_FRAME_MAX_PAYLOAD_SIZE`

        return true;
    }

    public static byte GetValidAirGndId(this RxFrame frame)
    {
        return frame.MacSrcUniqueIdPart[0];
    }

    /// <summary>
    /// Check - first byte of scr and dst mac needs to mach (unique air / gnd id)
    /// </summary>
    public static bool HasValidAirGndId(this RxFrame frame)
    {
        return frame.MacSrcUniqueIdPart[0] == frame.MacDstUniqueIdPart[0];
    }

    /// <summary>
    /// Check - last byte of src and dst mac needs to match (radio port)
    /// </summary>
    public static bool HasValidRadioPort(this RxFrame frame)
    {
        return frame.MacSrcRadioPort[0] == frame.MacDstRadioPort[0];
    }
}