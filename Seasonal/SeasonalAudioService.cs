using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Seasonal
{
    internal static class SeasonalWinMciAudio
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern int mciSendString(
            string command,
            StringBuilder? buffer,
            int bufferSize,
            IntPtr hwndCallback
        );

        public static void Open(string alias, string filePath)
        {
            try
            {
                _ = mciSendString($"close {alias}", null, 0, IntPtr.Zero);
                int res = mciSendString(
                    $"open \"{filePath}\" type waveaudio alias {alias}",
                    null,
                    0,
                    IntPtr.Zero
                );
                if (res != 0)
                {
                    Log.Debug("mciSendString open {Alias} returned code {Code}", alias, res);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to open MCI alias {Alias}", alias);
            }
        }

        public static void Play(string alias)
        {
            try
            {
                _ = mciSendString($"play {alias} from 0", null, 0, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to play MCI alias {Alias}", alias);
            }
        }

        public static void Stop(string alias)
        {
            try
            {
                _ = mciSendString($"stop {alias}", null, 0, IntPtr.Zero);
            }
            catch
            {
                Log.Warning("Failed to stop MCI alias {Alias}", alias);
            }
        }

        public static void Close(string alias)
        {
            try
            {
                _ = mciSendString($"close {alias}", null, 0, IntPtr.Zero);
            }
            catch
            {
                Log.Warning("Failed to close MCI alias {Alias}", alias);
            }
        }
    }

    public sealed class SeasonalAudioService
    {
        private readonly ISettingsService _settingsService;
        private readonly string _audioDir;

        private DispatcherTimer? _ambientLoopTimer;
        private DispatcherTimer? _randomCueTimer;
        private readonly Random _rnd = new();
        private bool _ambientRequested;

        private string _ambientPath = string.Empty;
        private string _clickPath = string.Empty;
        private string _successPath = string.Empty;
        private string _specialPath = string.Empty;

        public SeasonalAudioService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _audioDir = Path.Combine(Path.GetTempPath(), "TextureSwapper_Seasonal_Audio");

            try
            {
                if (!Directory.Exists(_audioDir))
                {
                    _ = Directory.CreateDirectory(_audioDir);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to create Seasonal audio temp directory.");
            }

            _ = Task.Run(PrebakeSounds);
            InitializeTimers();
        }

        private AppSettings Settings => _settingsService.Load();

        public bool IsAudioEnabled =>
            Settings.EnableSeasonalSound
            && SeasonalThemeConfig.Current.AudioPreset != AudioPreset.None;

        private void InitializeTimers()
        {
            _ = Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                _ambientLoopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10.0) };
                _ambientLoopTimer.Tick += (_, _) =>
                {
                    if (_ambientRequested && IsAudioEnabled && File.Exists(_ambientPath))
                    {
                        SeasonalWinMciAudio.Play("seasonal_ambient");
                    }
                };

                _randomCueTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
                _randomCueTimer.Tick += OnRandomCueTimerTick;
                _randomCueTimer.Start();
            });
        }

        private void OnRandomCueTimerTick(object? sender, EventArgs e)
        {
            if (!IsAudioEnabled || !Settings.EnableSeasonalRandomAudio)
            {
                return;
            }

            if (_rnd.NextDouble() < 0.35)
            {
                PlaySpecialCue(force: false);
            }
        }

        private void PrebakeSounds()
        {
            try
            {
                AudioPreset preset = SeasonalThemeConfig.Current.AudioPreset;
                if (preset == AudioPreset.None)
                {
                    return;
                }

                _ambientPath = Path.Combine(_audioDir, $"ambient_{preset}.wav");
                _clickPath = Path.Combine(_audioDir, $"click_{preset}.wav");
                _successPath = Path.Combine(_audioDir, $"success_{preset}.wav");
                _specialPath = Path.Combine(_audioDir, $"special_{preset}.wav");

                GenerateWavFile(_clickPath, GenerateClickWav(preset));
                GenerateWavFile(_successPath, GenerateSuccessWav(preset));
                GenerateWavFile(_specialPath, GenerateSpecialWav(preset));
                GenerateWavFile(_ambientPath, GenerateAmbientWav(preset));

                SeasonalWinMciAudio.Open("seasonal_click", _clickPath);
                SeasonalWinMciAudio.Open("seasonal_success", _successPath);
                SeasonalWinMciAudio.Open("seasonal_special", _specialPath);
                SeasonalWinMciAudio.Open("seasonal_ambient", _ambientPath);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Seasonal sound prebake encountered an error.");
            }
        }

        public void PlayClick()
        {
            if (!IsAudioEnabled || !File.Exists(_clickPath))
            {
                return;
            }

            SeasonalWinMciAudio.Play("seasonal_click");
        }

        public void PlayHollowClick()
        {
            PlayClick();
        }

        public void PlayCardSelect()
        {
            PlayClick();
        }

        public void PlayApplySuccess()
        {
            if (!IsAudioEnabled || !File.Exists(_successPath))
            {
                return;
            }

            SeasonalWinMciAudio.Play("seasonal_success");
        }

        public void PlaySpecialCue(bool force = false)
        {
            if (!force && !IsAudioEnabled)
            {
                return;
            }

            if (File.Exists(_specialPath))
            {
                SeasonalWinMciAudio.Play("seasonal_special");
            }
        }

        public void PlayEvilLaugh(bool force = false)
        {
            PlaySpecialCue(force);
        }

        public void PlayGhostWail()
        {
            PlaySpecialCue(force: false);
        }

        public void PlayLaunchGreeting()
        {
            PlaySpecialCue(force: false);
        }

        public void SetAmbientDrone(bool enable)
        {
            _ambientRequested = enable;
            if (enable && IsAudioEnabled && File.Exists(_ambientPath))
            {
                SeasonalWinMciAudio.Play("seasonal_ambient");
                _ambientLoopTimer?.Start();
            }
            else
            {
                _ambientLoopTimer?.Stop();
                SeasonalWinMciAudio.Stop("seasonal_ambient");
            }
        }

        private static void GenerateWavFile(string path, byte[] pcmData)
        {
            const int sampleRate = 44100;
            const short bitsPerSample = 16;
            const short channels = 1;

            using MemoryStream ms = new();
            using BinaryWriter bw = new(ms);

            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + pcmData.Length);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * (bitsPerSample / 8));
            bw.Write((short)(channels * (bitsPerSample / 8)));
            bw.Write(bitsPerSample);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(pcmData.Length);
            bw.Write(pcmData);

            File.WriteAllBytes(path, ms.ToArray());
        }

        private static byte[] GenerateClickWav(AudioPreset preset)
        {
            const int sampleRate = 44100;
            double duration = preset == AudioPreset.Christmas ? 0.08 : 0.06;
            int numSamples = (int)(sampleRate * duration);
            byte[] buffer = new byte[numSamples * 2];

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Exp(-t * (preset == AudioPreset.Christmas ? 35.0 : 50.0));
                double freq = preset == AudioPreset.Christmas ? 1760.0 : 600.0;
                double wave = Math.Sin(2.0 * Math.PI * freq * t);
                short sample = (short)(wave * env * 14000);
                buffer[i * 2] = (byte)(sample & 0xFF);
                buffer[(i * 2) + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return buffer;
        }

        private static byte[] GenerateSuccessWav(AudioPreset preset)
        {
            const int sampleRate = 44100;
            double duration = 0.35;
            int numSamples = (int)(sampleRate * duration);
            byte[] buffer = new byte[numSamples * 2];

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Exp(-t * 8.0);
                double freq = preset == AudioPreset.Christmas ? 1318.5 : 880.0;
                double wave =
                    (Math.Sin(2.0 * Math.PI * freq * t) * 0.7)
                    + (Math.Sin(2.0 * Math.PI * (freq * 1.5) * t) * 0.3);
                short sample = (short)(wave * env * 18000);
                buffer[i * 2] = (byte)(sample & 0xFF);
                buffer[(i * 2) + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return buffer;
        }

        private static byte[] GenerateSpecialWav(AudioPreset preset)
        {
            const int sampleRate = 44100;
            double duration = 0.8;
            int numSamples = (int)(sampleRate * duration);
            byte[] buffer = new byte[numSamples * 2];

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Sin(Math.PI * (t / duration));
                double freq =
                    preset == AudioPreset.Christmas
                        ? 1046.5 + (Math.Sin(t * 12.0) * 100.0)
                        : 300.0 - (t * 150.0);
                double wave = Math.Sin(2.0 * Math.PI * freq * t);
                short sample = (short)(wave * env * 15000);
                buffer[i * 2] = (byte)(sample & 0xFF);
                buffer[(i * 2) + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return buffer;
        }

        private static byte[] GenerateAmbientWav(AudioPreset preset)
        {
            const int sampleRate = 44100;
            double duration = 4.0;
            int numSamples = (int)(sampleRate * duration);
            byte[] buffer = new byte[numSamples * 2];

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Sin(Math.PI * (t / duration));
                double freq = preset == AudioPreset.Christmas ? 440.0 : 85.0;
                double wave =
                    (Math.Sin(2.0 * Math.PI * freq * t) * 0.5)
                    + (Math.Sin(2.0 * Math.PI * (freq * 1.01) * t) * 0.5);
                short sample = (short)(wave * env * 8000);
                buffer[i * 2] = (byte)(sample & 0xFF);
                buffer[(i * 2) + 1] = (byte)((sample >> 8) & 0xFF);
            }
            return buffer;
        }
    }
}
