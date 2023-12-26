using CommunityToolkit.Mvvm.ComponentModel;

using LibVLCSharp.Shared;

namespace Osd.Wpf.ViewModels;

public class OsdVideoViewModel : ObservableObject
{
    private bool _isPlaying = false;

    private readonly string _sdp = """
                                   sdp://v=0
                                   c=IN IP4 0.0.0.0
                                   m=video 5600 RTP/AVP 96
                                   a=rtpmap:96 H264/90000
                                   """;

    // private readonly Dictionary<string, MavlinkStatViewModel> _statsByName = new();
    private readonly LibVLC _libVlc = new LibVLC();
    private readonly Media _media;

    public OsdVideoViewModel()
    {
        MediaPlayer = new MediaPlayer(_libVlc)
        {
            NetworkCaching = 25
        };

        _media = new Media(_libVlc, _sdp, FromType.FromLocation);
    }

    public MediaPlayer MediaPlayer { get; }

    public void StartPlay()
    {
        if (_isPlaying)
        {
            MediaPlayer.Stop();
        }
        _isPlaying = true;
        MediaPlayer.Play(_media);
    }
}