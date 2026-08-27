using System;
using System.IO;
using System.Linq;
using TaleWorlds.Library;

namespace SOTOR
{
        public static class SotorLog
    {
        public enum Level
        {
            Debug,
            Info,
            Warn,
            Error
        }

        private const int MaxSessionFiles = 30;

        public static Level MinLevel = Level.Info;

        private static readonly object WriteLock = new object();
        private static string _logDirectory;
        private static string _logFilePath;
        private static StreamWriter _writer;
        private static bool _initialized;
        private static DateTime _lastFlushUtc = DateTime.UtcNow;
        private const double FlushIntervalSeconds = 2.0;

        public static string LogDirectory
        {
            get
            {
                EnsureInitialized();
                return _logDirectory;
            }
        }

        public static string LogFilePath
        {
            get
            {
                EnsureInitialized();
                return _logFilePath;
            }
        }

        public static void Debug(string message) => Write(Level.Debug, message);

        public static void Info(string message) => Write(Level.Info, message);

        public static void Warn(string message) => Write(Level.Warn, message);

        public static void Error(string message) => Write(Level.Error, message);

        public static void Write(Level level, string message)
        {
            if (level < MinLevel)
            {
                return;
            }

            EnsureInitialized();
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

            bool important = level >= Level.Warn;
            if (important)
            {
                TaleWorlds.Library.Debug.Print("[SOTOR] " + line);
            }

            try
            {
                lock (WriteLock)
                {

                    if (_writer != null)
                    {
                        _writer.WriteLine(line);
                        if (important || (DateTime.UtcNow - _lastFlushUtc).TotalSeconds >= FlushIntervalSeconds)
                        {
                            _writer.Flush();
                            _lastFlushUtc = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        File.AppendAllText(_logFilePath, line + Environment.NewLine);
                    }
                }
            }
            catch
            {

            }
        }

        public static void Flush()
        {
            try
            {
                lock (WriteLock)
                {
                    _writer?.Flush();
                    _lastFlushUtc = DateTime.UtcNow;
                }
            }
            catch
            {
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            var documentsLogDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord",
                "Logs",
                "SOTOR");

            try
            {
                Directory.CreateDirectory(documentsLogDir);
                _logDirectory = documentsLogDir;

                var sessionStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _logFilePath = Path.Combine(documentsLogDir, $"session_{sessionStamp}.log");
                File.WriteAllText(
                    _logFilePath,
                    $"=== SOTOR session {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");

                File.WriteAllText(
                    Path.Combine(documentsLogDir, "latest.txt"),
                    _logFilePath + Environment.NewLine);

                _writer = new StreamWriter(_logFilePath, append: true) { AutoFlush = false };
                AppDomain.CurrentDomain.ProcessExit += (s, e) => Flush();

                PruneOldSessionFiles(documentsLogDir);
            }
            catch
            {
                _logDirectory = Path.GetTempPath();
                _logFilePath = Path.Combine(_logDirectory, $"SOTOR_session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
            }

            _initialized = true;
        }

        private static void PruneOldSessionFiles(string logDir)
        {
            try
            {
                var staleFiles = Directory.GetFiles(logDir, "session_*.log")
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Skip(MaxSessionFiles)
                    .ToList();

                foreach (var file in staleFiles)
                {
                    file.Delete();
                }
            }
            catch
            {

            }
        }
    }
}
