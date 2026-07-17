using System.Runtime.InteropServices;
using PortAudioSharp;

namespace Cove.Dictation;

public sealed class PortAudioRecorder : IAudioRecorder, IDisposable
{
    private readonly object _lifecycle = new();
    private readonly object _capture = new();
    private readonly List<float> _captured = new();
    private PortAudioSharp.Stream? _stream;
    private int _deviceRate = DictationService.SampleRate;
    private static bool _initialized;

    public void Start()
    {
        lock (_lifecycle)
        {
            if (_stream is not null)
                throw new DictationException("recorder already started");
            if (!_initialized)
            {
                PortAudio.Initialize();
                _initialized = true;
            }
            var device = PortAudio.DefaultInputDevice;
            if (device == PortAudio.NoDevice)
                throw new DictationException("no default input device (microphone unavailable)");
            var info = PortAudio.GetDeviceInfo(device);
            _deviceRate = (int)info.defaultSampleRate;
            if (_deviceRate <= 0)
                _deviceRate = DictationService.SampleRate;
            var parameters = new StreamParameters
            {
                device = device,
                channelCount = 1,
                sampleFormat = SampleFormat.Float32,
                suggestedLatency = info.defaultLowInputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero,
            };
            lock (_capture)
                _captured.Clear();
            try
            {
                var stream = new PortAudioSharp.Stream(parameters, null, _deviceRate, 0,
                    StreamFlags.ClipOff, OnAudio, IntPtr.Zero);
                stream.Start();
                _stream = stream;
            }
            catch (Exception ex) when (ex is not DictationException)
            {
                throw new DictationException($"microphone capture failed to start: {ex.Message}", ex);
            }
        }
    }

    public float[] Stop()
    {
        PortAudioSharp.Stream? stream;
        int deviceRate;
        lock (_lifecycle)
        {
            stream = _stream;
            _stream = null;
            deviceRate = _deviceRate;
        }
        if (stream is null)
            return [];
        try
        {
            stream.Stop();
        }
        finally
        {
            stream.Dispose();
        }
        float[] clip;
        lock (_capture)
        {
            clip = _captured.ToArray();
            _captured.Clear();
        }
        return AudioResampler.Resample(clip, deviceRate, DictationService.SampleRate);
    }

    private StreamCallbackResult OnAudio(IntPtr input, IntPtr output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        if (input != IntPtr.Zero && frameCount > 0)
        {
            var buffer = new float[frameCount];
            Marshal.Copy(input, buffer, 0, (int)frameCount);
            lock (_capture)
            {
                if (_captured.Count < (int)(DictationService.MaxClipSeconds * _deviceRate))
                    _captured.AddRange(buffer);
            }
        }
        return StreamCallbackResult.Continue;
    }

    public void Dispose()
    {
        PortAudioSharp.Stream? stream;
        lock (_lifecycle)
        {
            stream = _stream;
            _stream = null;
        }
        stream?.Dispose();
    }
}
