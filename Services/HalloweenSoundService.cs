using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    internal static class WinMciAudio
    {
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
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

        public static void SetVolume(string alias, double vol)
        {
            try
            {
                int val = (int)Math.Clamp(vol * 1000.0, 0, 1000);
                _ = mciSendString($"setaudio {alias} volume to {val}", null, 0, IntPtr.Zero);
            }
            catch
            {
                Log.Warning("Failed to set volume for MCI alias {Alias}", alias);
            }
        }
    }

    public sealed class HalloweenSoundService
    {
        private readonly ISettingsService _settingsService;
        private readonly string _audioDir;

        private DispatcherTimer? _ambientLoopTimer;
        private DispatcherTimer? _scareTimer;
        private readonly Random _rnd = new();
        private bool _ambientRequested;

        private string _ambientPath = string.Empty;
        private string _clickPath = string.Empty;
        private string _laughPath = string.Empty;
        private string _wailPath = string.Empty;
        private string _cacklePath = string.Empty;
        private string _roarPath = string.Empty;
        private string _chimePath = string.Empty;
        private string _applySpellPath = string.Empty;
        private string _launchWhisperPath = string.Empty;

        private SoundPlayer? _backupClickPlayer;

        public HalloweenSoundService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _audioDir = Path.Combine(Path.GetTempPath(), "TextureSwapper_Halloween_Audio");

            try
            {
                if (!Directory.Exists(_audioDir))
                {
                    _ = Directory.CreateDirectory(_audioDir);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to create Halloween audio temp directory.");
            }

            _ = Task.Run(PrebakeSounds);
            InitializeTimers();
        }

        private AppSettings Settings => _settingsService.Load();

        public bool IsAudioEnabled => Settings.EnableHalloweenSound;

        private void InitializeTimers()
        {
            _ = (
                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    _ambientLoopTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(9.5),
                    };
                    _ambientLoopTimer.Tick += (_, _) =>
                    {
                        if (_ambientRequested && IsAudioEnabled)
                        {
                            WinMciAudio.Play("ambient");
                        }
                    };

                    _scareTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(50) };
                    _scareTimer.Tick += OnScareTimerTick;
                })
            );
        }

        private void OnScareTimerTick(object? sender, EventArgs e)
        {
            try
            {
                if (!IsAudioEnabled || !Settings.EnableHalloweenRandomScares)
                {
                    return;
                }

                if (_scareTimer != null)
                {
                    _scareTimer.Interval = TimeSpan.FromSeconds(_rnd.Next(45, 95));
                }

                int choice = _rnd.Next(0, 4);
                switch (choice)
                {
                    case 0:
                        PlayEvilLaugh();
                        break;
                    case 1:
                        PlayGhostWail();
                        break;
                    case 2:
                        PlayWitchCackle();
                        break;
                    case 3:
                        PlayMonsterRoar();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during Halloween scare timer tick.");
            }
        }

        public void PlayLaunchGreeting()
        {
            if (!IsAudioEnabled)
            {
                return;
            }

            PlaySoundFile("launch", _launchWhisperPath, volume: 0.85);
        }

        public void PlayEvilLaugh(bool force = false)
        {
            if (!force && !IsAudioEnabled)
            {
                return;
            }

            PlaySoundFile("scare", _laughPath, volume: 0.9);
        }

        public void PlayGhostWail(bool force = false)
        {
            if (!force && !IsAudioEnabled)
            {
                return;
            }

            PlaySoundFile("scare", _wailPath, volume: 0.85);
        }

        public void PlayWitchCackle(bool force = false)
        {
            if (!force && !IsAudioEnabled)
            {
                return;
            }

            PlaySoundFile("scare", _cacklePath, volume: 0.85);
        }

        public void PlayMonsterRoar(bool force = false)
        {
            if (!force && !IsAudioEnabled)
            {
                return;
            }

            PlaySoundFile("scare", _roarPath, volume: 0.9);
        }

        public void PlaySpookyChime()
        {
            if (!IsAudioEnabled)
            {
                return;
            }

            PlaySoundFile("sfx", _chimePath, volume: 0.8);
        }

        private long _lastClickTicks;

        public void PlayHollowClick()
        {
            if (!IsAudioEnabled)
            {
                return;
            }

            long now = DateTime.UtcNow.Ticks;
            if (now - _lastClickTicks < TimeSpan.FromMilliseconds(40).Ticks)
            {
                return;
            }
            _lastClickTicks = now;

            WinMciAudio.SetVolume("click", 0.75);
            WinMciAudio.Play("click");

            if (_backupClickPlayer != null)
            {
                try
                {
                    _backupClickPlayer.Play();
                }
                catch { }
            }
        }

        public void PlayCardSelect()
        {
            PlayHollowClick();
        }

        public void PlayApplySuccess()
        {
            if (!IsAudioEnabled)
            {
                return;
            }

            PlaySoundFile("sfx", _applySpellPath, volume: 0.85);
        }

        public void SetAmbientDrone(bool enable)
        {
            _ambientRequested = enable;

            _ = (
                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    try
                    {
                        if (enable && IsAudioEnabled && File.Exists(_ambientPath))
                        {
                            WinMciAudio.Open("ambient", _ambientPath);
                            WinMciAudio.SetVolume("ambient", 0.75);
                            WinMciAudio.Play("ambient");

                            _ambientLoopTimer?.Start();
                            _scareTimer?.Start();
                        }
                        else
                        {
                            _ambientLoopTimer?.Stop();
                            _scareTimer?.Stop();
                            WinMciAudio.Stop("ambient");
                            WinMciAudio.Close("ambient");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to toggle Halloween ambient drone.");
                    }
                })
            );
        }

        public void UpdateAmbientVolume(double volume)
        {
            WinMciAudio.SetVolume("ambient", volume);
        }

        private long _lastPreviewTicks;

        public void PlayPreviewClick(double volume)
        {
            if (!IsAudioEnabled)
            {
                return;
            }

            long now = DateTime.UtcNow.Ticks;
            if (now - _lastPreviewTicks < TimeSpan.FromMilliseconds(100).Ticks)
            {
                return;
            }
            _lastPreviewTicks = now;

            double effectiveVolume = Math.Clamp(0.85 * volume, 0.0, 1.0);
            WinMciAudio.SetVolume("click", effectiveVolume);
            WinMciAudio.Play("click");
        }

        private void PlaySoundFile(string alias, string filePath, double volume = 0.85)
        {
            if (!IsAudioEnabled || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }

            double effectiveVolume = Math.Clamp(volume, 0.0, 1.0);

            _ = Task.Run(() =>
            {
                try
                {
                    WinMciAudio.Open(alias, filePath);
                    WinMciAudio.SetVolume(alias, effectiveVolume);
                    WinMciAudio.Play(alias);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to play sound on alias {Alias}", alias);
                }
            });
        }

        private void PrebakeSounds()
        {
            try
            {
                _clickPath = Path.Combine(_audioDir, "click.wav");
                _laughPath = Path.Combine(_audioDir, "evil_laugh.wav");
                _wailPath = Path.Combine(_audioDir, "ghost_wail.wav");
                _cacklePath = Path.Combine(_audioDir, "witch_cackle.wav");
                _roarPath = Path.Combine(_audioDir, "monster_roar.wav");
                _chimePath = Path.Combine(_audioDir, "chime.wav");
                _applySpellPath = Path.Combine(_audioDir, "apply_spell.wav");
                _ambientPath = Path.Combine(_audioDir, "ambient_drone.wav");
                _launchWhisperPath = Path.Combine(_audioDir, "launch_whisper.wav");

                byte[] clickBytes = GenerateHollowClickPcm();
                File.WriteAllBytes(_clickPath, clickBytes);
                File.WriteAllBytes(_laughPath, GenerateEvilLaughPcm());
                File.WriteAllBytes(_wailPath, GenerateGhostWailPcm());
                File.WriteAllBytes(_cacklePath, GenerateWitchCacklePcm());
                File.WriteAllBytes(_roarPath, GenerateMonsterRoarPcm());
                File.WriteAllBytes(_chimePath, GenerateSpookyChimePcm());
                File.WriteAllBytes(_applySpellPath, GenerateApplySpellPcm());
                File.WriteAllBytes(_ambientPath, GenerateAmbientDronePcm());
                File.WriteAllBytes(_launchWhisperPath, GenerateLaunchWhisperPcm());

                WinMciAudio.Open("click", _clickPath);
                WinMciAudio.SetVolume("click", 0.75);

                try
                {
                    _backupClickPlayer = new SoundPlayer(_clickPath);
                    _backupClickPlayer.LoadAsync();
                }
                catch { }

                Log.Information("Halloween audio assets generated and prebaked successfully.");

                if (_ambientRequested)
                {
                    SetAmbientDrone(true);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to generate Halloween procedural sounds.");
            }
        }

        private static byte[] GenerateHollowClickPcm()
        {
            const int sampleRate = 44100;
            const double duration = 0.065;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double freq = 720.0 * Math.Exp(-22.0 * t);
                double envelope = Math.Exp(-55.0 * t);
                double tone1 = Math.Sin(2.0 * Math.PI * freq * t);
                double tone2 = Math.Sin(2.0 * Math.PI * 1350.0 * t) * 0.3;
                double val = (tone1 + tone2) * envelope * 24000.0;
                samples[i] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] GenerateEvilLaughPcm()
        {
            const int sampleRate = 44100;
            const double totalDuration = 2.4;
            int totalSamples = (int)(sampleRate * totalDuration);
            short[] samples = new short[totalSamples];
            double[] raw = new double[totalSamples];

            var syllables = new[]
            {
                new
                {
                    Start = 0.04,
                    Dur = 0.42,
                    StartFreq = 220.0,
                    PeakFreq = 320.0,
                    EndFreq = 260.0,
                    Amp = 0.95,
                },
                new
                {
                    Start = 0.52,
                    Dur = 0.34,
                    StartFreq = 300.0,
                    PeakFreq = 300.0,
                    EndFreq = 240.0,
                    Amp = 1.0,
                },
                new
                {
                    Start = 0.92,
                    Dur = 0.32,
                    StartFreq = 270.0,
                    PeakFreq = 270.0,
                    EndFreq = 210.0,
                    Amp = 0.92,
                },
                new
                {
                    Start = 1.28,
                    Dur = 0.30,
                    StartFreq = 240.0,
                    PeakFreq = 240.0,
                    EndFreq = 180.0,
                    Amp = 0.85,
                },
                new
                {
                    Start = 1.62,
                    Dur = 0.72,
                    StartFreq = 210.0,
                    PeakFreq = 190.0,
                    EndFreq = 120.0,
                    Amp = 0.75,
                },
            };

            Random rng = new(666);

            for (int s = 0; s < syllables.Length; s++)
            {
                var syl = syllables[s];
                int startSample = (int)(syl.Start * sampleRate);
                int sylSamples = (int)(syl.Dur * sampleRate);
                int endSample = Math.Min(totalSamples, startSample + sylSamples);

                double phase = 0;
                for (int i = startSample; i < endSample; i++)
                {
                    double localT = (double)(i - startSample) / sampleRate;
                    double progress = localT / syl.Dur;

                    double freq =
                        progress < 0.25
                            ? syl.StartFreq + ((syl.PeakFreq - syl.StartFreq) * (progress / 0.25))
                            : syl.PeakFreq
                                - ((syl.PeakFreq - syl.EndFreq) * ((progress - 0.25) / 0.75));

                    double env =
                        progress < 0.08 ? progress / 0.08 : Math.Exp(-3.2 * (progress - 0.08));
                    phase += 2.0 * Math.PI * freq / sampleRate;
                    double f0 = Math.Sin(phase);
                    double f1 = Math.Sin(phase * 2.0) * 0.55;
                    double f2 = Math.Sin(phase * 3.0) * 0.35;
                    double tremolo = 1.0 + (0.28 * Math.Sin(2.0 * Math.PI * 7.5 * localT));
                    double noise = (rng.NextDouble() - 0.5) * 0.12;

                    raw[i] += (f0 + f1 + f2 + noise) * env * tremolo * syl.Amp;
                }
            }

            for (int i = 0; i < totalSamples; i++)
            {
                samples[i] = (short)Math.Clamp(raw[i] * 18000.0, short.MinValue, short.MaxValue);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] GenerateGhostWailPcm()
        {
            const int sampleRate = 44100;
            const double duration = 2.6;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];
            Random rng = new(42);

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Sin(Math.PI * Math.Pow(t / duration, 0.85));

                double f;
                if (t < 1.1)
                {
                    f = 290.0 + (190.0 * Math.Sin(t / 1.1 * (Math.PI / 2.0)));
                }
                else
                {
                    double sinkProgress = (t - 1.1) / (duration - 1.1);
                    f = 480.0 - (300.0 * Math.Pow(sinkProgress, 1.3));
                }

                double vibrato = 1.0 + (0.05 * Math.Sin(2.0 * Math.PI * 5.2 * t));
                double phase = 2.0 * Math.PI * f * vibrato * t;

                double fund = Math.Sin(phase);
                double sub = Math.Sin(phase * 0.5) * 0.4;
                double fifth = Math.Sin(phase * 1.5) * 0.25;
                double windNoise = (rng.NextDouble() - 0.5) * 0.08;

                double val = (fund + sub + fifth + windNoise) * env * 22000.0;
                samples[i] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] GenerateWitchCacklePcm()
        {
            const int sampleRate = 44100;
            const double duration = 1.8;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];
            Random rng = new(777);

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double burstCycle = t * 6.5 % 1.0;
                double burstEnv = Math.Sin(Math.PI * burstCycle) * Math.Exp(-1.5 * burstCycle);

                double freq = 820.0 + (380.0 * Math.Sin(2.0 * Math.PI * 9.0 * t));
                double screech =
                    Math.Sin(2.0 * Math.PI * freq * t)
                    + (Math.Sin(2.0 * Math.PI * (freq * 1.8) * t) * 0.4);
                double noise = (rng.NextDouble() - 0.5) * 0.15;

                double overallEnv = Math.Sin(Math.PI * (t / duration));
                double val = (screech + noise) * burstEnv * overallEnv * 20000.0;
                samples[i] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] GenerateMonsterRoarPcm()
        {
            const int sampleRate = 44100;
            const double duration = 2.0;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];
            Random rng = new(999);

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Sin(Math.PI * Math.Pow(t / duration, 0.6)) * Math.Exp(-0.8 * t);

                double freq = 85.0 - (30.0 * (t / duration));
                double sub = Math.Sin(2.0 * Math.PI * freq * t);
                double growl = Math.Sin(2.0 * Math.PI * (freq * 2.3) * t) * 0.5;
                double texture = Math.Sin(2.0 * Math.PI * 35.0 * t);
                double throatNoise = (rng.NextDouble() - 0.5) * 0.28;

                double combined = (sub + growl + throatNoise) * (0.7 + (0.3 * texture));
                double val = combined * env * 24000.0;
                samples[i] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] GenerateSpookyChimePcm()
        {
            const int sampleRate = 44100;
            const double duration = 1.9;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            double[] freqs = [523.25, 659.25, 783.99, 1046.50];
            double[] decays = [2.2, 2.8, 3.2, 4.0];

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double mix = 0;
                for (int f = 0; f < freqs.Length; f++)
                {
                    double bell = Math.Sin(2.0 * Math.PI * freqs[f] * t) * Math.Exp(-decays[f] * t);
                    mix += bell * (1.0 / (f + 1));
                }
                double val = mix * 18000.0;
                samples[i] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] GenerateApplySpellPcm()
        {
            const int sampleRate = 44100;
            const double duration = 0.85;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double f = 330.0 + (550.0 * Math.Pow(t / duration, 1.5));
                double envelope =
                    Math.Sin(Math.PI * Math.Pow(t / duration, 0.7)) * Math.Exp(-1.2 * t);
                double val =
                    (Math.Sin(2.0 * Math.PI * f * t) * 0.6)
                    + (Math.Sin(2.0 * Math.PI * (f * 1.5) * t) * 0.25)
                    + (Math.Sin(2.0 * Math.PI * (f * 2.0) * t) * 0.15);
                double shimmer = 1.0 + (0.15 * Math.Sin(2.0 * Math.PI * 18.0 * t));
                double finalSample = val * envelope * shimmer * 18000.0;
                samples[i] = (short)Math.Clamp(finalSample, short.MinValue, short.MaxValue);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] GenerateLaunchWhisperPcm()
        {
            const int sampleRate = 44100;
            const double duration = 2.0;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];
            Random rng = new(555);

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Sin(Math.PI * (t / duration));

                double sub = Math.Sin(2.0 * Math.PI * 62.0 * t) * 0.45;
                double hiss = (rng.NextDouble() - 0.5) * 0.25;
                double resonance =
                    Math.Sin(2.0 * Math.PI * 440.0 * Math.Sin(2.0 * Math.PI * 1.5 * t) * t) * 0.2;

                double val = (sub + hiss + resonance) * env * 17000.0;
                samples[i] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] GenerateAmbientDronePcm()
        {
            const int sampleRate = 44100;
            const double duration = 10.0;
            int totalSamples = (int)(sampleRate * duration);
            short[] samples = new short[totalSamples];
            Random rng = new(1337);

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;

                double sub1 = Math.Sin(2.0 * Math.PI * 48.0 * t) * 0.4;
                double sub2 = Math.Sin(2.0 * Math.PI * 64.0 * t) * 0.25;

                double windMod = 0.5 + (0.5 * Math.Sin(2.0 * Math.PI * 0.2 * t));
                double windNoise = (rng.NextDouble() - 0.5) * 0.18 * windMod;

                double bellMix = 0;
                if (t is >= 1.5 and < 5.0)
                {
                    double bt = t - 1.5;
                    bellMix += Math.Sin(2.0 * Math.PI * 220.0 * bt) * Math.Exp(-1.8 * bt) * 0.35;
                }
                if (t is >= 6.5 and < 10.0)
                {
                    double bt = t - 6.5;
                    bellMix += Math.Sin(2.0 * Math.PI * 196.0 * bt) * Math.Exp(-1.8 * bt) * 0.35;
                }

                double edgeFade = 1.0;
                const double fadeTime = 0.5;
                if (t < fadeTime)
                {
                    edgeFade = t / fadeTime;
                }
                else if (t > duration - fadeTime)
                {
                    edgeFade = (duration - t) / fadeTime;
                }

                double val = (sub1 + sub2 + windNoise + bellMix) * edgeFade * 15000.0;
                samples[i] = (short)Math.Clamp(val, short.MinValue, short.MaxValue);
            }

            return CreateWavHeaderAndData(samples, sampleRate);
        }

        private static byte[] CreateWavHeaderAndData(short[] samples, int sampleRate)
        {
            using MemoryStream ms = new();
            using BinaryWriter bw = new(ms);

            int subChunk2Size = samples.Length * 2;
            int chunkSize = 36 + subChunk2Size;

            bw.Write("RIFF"u8);
            bw.Write(chunkSize);
            bw.Write("WAVE"u8);
            bw.Write("fmt "u8);
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write("data"u8);
            bw.Write(subChunk2Size);
            for (int i = 0; i < samples.Length; i++)
            {
                bw.Write(samples[i]);
            }

            return ms.ToArray();
        }
    }
}
