using System.Diagnostics;
using System.Text;

namespace WiFiBroadcastNet.SystemHelpers;

internal class LinuxHelpers : IOsCommandHelper
{
    public void SetUnmanagedMode(string deviceName)
    {
        Execute("nmcli", $"device set {deviceName} managed no");
    }

    public void SetMonitorMode(string deviceName)
    {
        Execute("iw", $"dev {deviceName} set monitor otherbss");
    }

    public void SetFrequency(string deviceName, Frequency frequency)
    {
        Execute("iw", $"dev {deviceName} set freq {frequency.ValueInMHz}");
    }

    private void Execute(string name, string args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = name,
            Arguments = args,
            // WorkingDirectory = 
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8
        };
        
        
        var process = new Process(){StartInfo = startInfo};
        process.OutputDataReceived += (sender, eventArgs) => Console.WriteLine(eventArgs.Data);
        process.ErrorDataReceived += (sender, eventArgs) => Console.WriteLine(eventArgs.Data);
        process.Start();
        process?.WaitForExit(TimeSpan.FromSeconds(10));
    }
}