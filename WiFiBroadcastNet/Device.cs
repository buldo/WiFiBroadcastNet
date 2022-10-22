using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

using SharpPcap;
using SharpPcap.LibPcap;

using WiFiBroadcastNet.SystemHelpers;

namespace WiFiBroadcastNet;

public class Device
{
    private readonly ILiveDevice _realDevice;
    private readonly NetworkInterface _networkInterface;
    private readonly IOsCommandHelper _commandHelper;

    internal Device(
        ILiveDevice realDevice, 
        NetworkInterface networkInterface, 
        IOsCommandHelper commandHelper)
    {
        _realDevice = realDevice;
        _networkInterface = networkInterface;
        _commandHelper = commandHelper;
    }

    internal void PrepareOs()
    {
        _commandHelper.SetMonitorMode(_realDevice.Name);
    }
}