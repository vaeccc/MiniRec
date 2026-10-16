using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MiniRec
{
    internal enum AudioSource { Microphone, System, Both }

    internal sealed class RecordingResult
    {
        public string FilePath { get; set; }
        public string Error { get; set; }
        public string RecoveryFolder { get; set; }
        public bool Success => string.IsNullOrEmpty(Error) && !string.IsNullOrEmpty(FilePath);
    }

    // Capture to disk while recording. Offline conversion/mixing is streaming and bounded-memory.
    internal sealed class RecordingService
    {
        private readonly object _gate = new object();
        private List<CaptureTrack> _tracks = new List<CaptureTrack>();
        private bool _active;
        private AppSettings _options;
        public bool IsRecording { get { lock (_gate) return _active; } }

        public Task StartAsync(AudioSource source, AppSettings settings)
        {
            return Task.Run(() =>
            {
                lock (_gate)
                {
                    if (_active) throw new InvalidOperationException("A recording is already running.");
                    _active = true; // Reserve the session before opening audio devices.
                }
                var tracks = new List<CaptureTrack>();
                try
                {
                    settings.Validate();
                    Directory.CreateDirectory(settings.OutputFolder);
                    string recoveryFolder = RecoveryStore.EnsureFolder(settings.OutputFolder);
                    if (source == AudioSource.Microphone || source == AudioSource.Both)
                        tracks.Add(CaptureTrack.OpenMicrophone(settings.DeviceId, recoveryFolder));
                    if (source == AudioSource.System || source == AudioSource.Both)
                        tracks.Add(CaptureTrack.OpenLoopback(recoveryFolder));
                    foreach (var track in tracks) track.Start();
                    lock (_gate) { _tracks = tracks; _options = settings.Clone(); }
                }
                catch
                {
                    foreach (var track in tracks) { try { track.Stop(); } catch { } }
                    foreach (var track in tracks)
                    {
                        try { track.WaitStopped(); } catch { }
                        track.Dispose();
                    }
                    PreserveRecovery(tracks, settings.OutputFolder);
                    lock (_gate) { _tracks.Clear(); _active = false; }
                    throw;
                }
            });
        }

        public Task<RecordingResult> StopAsync()
        {
            return Task.Run(() =>
            {
                List<CaptureTrack> tracks;
                AppSettings settings;
                lock (_gate)
                {
                    if (!_active || _tracks.Count == 0)
                        return new RecordingResult { Error = "没有正在录制的音频。" };
                    tracks = new List<CaptureTrack>(_tracks);
                    _tracks.Clear();
                    _active = false;
                    settings = _options;
                }

                var intermediates = new List<string>();
                bool outputSaved = false;
                try
                {
                    foreach (var track in tracks) track.Stop();
                    foreach (var track in tracks) track.WaitStopped();

                    foreach (var track in tracks)
                    {
                        if (track.Error != null) throw new IOException("录音设备异常。", track.Error);
                        track.Dispose();
                    }

                    var converted = new List<string>();
                    foreach (var track in tracks)
                    {
                        string standard = TempPath();
                        intermediates.Add(standard);
                        ConvertToPcm(track.Path, standard);
                        converted.Add(standard);
                    }
                    string pcm = converted[0];
                    if (converted.Count == 2)
                    {
                        pcm = TempPath();
                        intermediates.Add(pcm);
                        Mix(converted[0], converted[1], pcm, tracks[0].StartTicks, tracks[1].StartTicks);
                    }
                    if (settings.Normalize)
                    {
                        string normalized = TempPath();
                        intermediates.Add(normalized);
                        NormalizePcm(pcm, normalized);
                        pcm = normalized;
                    }

                    string finalPath = SaveProcessedAudio(pcm, settings.OutputFolder, settings.Format);
                    outputSaved = true;
                    return new RecordingResult { FilePath = finalPath };
                }
                catch (Exception ex)
                {
                    Diagnostics.Write("Recording stop: " + ex);
                    foreach (var track in tracks) { try { track.Stop(); } catch { } }
                    foreach (var track in tracks) { try { track.WaitStopped(); } catch { } track.Dispose(); }
                    string recovery = PreserveRecovery(tracks, settings.OutputFolder);
                    return new RecordingResult { Error = ex.Message, RecoveryFolder = recovery };
                }
                finally
                {
                    foreach (var track in tracks)
                    {
                        track.Dispose();
                        // Captured WAVs survive any failed encode, unexpected shutdown,
                        // or recovery copy failure. Delete them only after final save.
                        if (outputSaved) SettingsStore.TryDelete(track.Path);
                    }
                    foreach (string file in intermediates) SettingsStore.TryDelete(file);
                }
            });
        }

        private static string PreserveRecovery(IEnumerable<CaptureTrack> tracks, string folder)
        {
            try
            {
                string recovery = RecoveryStore.EnsureFolder(folder);
                bool found = false;
                foreach (var track in tracks)
                {
                    if (!File.Exists(track.Path)) continue;
                    found = true;
                    try { RecoveryStore.RepairHeader(track.Path); }
                    catch (Exception ex) { Diagnostics.Write("Recovery repair failed: " + ex); }
                }
                return found ? recovery : null;
            }
            catch (Exception ex) { Diagnostics.Write("Recovery failed: " + ex); return null; }
        }

        private static string SaveProcessedAudio(string pcm, string outputFolder, string format)
        {
            Directory.CreateDirectory(outputFolder);
            string ext = format.ToLowerInvariant();
            string finalPath = UniqueOutputPath(outputFolder, ext);
            // Media Foundation chooses the MP3 container from the extension:
            // the temporary encoded file MUST also end in .mp3 (not .partial).
            string pending = Path.Combine(outputFolder,
                ".minirec_" + Guid.NewGuid().ToString("N") + "." + ext);
            try
            {
                if (ext == "mp3")
                {
                    using (var reader = new WaveFileReader(pcm))
                        MediaFoundationEncoder.EncodeToMp3(reader, pending, 192000);
                }
                else if (ext == "wav") File.Copy(pcm, pending);
                else throw new InvalidOperationException("Unsupported recording format: " + format);

                if (new FileInfo(pending).Length == 0)
                    throw new IOException("Encoded audio file is empty.");
                File.Move(pending, finalPath);
                return finalPath;
            }
            finally { SettingsStore.TryDelete(pending); }
        }

        private static string TempPath() =>
            Path.Combine(Path.GetTempPath(), "qar_" + Guid.NewGuid().ToString("N") + ".wav");

        private static string UniqueOutputPath(string folder, string ext)
        {
            string name = "Recording_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            for (int i = 0; ; i++)
            {
                string path = Path.Combine(folder, name + (i == 0 ? "" : "_" + i) + "." + ext);
                if (!File.Exists(path) && !File.Exists(path + ".partial")) return path;
            }
        }

        private static readonly WaveFormat StandardFormat = new WaveFormat(44100, 16, 2);

        private static void ConvertToPcm(string input, string output)
        {
            using (var source = new WaveFileReader(input))
            using (var resampler = new MediaFoundationResampler(source, StandardFormat))
            using (var writer = new WaveFileWriter(output, StandardFormat))
            {
                resampler.ResamplerQuality = 60;
                var buffer = new byte[32768];
                int read;
                while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
                    writer.Write(buffer, 0, read);
            }
        }

        private static void Mix(string leftPath, string rightPath, string output, long leftStart, long rightStart)
        {
            long first = Math.Min(leftStart, rightStart);
            var leftDelay = (long)Math.Round((leftStart - first) * 44100d / Stopwatch.Frequency);
            var rightDelay = (long)Math.Round((rightStart - first) * 44100d / Stopwatch.Frequency);
            using (var a = new WaveFileReader(leftPath))
            using (var b = new WaveFileReader(rightPath))
            using (var writer = new WaveFileWriter(output, StandardFormat))
            {
                const int frames = 4096;
                var ba = new byte[frames * 4];
                var bb = new byte[frames * 4];
                var combined = new byte[frames * 4];
                while (true)
                {
                    Array.Clear(ba, 0, ba.Length);
                    Array.Clear(bb, 0, bb.Length);
                    int ca = ReadAligned(a, ba, frames, ref leftDelay);
                    int cb = ReadAligned(b, bb, frames, ref rightDelay);
                    int used = Math.Max(ca, cb);
                    if (used == 0) break;
                    for (int sample = 0; sample < used * 2; sample++)
                    {
                        int offset = sample * 2;
                        int va = (short)(ba[offset] | (ba[offset + 1] << 8));
                        int vb = (short)(bb[offset] | (bb[offset + 1] << 8));
                        int mix = (va + vb) / 2; // Avoid clipping; optional normalization follows.
                        combined[offset] = (byte)(mix & 255);
                        combined[offset + 1] = (byte)((mix >> 8) & 255);
                    }
                    writer.Write(combined, 0, used * 4);
                }
            }
        }

        private static int ReadAligned(WaveFileReader stream, byte[] output, int frames, ref long delay)
        {
            int pad = (int)Math.Min(delay, frames);
            delay -= pad;
            int read = 0;
            if (frames > pad) read = stream.Read(output, pad * 4, (frames - pad) * 4);
            return pad + read / 4;
        }

        private static void NormalizePcm(string input, string output)
        {
            double peak = 0;
            var block = new byte[32768];
            using (var reader = new WaveFileReader(input))
            {
                int read;
                while ((read = reader.Read(block, 0, block.Length)) > 0)
                    for (int i = 0; i + 1 < read; i += 2)
                        peak = Math.Max(peak, Math.Abs((short)(block[i] | (block[i + 1] << 8))));
            }
            double gain = peak > 0 ? Math.Min(32700d / peak, 100d) : 1;
            using (var reader = new WaveFileReader(input))
            using (var writer = new WaveFileWriter(output, StandardFormat))
            {
                int read;
                while ((read = reader.Read(block, 0, block.Length)) > 0)
                {
                    for (int i = 0; i + 1 < read; i += 2)
                    {
                        int sample = (short)(block[i] | (block[i + 1] << 8));
                        int value = Math.Max(short.MinValue, Math.Min(short.MaxValue, (int)(sample * gain)));
                        block[i] = (byte)(value & 255);
                        block[i + 1] = (byte)((value >> 8) & 255);
                    }
                    writer.Write(block, 0, read);
                }
            }
        }

        // Runs on the Windows CI runner without microphone/speakers. Uses the
        // same encode path as a real recording and decodes the resulting MP3.
        internal static bool VerifyAudioPipeline()
        {
            string root = Path.Combine(Path.GetTempPath(),
                "MiniRecPipelineTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string sourceA = Path.Combine(root, "left.wav");
                string sourceB = Path.Combine(root, "right.wav");
                foreach (string path in new[] { sourceA, sourceB })
                {
                    using (var writer = new WaveFileWriter(path, StandardFormat))
                    {
                        // Half a second of ordinary 16-bit PCM stereo audio.
                        for (int i = 0; i < 22050; i++)
                        {
                            short sample = (short)(8000 * Math.Sin(2 * Math.PI * 440 * i / 44100d));
                            byte[] frame = { (byte)sample, (byte)(sample >> 8),
                                             (byte)sample, (byte)(sample >> 8) };
                            writer.Write(frame, 0, frame.Length);
                        }
                    }
                }
                string mixed = Path.Combine(root, "mixed.wav");
                Mix(sourceA, sourceB, mixed, 0, Stopwatch.Frequency / 10);
                string normalized = Path.Combine(root, "normalized.wav");
                NormalizePcm(mixed, normalized);
                string wav = SaveProcessedAudio(normalized, root, "WAV");
                string mp3 = SaveProcessedAudio(normalized, root, "MP3");
                if (!File.Exists(wav) || !File.Exists(mp3) ||
                    new FileInfo(wav).Length < 10000 || new FileInfo(mp3).Length < 1000 ||
                    !string.Equals(Path.GetExtension(mp3), ".mp3", StringComparison.OrdinalIgnoreCase))
                    return false;
                using (var reader = new WaveFileReader(wav))
                    if (reader.TotalTime.TotalMilliseconds < 450) return false;
                using (var reader = new MediaFoundationReader(mp3))
                {
                    var buffer = new byte[8192];
                    if (reader.TotalTime.TotalMilliseconds < 400 ||
                        reader.Read(buffer, 0, buffer.Length) <= 0) return false;
                }

                // Simulate abrupt process termination: preserve a WAV whose
                // header was never finalized and repair it on next launch.
                string recovery = RecoveryStore.EnsureFolder(root);
                string interrupted = RecoveryStore.CreateCapturePath(recovery);
                File.Copy(sourceA, interrupted);
                byte[] interruptedBytes = File.ReadAllBytes(interrupted);
                int dataOffset = -1;
                for (int i = 12; i + 8 < Math.Min(interruptedBytes.Length, 256); i++)
                {
                    if (interruptedBytes[i] == 'd' && interruptedBytes[i + 1] == 'a' &&
                        interruptedBytes[i + 2] == 't' && interruptedBytes[i + 3] == 'a')
                    {
                        dataOffset = i + 4;
                        break;
                    }
                }
                if (dataOffset < 0) return false;
                Array.Clear(interruptedBytes, 4, 4);
                Array.Clear(interruptedBytes, dataOffset, 4);
                File.WriteAllBytes(interrupted, interruptedBytes);
                RecoveryStore.RepairOrphans(root);
                using (var reader = new WaveFileReader(interrupted))
                    return reader.TotalTime.TotalMilliseconds >= 450;
            }
            catch (Exception ex)
            {
                Diagnostics.Write("Audio pipeline regression: " + ex);
                return false;
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private sealed class CaptureTrack : IDisposable
        {
            private readonly WasapiCapture _capture;
            private readonly WaveFileWriter _writer;
            private readonly object _writerGate = new object();
            private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(false);
            private bool _started;
            private bool _disposed;
            public string Path { get; }
            public long StartTicks { get; private set; }
            public Exception Error { get; private set; }
            private CaptureTrack(WasapiCapture capture, string recoveryFolder)
            {
                _capture = capture;
                Path = RecoveryStore.CreateCapturePath(recoveryFolder);
                _writer = new WaveFileWriter(Path, capture.WaveFormat);
                capture.DataAvailable += DataAvailable;
                capture.RecordingStopped += (_, args) => { Error = args.Exception; _stopped.Set(); };
            }
            public static CaptureTrack OpenMicrophone(string deviceId, string recoveryFolder)
            {
                using (var e = new MMDeviceEnumerator())
                {
                    MMDevice device = null;
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        try { device = e.GetDevice(deviceId); }
                        catch (Exception ex)
                        {
                            Diagnostics.Write("Saved microphone no longer available; using system default: " + ex.Message);
                        }
                    }
                    if (device == null || device.State != DeviceState.Active)
                        device = e.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                    return new CaptureTrack(new WasapiCapture(device), recoveryFolder);
                }
            }
            public static CaptureTrack OpenLoopback(string recoveryFolder) =>
                new CaptureTrack(new WasapiLoopbackCapture(), recoveryFolder);

            private void DataAvailable(object sender, WaveInEventArgs args)
            {
                lock (_writerGate)
                    if (args.BytesRecorded > 0) _writer.Write(args.Buffer, 0, args.BytesRecorded);
            }
            public void Start()
            {
                StartTicks = Stopwatch.GetTimestamp();
                _capture.StartRecording();
                _started = true;
            }
            public void Stop() { if (_started) _capture.StopRecording(); else _stopped.Set(); }
            public void WaitStopped()
            {
                if (!_stopped.Wait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("录音设备未及时停止。");
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                try { _capture.Dispose(); } catch { }
                lock (_writerGate) { try { _writer.Dispose(); } catch { } }
                _stopped.Dispose();
            }
        }
    }
}
