using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Win32;

namespace MiniRec
{
    // Names intentionally match the previous Python settings.json format.
    [DataContract]
    internal sealed class AppSettings
    {
        [DataMember(Name = "device_id")] public string DeviceId { get; set; }
        [DataMember(Name = "output_folder")] public string OutputFolder { get; set; }
        [DataMember(Name = "format")] public string Format { get; set; }
        [DataMember(Name = "tray_click_mode")] public string TrayClickMode { get; set; }
        [DataMember(Name = "language")] public string Language { get; set; }
        [DataMember(Name = "normalize")] public bool Normalize { get; set; }
        [DataMember(Name = "clipboard")] public bool Clipboard { get; set; }
        [DataMember(Name = "delete_after")] public bool DeleteAfter { get; set; }
        [DataMember(Name = "hk_mic")] public string HotkeyMic { get; set; }
        [DataMember(Name = "hk_loop")] public string HotkeySystem { get; set; }
        [DataMember(Name = "hk_both")] public string HotkeyBoth { get; set; }
        [DataMember(Name = "hk_stop")] public string HotkeyStop { get; set; }

        public AppSettings()
        {
            OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MiniRec");
            Format = "MP3";
            TrayClickMode = "last_used";
            Language = "system";
            HotkeyMic = HotkeySystem = HotkeyBoth = HotkeyStop = "";
        }

        public AppSettings Clone() => (AppSettings)MemberwiseClone();
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(OutputFolder)) throw new ArgumentException("请选择录音保存目录。");
            if (!string.Equals(Format, "WAV", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Format, "MP3", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("录音格式必须是 WAV 或 MP3。");
        }
    }

    internal static class SettingsStore
    {
        private static readonly string NewDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MiniRec");
        private static readonly string OldDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickAudioRecorder");
        public static string FilePath => Path.Combine(NewDirectory, "settings.json");
        private static string LegacyFilePath => Path.Combine(OldDirectory, "settings.json");

        private static DataContractJsonSerializer Serializer =>
            new DataContractJsonSerializer(typeof(AppSettings));

        public static AppSettings Load()
        {
            try { return LoadFromPaths(FilePath, LegacyFilePath); }
            catch (Exception ex)
            {
                Diagnostics.Write("Settings load: " + ex);
                return new AppSettings();
            }
        }

        // First launch imports but never changes or removes the former application config.
        // Once MiniRec/settings.json exists, that file always takes precedence.
        private static AppSettings LoadFromPaths(string current, string legacy)
        {
            if (File.Exists(current)) return Read(current);
            if (File.Exists(legacy))
            {
                var imported = Read(legacy);
                try
                {
                    Write(current, imported);
                    Diagnostics.Write("Imported former QuickAudioRecorder settings into MiniRec.");
                }
                catch (Exception ex)
                {
                    // Continue with the imported settings even if persistence is blocked.
                    Diagnostics.Write("Settings migration save failed: " + ex);
                }
                return imported;
            }
            return new AppSettings();
        }

        private static AppSettings Read(string path)
        {
            AppSettings settings;
            using (var file = File.OpenRead(path))
                settings = (AppSettings)Serializer.ReadObject(file);
            if (settings == null) throw new InvalidDataException("Settings content is empty.");
            var defaults = new AppSettings();
            if (string.IsNullOrWhiteSpace(settings.OutputFolder)) settings.OutputFolder = defaults.OutputFolder;
            if (string.IsNullOrWhiteSpace(settings.Format)) settings.Format = defaults.Format;
            if (string.IsNullOrWhiteSpace(settings.TrayClickMode)) settings.TrayClickMode = defaults.TrayClickMode;
            if (string.IsNullOrWhiteSpace(settings.Language)) settings.Language = defaults.Language;
            settings.Validate();
            return settings;
        }

        public static void Save(AppSettings settings) => Write(FilePath, settings);

        private static void Write(string path, AppSettings settings)
        {
            settings.Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temp)) Serializer.WriteObject(stream, settings);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { TryDelete(temp); }
        }

        internal static bool VerifyLegacyMigration()
        {
            string root = Path.Combine(Path.GetTempPath(), "MiniRecMigrationTest-" + Guid.NewGuid().ToString("N"));
            string legacy = Path.Combine(root, "QuickAudioRecorder", "settings.json");
            string current = Path.Combine(root, "MiniRec", "settings.json");
            try
            {
                var old = new AppSettings
                {
                    OutputFolder = @"C:\Recordings\原来的录音",
                    Format = "WAV",
                    HotkeyMic = "+",
                    HotkeyStop = "-",
                    TrayClickMode = "both",
                    Language = "zh_CN",
                    Clipboard = true
                };
                Write(legacy, old);
                var imported = LoadFromPaths(current, legacy);
                if (!File.Exists(current) || !File.Exists(legacy) ||
                    imported.HotkeyMic != "+" || imported.HotkeyStop != "-" ||
                    imported.OutputFolder != old.OutputFolder || imported.TrayClickMode != "both" ||
                    imported.Format != "WAV" || !imported.Clipboard) return false;

                // Modifying the new file must not touch the former app's settings.
                imported.Format = "MP3";
                Write(current, imported);
                var latest = LoadFromPaths(current, legacy);
                var preserved = Read(legacy);
                return latest.Format == "MP3" && preserved.Format == "WAV" &&
                       preserved.HotkeyMic == "+" && preserved.HotkeyStop == "-";
            }
            catch (Exception ex)
            {
                Diagnostics.Write("Settings migration regression: " + ex);
                return false;
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        public static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    internal static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Entry = "MiniRec";
        public static bool Enabled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return string.Equals(key?.GetValue(Entry) as string, Command, StringComparison.OrdinalIgnoreCase);
            }
        }
        private static string Command => "\"" + System.Windows.Forms.Application.ExecutablePath + "\"";
        public static void Set(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(Entry, Command);
                else key.DeleteValue(Entry, false);
            }
        }
    }

    internal static class Diagnostics
    {
        private static readonly object Sync = new object();
        public static void Write(string message)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MiniRec", "Logs");
                Directory.CreateDirectory(dir);
                lock (Sync)
                {
                    string path = Path.Combine(dir, "app.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024)
                    {
                        string previous = Path.Combine(dir, "app.previous.log");
                        SettingsStore.TryDelete(previous);
                        File.Move(path, previous);
                    }
                    File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") +
                        message + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}
