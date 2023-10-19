using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gst;

namespace OsdDemo.Windows;
public static class GstModule
{
    public static void Init()
    {
        PreparePath();

        GstVideo.Module.Initialize();
        Application.Init();
    }

    private static void PreparePath()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        var gstPath = GetGstPath(EnvironmentVariableTarget.User) ?? GetGstPath(EnvironmentVariableTarget.Machine);

        if (gstPath != null)
        {
            path = path +
                   Path.PathSeparator +
                   Path.Combine(gstPath, "bin") +
                   Path.PathSeparator +
                   Path.Combine(gstPath, "lib", "gstreamer-1.0");
        }

        Environment.SetEnvironmentVariable("PATH", path);
    }

    private static string? GetGstPath(EnvironmentVariableTarget target)
    {
        var variables= Environment.GetEnvironmentVariables(target);
        var name = variables.Keys.Cast<string>().FirstOrDefault(s => s.StartsWith("GSTREAMER"));
        if (name == null)
        {
            return null;
        }

        return variables[name].ToString();
    }
}
