using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CwTrainer.Settings
{
    /// <summary>
    /// Simple INI file reader/writer using the classic Windows API
    /// (GetPrivateProfileString / WritePrivateProfileString) - no external
    /// dependencies, human-readable/editable file format, works the same
    /// way it has since Win16.
    ///
    /// Usage:
    ///   var ini = new IniFile("settings.ini"); // relative to exe folder, or pass a full path
    ///   ini.WriteInt("Window", "Width", this.Width);
    ///   int w = ini.ReadInt("Window", "Width", 1200); // 1200 = default if missing/unparsable
    /// </summary>
    public sealed class IniFile
    {
        private readonly string _path;

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetPrivateProfileString(
            string section, string key, string defaultValue,
            StringBuilder result, int size, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern long WritePrivateProfileString(
            string section, string key, string value, string filePath);



        /// <summary>
        /// Path can be relative (resolved against the executable's
        /// directory, same as the app's working folder normally is) or
        /// absolute. Relative is the common case - e.g. new IniFile("settings.ini")
        /// keeps the file alongside the .exe.
        /// </summary>
        public IniFile(string path)
        {
            _path = Path.IsPathRooted(path)
                ? path
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }

        public string FilePath => _path;

        // --- Raw string read/write - everything else builds on these ---

        public string ReadString(string section, string key, string defaultValue = "")
        {
            var buffer = new StringBuilder(1024);
            int charsReturned = GetPrivateProfileString(section, key, defaultValue, buffer, buffer.Capacity, _path);
            return charsReturned > 0 ? buffer.ToString() : defaultValue;
        }

        public void WriteString(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, _path);
        }

        // --- Typed convenience wrappers ---

        public int ReadInt(string section, string key, int defaultValue = 0)
        {
            string raw = ReadString(section, key, defaultValue.ToString());
            return int.TryParse(raw, out int result) ? result : defaultValue;
        }

        public void WriteInt(string section, string key, int value)
        {
            WriteString(section, key, value.ToString());
        }

        public double ReadDouble(string section, string key, double defaultValue = 0)
        {
            string raw = ReadString(section, key, defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double result) ? result : defaultValue;
        }

        public void WriteDouble(string section, string key, double value)
        {
            WriteString(section, key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public bool ReadBool(string section, string key, bool defaultValue = false)
        {
            string raw = ReadString(section, key, defaultValue ? "1" : "0");
            if (raw == "1") return true;
            if (raw == "0") return false;
            return bool.TryParse(raw, out bool result) ? result : defaultValue;
        }

        public void WriteBool(string section, string key, bool value)
        {
            WriteString(section, key, value ? "1" : "0");
        }

        /// <summary>True if the INI file currently exists on disk (e.g. to decide whether to apply defaults vs. restore saved values on first run).</summary>
        public bool Exists => File.Exists(_path);
    }
}
