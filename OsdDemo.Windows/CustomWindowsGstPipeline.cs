using GstVideo;
using ElementFactory = Gst.ElementFactory;
using Pipeline = Gst.Pipeline;

namespace OsdDemo.Windows;
internal class CustomWindowsGstPipeline
{
    public CustomWindowsGstPipeline()
    {

        //Gst.Functions.ParseLaunch(
        //    "udpsrc port=5600 caps=application/x-rtp, media=(string)video, clock-rate=(int)90000, encoding-name=(string)H265 ! rtpjitterbuffer latency=7 ! rtph265depay ! h265parse ! video/x-h265, alignment=nal ! avdec_h265 ! video/x-raw ! queue max-size-bytes=0 ! autovideosink sync=false -e");
        ////var pipeline = new Pipeline();
        //var udpSrc = ElementFactory.Make("udpsrc", "udpsrc");
        //udpSrc.

    }
}
