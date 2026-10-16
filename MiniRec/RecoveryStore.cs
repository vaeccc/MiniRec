using System;
using System.IO;
using System.Text;

namespace MiniRec
{
    // Raw capture WAVs live beside final recordings so that Windows TEMP cleanup
    // or an unexpected termination cannot silently discard a user's recording.
    internal static class RecoveryStore
    {
        public static string EnsureFolder(string outputFolder)
        {
            string recovery = Path.Combine(outputFolder, "Recovery");
            Directory.CreateDirectory(recovery);
            return recovery;
        }

        public static string CreateCapturePath(string recoveryFolder)
        {
            return Path.Combine(recoveryFolder,
                "capture_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" +
                Guid.NewGuid().ToString("N") + ".wav");
        }

        public static void RepairOrphans(string outputFolder)
        {
            try
            {
                string folder = Path.Combine(outputFolder, "Recovery");
                if (!Directory.Exists(folder)) return;
                foreach (string path in Directory.EnumerateFiles(folder, "capture_*.wav"))
                {
                    try { RepairHeader(path); }
                    catch (Exception ex) { Diagnostics.Write("Recover incomplete WAV " + path + ": " + ex); }
                }
            }
            catch (Exception ex) { Diagnostics.Write("Recovery scan failed: " + ex); }
        }

        // WaveFileWriter completes RIFF/data chunk lengths when disposed. A crash
        // can leave both size fields zero, despite recorded PCM data being present.
        // Only touch files with a recognizable RIFF/WAVE header and a final data chunk.
        public static bool RepairHeader(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                if (file.Length < 44 || file.Length - 8 > uint.MaxValue) return false;
                using (var reader = new BinaryReader(file, Encoding.ASCII, true))
                using (var writer = new BinaryWriter(file, Encoding.ASCII, true))
                {
                    if (reader.ReadUInt32() != 0x46464952) return false; // RIFF
                    uint riffSize = reader.ReadUInt32();
                    if (reader.ReadUInt32() != 0x45564157) return false; // WAVE
                    long position = 12;
                    bool foundFormat = false;
                    while (position + 8 <= file.Length)
                    {
                        file.Position = position;
                        uint kind = reader.ReadUInt32();
                        uint declaredSize = reader.ReadUInt32();
                        long start = file.Position;
                        if (kind == 0x20746D66) // 'fmt '
                        {
                            if (declaredSize < 16 || start + declaredSize > file.Length) return false;
                            foundFormat = true;
                        }
                        if (kind == 0x61746164) // 'data'
                        {
                            if (!foundFormat) return false;
                            long actualLength = file.Length - start;
                            if (actualLength > uint.MaxValue) return false;
                            if (declaredSize == actualLength && riffSize == file.Length - 8)
                                return true;
                            // Data is the final chunk for files written by NAudio.
                            // Reject extra data beyond a correctly finalized chunk;
                            // don't modify unrelated WAV files.
                            if (declaredSize > 0 && declaredSize < actualLength &&
                                riffSize == file.Length - 8) return true;
                            file.Position = 4;
                            writer.Write((uint)(file.Length - 8));
                            file.Position = position + 4;
                            writer.Write((uint)actualLength);
                            writer.Flush();
                            return true;
                        }
                        long next = start + declaredSize + (declaredSize & 1);
                        if (next <= position || next > file.Length) return false;
                        position = next;
                    }
                }
            }
            return false;
        }
    }
}
