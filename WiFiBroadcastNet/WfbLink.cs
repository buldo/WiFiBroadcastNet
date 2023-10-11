using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WiFiBroadcastNet.Devices;

namespace WiFiBroadcastNet;

public class WfbLink
{
    private readonly List<DeviceHandler> _deviceHandlers;

    public WfbLink(IDevicesProvider devicesProvider)
    {
        _deviceHandlers = devicesProvider
            .GetDevices()
            .Select(device => new DeviceHandler(device))
            .ToList();
    }

    public void Start()
    {
        foreach (var handler in _deviceHandlers)
        {
            handler.Start();
        }
    }
}

internal class DeviceHandler
{
    private readonly IRadioDevice _device;

    public DeviceHandler(IRadioDevice device)
    {
        _device = device;
    }

    public void Start()
    {

    }
}