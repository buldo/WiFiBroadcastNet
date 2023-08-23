using System.Diagnostics;

using Bld.Libnl.Net.Netlink;
using Bld.Libnl.Net.Nl80211;
using Bld.Libnl.Net.Nl80211.Enums;

using RunProcessAsTask;

namespace SendSinglePacketDemo
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var interfaces = LinuxDevicesHelper.GetWirelessInterfaces();
            foreach (var nlInterface in interfaces)
            {
                Console.WriteLine($"{nlInterface.InterfaceName} {nlInterface.WiPhy}");
            }
            //var runResult = await ProcessEx.RunAsync("iw", $"{device.InterfaceName} set monitor otherbss");
            //return string.Join(Environment.NewLine, runResult.StandardOutput.Concat(runResult.StandardError));
        }
    }


    internal static class LinuxDevicesHelper
    {
        public static List<NlWiPhy> GetWirelessPhy()
        {
            var interfaces = new List<NlWiPhy>();
            var nl80211Wrapper = new Nl80211Wrapper();
            var dumpMessages = nl80211Wrapper.DumpWiphy();
            foreach (var message in dumpMessages)
            {
                var name = (string)message.Attributes[nl80211_attrs.NL80211_ATTR_WIPHY_NAME].GetValue();
                var wiPhy = (UInt32)message.Attributes[nl80211_attrs.NL80211_ATTR_WIPHY].GetValue();
                var modes = (IfMode[])message.Attributes[nl80211_attrs.NL80211_ATTR_SUPPORTED_IFTYPES].GetValue();
                var iface = new NlWiPhy()
                {
                    WiPhyName = name,
                    WiPhy = wiPhy,
                    Modes = modes
                };
                interfaces.Add(iface);
            }

            return interfaces;
        }

        public static List<NlInterface> GetWirelessInterfaces()
        {
            var interfaces = new List<NlInterface>();
            var nl80211Wrapper = new Nl80211Wrapper();
            var dumpMessages = nl80211Wrapper.DumpInterfaces();
            foreach (var message in dumpMessages)
            {
                var ifName = (string)message.Attributes[nl80211_attrs.NL80211_ATTR_IFNAME].GetValue();
                var wiPhy = (UInt32)message.Attributes[nl80211_attrs.NL80211_ATTR_WIPHY].GetValue();
                var iface = new NlInterface
                {
                    WiPhy = wiPhy,
                    InterfaceName = ifName
                };
                interfaces.Add(iface);
            }

            return interfaces;
        }
    }
}
