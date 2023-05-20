using System.Diagnostics;
using System.Text;
using Bld.Libnl.Net.Nl80211;
using Bld.Libnl.Net.Nl80211.Enums;

namespace WiFiBroadcastNet.SystemHelpers;

internal class LinuxHelpers
{
    private static readonly Nl80211Wrapper _nl80211Wrapper = new Nl80211Wrapper();

    public List<NlInterface> GetWirelessInterfaces()
    {
        var interfaces = new List<NlInterface>();
        var dumpMessages = _nl80211Wrapper.DumpInterfaces();
        foreach (var message in dumpMessages)
        {
            var name = (string)message.Attributes[nl80211_attrs.NL80211_ATTR_IFNAME].GetValue();
            var wiPhy = (int)message.Attributes[nl80211_attrs.NL80211_ATTR_WIPHY].GetValue();
            var iface = new NlInterface()
            {
                IfName = name,
                WiPhy = wiPhy,
            };
            interfaces.Add(iface);
        }

        return interfaces;
    }

    public void SetUnmanagedMode(string deviceName)
    {
        Execute("nmcli", $"device set {deviceName} managed no");
    }

    public void SetMonitorMode(string deviceName)
    {
        Execute("iw", $"dev {deviceName} set monitor otherbss");
    }

    public void SetFrequency(string deviceName, Frequency frequency)
    {
        Execute("iw", $"dev {deviceName} set freq {frequency.ValueInMHz}");
    }

    private void Execute(string name, string args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = name,
            Arguments = args,
            // WorkingDirectory =
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8
        };


        var process = new Process(){StartInfo = startInfo};
        process.OutputDataReceived += (sender, eventArgs) => Console.WriteLine(eventArgs.Data);
        process.ErrorDataReceived += (sender, eventArgs) => Console.WriteLine(eventArgs.Data);
        process.Start();
        process?.WaitForExit(TimeSpan.FromSeconds(10));
    }
}