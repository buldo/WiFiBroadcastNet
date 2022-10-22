using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WiFiBroadcastNet.SystemHelpers;

internal class LinuxHelpers : IOsCommandHelper
{
    public void SetUnmanagedMode(string deviceName)
    {
        throw new NotImplementedException();
    }

    public void SetMonitorMode(string deviceName)
    {
        throw new NotImplementedException();
    }
}

internal class NotImplementedHelpers : IOsCommandHelper
{
    public void SetUnmanagedMode(string deviceName)
    {
    }

    public void SetMonitorMode(string deviceName)
    {
    }
}

internal interface IOsCommandHelper
{
    void SetUnmanagedMode(string deviceName);

    void SetMonitorMode(string deviceName);
}