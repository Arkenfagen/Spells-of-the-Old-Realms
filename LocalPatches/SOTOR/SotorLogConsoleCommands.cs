using System.Collections.Generic;
using TaleWorlds.Library;

namespace SOTOR
{

    public static class SotorLogConsoleCommands
    {
        [CommandLineFunctionality.CommandLineArgumentFunction("verbose_log", "sotor")]
        public static string VerboseLog(List<string> args)
        {
            string arg = (args != null && args.Count > 0) ? args[0].Trim().ToLowerInvariant() : "";

            switch (arg)
            {
                case "on":
                case "1":
                case "true":
                    SotorLog.MinLevel = SotorLog.Level.Debug;
                    SotorLog.Info("Verbose logging ON (Debug).");
                    return "SOTOR log level: Debug (verbose). Detailed diagnostics are now recorded "
                           + "to Logs/SOTOR/latest.txt. Turn it off again when you are done.";

                case "off":
                case "0":
                case "false":
                    SotorLog.Info("Verbose logging OFF (back to Info).");
                    SotorLog.MinLevel = SotorLog.Level.Info;
                    return "SOTOR log level: Info (normal).";

                default:
                    return $"usage: sotor.verbose_log <on|off>   (currently {SotorLog.MinLevel})";
            }
        }
    }
}
