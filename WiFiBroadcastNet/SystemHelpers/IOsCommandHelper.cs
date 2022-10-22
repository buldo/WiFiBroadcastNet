namespace WiFiBroadcastNet.SystemHelpers;

internal interface IOsCommandHelper
{
    void SetUnmanagedMode(string deviceName);

    void SetMonitorMode(string deviceName);

    void SetFrequency(string deviceName, Frequency frequency);
}