namespace WiFiBroadcastNet;

public static class OpenHdRadioPorts
{
    public static byte VIDEO_PRIMARY_RADIO_PORT { get; } = 10;

    public static byte VIDEO_SECONDARY_RADIO_PORT { get; } = 11;

    public static byte MANAGEMENT_RADIO_PORT_AIR_TX { get; } = 20;

    public static byte MANAGEMENT_RADIO_PORT_GND_TX { get; } = 21;

    public static byte TELEMETRY_WIFIBROADCAST_RX_RADIO_PORT { get; } = 3;

    public static byte TELEMETRY_WIFIBROADCAST_TX_RADIO_PORT { get; } = 4;
}