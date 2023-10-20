using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using LibVLCSharp.Shared;

namespace OsdDemo.ViewModels;
internal class MainWindowViewModel : ObservableObject
{
    private readonly string _sdp = """
                                   sdp://v=0
                                   c=IN IP4 0.0.0.0
                                   m=video 5600 RTP/AVP 96
                                   a=rtpmap:96 H264/90000
                                   """;

    private readonly Dictionary<string, MavlinkStatViewModel> _statsByName = new();
    private readonly LibVLC _libVlc = new LibVLC();
    private readonly Media _media;

    private int _packetsCount;
    private string _statusText;


    public MainWindowViewModel()
    {
        ConnectCommand = new AsyncRelayCommand(ExecuteConnect);
        MediaPlayer = new MediaPlayer(_libVlc)
        {
            NetworkCaching = 25
        };

        _media = new Media(_libVlc, _sdp, FromType.FromLocation);
    }

    public AsyncRelayCommand ConnectCommand { get; }

    public int PacketsCount { get => _packetsCount; set => SetProperty(ref _packetsCount, value); }

    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public ObservableCollection<MavlinkStatViewModel> MavlinkStat { get; } = new();

    public MediaPlayer MediaPlayer { get; }

    private async Task ExecuteConnect()
    {
        var conn = MavlinkV2Connection.Create($"tcp://192.168.88.160:5760");
        conn.Subscribe(OnPacket);
        conn.DeserializePackageErrors.Subscribe(OnError);

        MediaPlayer.Play(_media);
    }

    private void OnPacket(IPacketV2<IPayload> packet)
    {
        PacketsCount++;
        if (_statsByName.TryGetValue(packet.Name, out var stat))
        {
            stat.Count++;
        }
        else
        {
            stat = new MavlinkStatViewModel
            {
                Name = packet.Name,
                Count = 1
            };

            _statsByName[packet.Name] = stat;
            MavlinkStat.Add(stat);
        }

        if (packet.Payload is StatustextPayload statusTextPayload)
        {
            StatusText = new string(statusTextPayload.Text);
        }

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

public class MavlinkStatViewModel : ObservableObject
{
    private int _count;

    public required string Name { get; init; }

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }
}