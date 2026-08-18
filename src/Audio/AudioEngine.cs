using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CrispMic;

public class AudioDeviceInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    public override string ToString() => Name;
}

public class AudioEngine : IDisposable
{
    private readonly AppConfig _config;
    private WasapiCapture? _capture;
    private WasapiOut? _renderOut;
    private BufferedWaveProvider? _renderBuffer;

    private WasapiOut? _monitorOut;
    private BufferedWaveProvider? _monitorBuffer;

    private RNNoiseProcessor? _rnnoise;
    private readonly BiquadFilter _bassFilter = new();
    private readonly BiquadFilter _midFilter = new();
    private readonly BiquadFilter _trebleFilter = new();

    private readonly object _lock = new();
    private readonly List<float> _inputAccumulator = new(2048);

    private float _gateEnvelope = 1.0f;
    private bool _isMuted = false;
    private bool _disposed = false;

    public float CurrentPeak { get; private set; }
    public float CurrentVad { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action<string>? OnError;

    public AudioEngine(AppConfig config)
    {
        _config = config;
        _isMuted = config.Muted;
        UpdateFilters();
    }

    public static List<AudioDeviceInfo> GetInputDevices()
    {
        var list = new List<AudioDeviceInfo>();
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        foreach (var dev in devices)
        {
            list.Add(new AudioDeviceInfo { Id = dev.ID, Name = dev.FriendlyName });
        }
        return list;
    }

    public static List<AudioDeviceInfo> GetOutputDevices()
    {
        var list = new List<AudioDeviceInfo>();
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var dev in devices)
        {
            list.Add(new AudioDeviceInfo { Id = dev.ID, Name = dev.FriendlyName });
        }
        return list;
    }

    public void UpdateFilters()
    {
        lock (_lock)
        {
            const float sampleRate = 48000f;
            _bassFilter.SetLowShelf(sampleRate, 120f, _config.BassDb);
            _midFilter.SetPeakingEq(sampleRate, 1200f, _config.MidDb);
            _trebleFilter.SetHighShelf(sampleRate, 5500f, _config.TrebleDb);
        }
    }

    public void SetMuted(bool muted)
    {
        _isMuted = muted;
        _config.Muted = muted;
    }

    public void Start()
    {
        lock (_lock)
        {
            Stop();

            try
            {
                _rnnoise = new RNNoiseProcessor();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"RNNoise initialization error: {ex.Message}");
            }

            try
            {
                using var enumerator = new MMDeviceEnumerator();

                // 1. Resolve Capture Device
                MMDevice? captureDevice = null;
                if (!string.IsNullOrEmpty(_config.InputDeviceId))
                {
                    try { captureDevice = enumerator.GetDevice(_config.InputDeviceId); } catch { }
                }
                captureDevice ??= enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);

                // 2. Resolve Render Device (VB-Cable Input or default)
                MMDevice? renderDevice = null;
                if (!string.IsNullOrEmpty(_config.OutputDeviceId))
                {
                    try { renderDevice = enumerator.GetDevice(_config.OutputDeviceId); } catch { }
                }
                if (renderDevice == null)
                {
                    var renderDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                    renderDevice = renderDevices.FirstOrDefault(d => d.FriendlyName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
                                ?? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                }

                var standardFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

                // Setup Render
                _renderBuffer = new BufferedWaveProvider(standardFormat)
                {
                    DiscardOnBufferOverflow = true
                };
                _renderOut = new WasapiOut(renderDevice, AudioClientShareMode.Shared, useEventSync: true, 15);
                _renderOut.Init(_renderBuffer);
                _renderOut.Play();

                // Setup Monitor if enabled
                if (_config.MonitoringEnabled && !string.IsNullOrEmpty(_config.MonitorDeviceId))
                {
                    try
                    {
                        var monitorDevice = enumerator.GetDevice(_config.MonitorDeviceId);
                        _monitorBuffer = new BufferedWaveProvider(standardFormat)
                        {
                            DiscardOnBufferOverflow = true
                        };
                        _monitorOut = new WasapiOut(monitorDevice, AudioClientShareMode.Shared, useEventSync: true, 15);
                        _monitorOut.Init(_monitorBuffer);
                        _monitorOut.Play();
                    }
                    catch { }
                }

                // Setup Capture
                _capture = new WasapiCapture(captureDevice, useEventSync: true, 10);
                _capture.DataAvailable += OnCaptureDataAvailable;
                _capture.RecordingStopped += (s, e) => { IsRunning = false; };
                _capture.StartRecording();

                IsRunning = true;
            }
            catch (Exception ex)
            {
                Stop();
                OnError?.Invoke($"Audio stream start failed: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            try
            {
                if (_capture != null)
                {
                    _capture.StopRecording();
                    _capture.Dispose();
                    _capture = null;
                }
            }
            catch { }

            try
            {
                if (_renderOut != null)
                {
                    _renderOut.Stop();
                    _renderOut.Dispose();
                    _renderOut = null;
                }
            }
            catch { }

            try
            {
                if (_monitorOut != null)
                {
                    _monitorOut.Stop();
                    _monitorOut.Dispose();
                    _monitorOut = null;
                }
            }
            catch { }

            _rnnoise?.Dispose();
            _rnnoise = null;

            _inputAccumulator.Clear();
            IsRunning = false;
            CurrentPeak = 0f;
            CurrentVad = 0f;
        }
    }

    private void OnCaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposed || e.BytesRecorded == 0 || _capture == null) return;

        WaveFormat format = _capture.WaveFormat;
        int bytesRecorded = e.BytesRecorded;
        byte[] buffer = e.Buffer;

        lock (_lock)
        {
            // Convert incoming PCM bytes to mono float samples
            if (format.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                int sampleCount = bytesRecorded / 4;
                int channels = format.Channels;
                for (int i = 0; i < sampleCount; i += channels)
                {
                    float sample = BitConverter.ToSingle(buffer, i * 4);
                    _inputAccumulator.Add(sample);
                }
            }
            else if (format.BitsPerSample == 16)
            {
                int sampleCount = bytesRecorded / 2;
                int channels = format.Channels;
                for (int i = 0; i < sampleCount; i += channels)
                {
                    short sampleShort = BitConverter.ToInt16(buffer, i * 2);
                    _inputAccumulator.Add(sampleShort / 32768.0f);
                }
            }
            else if (format.BitsPerSample == 24)
            {
                int sampleCount = bytesRecorded / 3;
                int channels = format.Channels;
                for (int i = 0; i < sampleCount; i += channels)
                {
                    int sampleInt = (buffer[i * 3 + 2] << 24) | (buffer[i * 3 + 1] << 16) | (buffer[i * 3] << 8);
                    _inputAccumulator.Add(sampleInt / 2147483648.0f);
                }
            }

            // Process full 480-sample (10ms) frames
            const int frameSize = RNNoiseProcessor.FrameSize;
            float maxPeak = 0f;

            float inputGainFactor = MathF.Pow(10f, _config.InputGainDb / 20f);
            float outputGainFactor = MathF.Pow(10f, _config.OutputGainDb / 20f);

            float[] frameIn = new float[frameSize];
            float[] frameOut = new float[frameSize];
            byte[] outBytes = new byte[frameSize * 4];

            while (_inputAccumulator.Count >= frameSize)
            {
                for (int i = 0; i < frameSize; i++)
                {
                    frameIn[i] = _inputAccumulator[i] * inputGainFactor;
                }
                _inputAccumulator.RemoveRange(0, frameSize);

                if (_isMuted)
                {
                    Array.Clear(frameOut, 0, frameSize);
                }
                else
                {
                    float vadProb = 1.0f;

                    // 1. Neural Denoising
                    if (_config.NoiseSuppressionEnabled && _rnnoise != null)
                    {
                        vadProb = _rnnoise.ProcessFrame(frameIn, frameOut);
                    }
                    else
                    {
                        Array.Copy(frameIn, frameOut, frameSize);
                    }

                    CurrentVad = vadProb;

                    // 2. Hard-Reduce VAD Gate
                    if (_config.HardReduceEnabled)
                    {
                        float targetGate = (vadProb >= _config.HardReduceThreshold) ? 1.0f : 0.0f;
                        // Smooth envelope: 5ms attack, 40ms release
                        float smoothing = (targetGate > _gateEnvelope) ? 0.4f : 0.05f;
                        _gateEnvelope += (targetGate - _gateEnvelope) * smoothing;
                        if (_gateEnvelope < 0.01f) _gateEnvelope = 0f;
                    }
                    else
                    {
                        _gateEnvelope = 1.0f;
                    }

                    // 3. 3-Band EQ, Output Gain & Soft Limiter
                    for (int i = 0; i < frameSize; i++)
                    {
                        float s = frameOut[i] * _gateEnvelope;

                        // EQ
                        s = _bassFilter.Process(s);
                        s = _midFilter.Process(s);
                        s = _trebleFilter.Process(s);

                        // Output Gain
                        s *= outputGainFactor;

                        // Soft Saturation Limiter (tanh curve to prevent digital clipping)
                        s = MathF.Tanh(s) * 0.98f;

                        frameOut[i] = s;

                        float absS = MathF.Abs(s);
                        if (absS > maxPeak) maxPeak = absS;
                    }
                }

                // Write to byte buffer
                Buffer.BlockCopy(frameOut, 0, outBytes, 0, outBytes.Length);

                _renderBuffer?.AddSamples(outBytes, 0, outBytes.Length);

                if (_config.MonitoringEnabled && _monitorBuffer != null)
                {
                    _monitorBuffer.AddSamples(outBytes, 0, outBytes.Length);
                }
            }

            CurrentPeak = maxPeak;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
