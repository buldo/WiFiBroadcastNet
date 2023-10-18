using CommunityToolkit.Mvvm.ComponentModel;

using LibVLCSharp.Shared;

namespace OsdDemo.Windows;
public class VideoWindowViewModel : ObservableObject
{
    private readonly string _sdp = """
                                   sdp://v=0
                                   c=IN IP4 0.0.0.0
                                   m=video 5600 RTP/AVP 96
                                   a=rtpmap:96 H264/90000
                                   """;
    private readonly LibVLC _libVlc = new();
    private readonly Media _media;

    public VideoWindowViewModel()
    {

        MediaPlayer = new MediaPlayer(_libVlc);
        _media = new Media(_libVlc, _sdp, FromType.FromLocation);
    }

    public MediaPlayer MediaPlayer { get; }

    public void Play()
    {
        MediaPlayer.Play(_media);
    }
}
