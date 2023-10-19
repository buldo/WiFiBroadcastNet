using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpGen.Runtime;
using Vortice.MediaFoundation;

using static Vortice.MediaFoundation.MediaFactory;

namespace OsdDemo.Windows;
public class WmfModule
{
    IMFMediaEngineClassFactory _mediaEngineFactory = new();
    IMFAttributes _attributes = MFCreateAttributes(1);

    public WmfModule()
    {
        StartupResult = MFStartup(true);

        if (StartupResult.Failure)
        {
            return;
        }

        //_attributes.videVideoDeviceCategory = Vortice.MediaFoundation.
        var mediaPlayer = new MediaPlayer();
    }

    public Result StartupResult { get; }
}
