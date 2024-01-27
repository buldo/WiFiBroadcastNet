//----------------------------------------------------------------------------
// File Name: Log.cs
//
// Description:
// Log provides a one stop shop for log settings rather then have configuration
// functions in separate classes.
//
// Author(s):
// Aaron Clauson
//
// History:
// 04 Nov 2004	Aaron Clauson   Created.
// 14 Sep 2019  Aaron Clauson   Added NetStandard support.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//----------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using SIPSorcery;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys;

internal static class Log
{
    private const string LOG_CATEGORY = "sipsorcery";

    static Log()
    {
        LogFactory.Instance.OnFactorySet += Reset;
    }

    private static ILogger _logger;
    internal static ILogger Logger
    {
        get
        {
            if (_logger == null)
            {
                _logger = LogFactory.CreateLogger(LOG_CATEGORY);
            }

            return _logger;
        }
    }

    /// <summary>
    /// Intended to be called if the application wide logging configuration changes. Will force
    /// the singleton logger to be re-created.
    /// </summary>
    private static void Reset()
    {
        _logger = null;
    }
}