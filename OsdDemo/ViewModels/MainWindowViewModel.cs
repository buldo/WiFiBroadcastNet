using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Asv.Mavlink;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OsdDemo.ViewModels;
internal class MainWindowViewModel : ObservableObject
{
    private int _packetsCount;

    public MainWindowViewModel()
    {
        ConnectCommand = new AsyncRelayCommand(ExecuteConnect);
    }

    public AsyncRelayCommand ConnectCommand { get; }

    public int PacketsCount
    {
        get => _packetsCount;
        set => SetProperty(ref _packetsCount, value);
    }

    private async Task ExecuteConnect()
    {
        var conn = MavlinkV2Connection.Create($"tcp://192.168.88.160:5760");
        conn.Subscribe(OnPacket);
        conn.DeserializePackageErrors.Subscribe(OnError);
    }

    private void OnPacket(IPacketV2<IPayload> packet)
    {
        PacketsCount++;
        //Interlocked.Increment(ref _packetCount);
        //try
        //{
        //    _rw.EnterWriteLock();
        //    _lastPackets.Add(packet);
        //    if (_lastPackets.Count >= MaxHistorySize) _lastPackets.RemoveAt(0);
        //    var exist = _items.FirstOrDefault(_ => packet.MessageId == _.Msg);
        //    if (exist == null)
        //    {
        //        _items.Add(new DisplayRow { Msg = packet.MessageId, Message = packet.Name });
        //    }
        //    else
        //    {
        //        exist.Count++;
        //    }
        //}
        //finally
        //{
        //    _rw.ExitWriteLock();
        //}
    }

    private void OnError(DeserializePackageException ex)
    {
        //try
        //{
        //    _rw.EnterWriteLock();
        //    var exist = _items.FirstOrDefault(_ => ex.MessageId == _.Msg);
        //    if (exist == null)
        //    {
        //        _items.Add(new DisplayRow { Msg = ex.MessageId, Message = ex.Message });
        //    }
        //    else
        //    {
        //        exist.Count++;
        //    }
        //}
        //finally
        //{
        //    _rw.ExitWriteLock();
        //}
    }


}
