using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WiFiBroadcastNet.Devices;

namespace WiFiBroadcastNet;

public class WiFiBroadcastInterface
{
    private readonly List<DeviceHandler> _deviceHandlers;

    public WiFiBroadcastInterface(IDevicesProvider devicesProvider)
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
    private readonly IDevice _device;

    public DeviceHandler(IDevice device)
    {
        _device = device;
    }

    public void Start()
    {
        _device.AttachReader();
    }
}