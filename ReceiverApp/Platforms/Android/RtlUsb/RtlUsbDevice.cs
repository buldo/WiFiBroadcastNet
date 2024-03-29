﻿using System.Threading.Channels;
using Android.Hardware.Usb;
using Microsoft.Extensions.Logging;
using Rtl8812auNet.Abstractions;

namespace ReceiverApp
{
    internal class RtlUsbDevice : IRtlUsbDevice
    {
        private readonly Channel<byte[]> _bulkTransfersChannel = Channel.CreateUnbounded<byte[]>();
        private readonly UsbDevice _usbDevice;
        private readonly UsbDeviceConnection _usbDeviceConnection;
        private readonly ILogger<RtlUsbDevice> _logger;
        private readonly object _usbConnectionLock = new();
        private BulkDataHandler? _bulkDataHandler;

        public RtlUsbDevice(
            UsbDevice usbDevice,
            UsbDeviceConnection usbDeviceConnection,
            ILogger<RtlUsbDevice> logger)
        {
            _usbDevice = usbDevice;
            _usbDeviceConnection = usbDeviceConnection;
            _logger = logger;

            //_usbDevice.Open();
            //_usbDevice.SetConfiguration(1);
            //_usbDevice.ClaimInterface(0);

            //_reader = _usbDevice.OpenEndpointReader(GetInEp());
        }

        public void SetBulkDataHandler(BulkDataHandler handler)
        {
            _bulkDataHandler = handler;
        }

        public void InfinityRead()
        {
            var ep = GetInEp();
            var readBuffer = new byte[32768];
            while (true)
            {
                try
                {
                    int length;
                    lock (_usbConnectionLock)
                    {
                        length = _usbDeviceConnection.BulkTransfer(ep, readBuffer, readBuffer.Length, 0 );
                    }

                    if (length <0)
                    {
                        _logger.LogError("BULK read ERR {result}", length);
                    }
                    else
                    {
                        _bulkDataHandler?.Invoke(readBuffer.AsSpan(0, length));
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "_reader.Read error");
                }
            }
        }

        private UsbEndpoint GetInEp()
        {
            var iface = _usbDevice.GetConfiguration(0).GetInterface(0);

            for (int i = 0; i < iface.EndpointCount; i++)
            {
                var ep = iface.GetEndpoint(i);
                if (ep.Direction == UsbAddressing.In && ep.Type == UsbAddressing.XferBulk)
                {
                    return ep;
                }
            }

            throw new Exception("Read EP not found");
        }

        public int Speed { get; } = 3;


        public void WriteBytes(ushort register, Span<byte> data)
        {
            lock (_usbConnectionLock)
            {
                _usbDeviceConnection.ControlTransfer(
                    (UsbAddressing)0x40,
                    5,
                    register,
                    0,
                    data.ToArray(),
                    data.Length,
                    50);
            }
        }

        public ReadOnlySpan<byte> ReadBytes(ushort register, ushort bytesCount)
        {
            lock (_usbConnectionLock)
            {
                var buffer = new byte[bytesCount];
                _usbDeviceConnection.ControlTransfer(
                    (UsbAddressing)0xC0,
                    5,
                    register,
                    0,
                    buffer,
                    buffer.Length,
                    50);

                return buffer;
            }
        }

        public List<IRtlEndpoint> GetEndpoints()
        {
            var configuration = _usbDevice.GetConfiguration(0);
            var iface = configuration.GetInterface(0);

            var ret = new List<IRtlEndpoint>();
            for (int i = 0; i < iface.EndpointCount; i++)
            {
                var ep = iface.GetEndpoint(i);
                RtlEndpointType type = ep.Type switch
                {
                    UsbAddressing.XferBulk => RtlEndpointType.Bulk,
                    UsbAddressing.XferInterrupt => RtlEndpointType.Interrupt,
                    UsbAddressing.XferIsochronous => RtlEndpointType.Isochronous,
                    _ => RtlEndpointType.Control
                };

                var rtlEp = new RtlEndpoint()
                {
                    Direction = ep.Direction == UsbAddressing.In ? RtlEndpointDirection.In : RtlEndpointDirection.Out,
                    Type = type
                };
                ret.Add(rtlEp);
            }

            return ret;
        }
    }

    public class RtlEndpoint : IRtlEndpoint
    {
        public RtlEndpointType Type { get; set; }
        public RtlEndpointDirection Direction { get; set; }
    }
}
