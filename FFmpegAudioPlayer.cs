// Version: 0.0.0.2
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;
using System.Collections.Concurrent;

public class FFmpegAudioPlayer : IDisposable
{
    private long _playedSamples;
    private readonly int _sampleRate;
    private readonly int _channels;

    public double Clock => _playedSamples / (double)_sampleRate;

    public readonly BufferedWaveProvider _buffer;
    public readonly WaveOutEvent _output;

    public FFmpegAudioPlayer(int sampleRate, int channels)
    {
        _sampleRate = sampleRate;
        _channels = channels;

        _buffer = new BufferedWaveProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels));

        _output = new WaveOutEvent();
        _output.Init(_buffer);
        _output.Play();
    }

    public void AddSamples(byte[] data, int bytes)
    {
        _playedSamples += bytes / 2 / _channels;
        _buffer.AddSamples(data, 0, bytes);
    }

    public void Dispose()
    {
        _output.Stop();
        _output.Dispose();
    }
}
