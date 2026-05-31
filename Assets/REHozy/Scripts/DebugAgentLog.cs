using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace REHozy
{
    internal static class DebugAgentLog
    {
        private static readonly string LogPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "debug-e6a7a2.log"));

        public static void Log(string hypothesisId, string location, string message, string dataJson)
        {
            // #region agent log
            try
            {
                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
                var line =
                    "{\"sessionId\":\"e6a7a2\",\"hypothesisId\":\"" + hypothesisId +
                    "\",\"location\":\"" + location +
                    "\",\"message\":\"" + message +
                    "\",\"data\":" + dataJson +
                    ",\"timestamp\":" + ts + "}\n";
                File.AppendAllText(LogPath, line);
            }
            catch
            {
                // ignored
            }
            // #endregion
        }
    }
}
