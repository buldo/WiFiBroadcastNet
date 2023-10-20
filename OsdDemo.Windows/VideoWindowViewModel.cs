using System.IO;

using FlyleafLib;
using FlyleafLib.MediaFramework.MediaStream;
using FlyleafLib.MediaPlayer;

namespace OsdDemo.Windows;

public class VideoWindowViewModel
{
    public Player Player { get; } = new Player();

    public void Play()
    {
        Player.Config.Audio.Enabled = false;
        Player.Config.Subtitles.Enabled = false;
        Player.Config.Demuxer.FormatFlags |= 0x40;
        Player.Config.Demuxer.FormatOpt["flags"] = "low_delay";
        Player.Config.Demuxer.FormatOpt.Add("protocol_whitelist", "file,crypto,data,rtp,sdp,udp");
        Player.Config.Player.Stats = true;
        Player.Config.Player.MinBufferDuration = 0;




        var fileName = Path.GetFullPath("main.sdp");
        var a =Player.Open($"file://{fileName}");
        Player.Play();
    }
}