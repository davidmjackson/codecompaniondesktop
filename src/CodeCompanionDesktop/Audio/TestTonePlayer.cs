using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace CodeCompanionDesktop.Audio;

public sealed class TestTonePlayer
{
    private const int SampleRate = 44100;
    private const short BitsPerSample = 16;
    private const short ChannelCount = 1;
    private const double Frequency = 880.0;
    private const double DurationSeconds = 0.45;
    private const short Amplitude = 12000;

    public async Task<string> PlayAsync()
    {
        var path = EnsureTestToneFile();

        await Task.Run(() =>
        {
            using var player = new SoundPlayer(path);
            player.Load();
            player.PlaySync();
        });

        return path;
    }

    private static string EnsureTestToneFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodeCompanionDesktop");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "test-tone.wav");
        if (File.Exists(path))
        {
            return path;
        }

        WriteTestTone(path);
        return path;
    }

    private static void WriteTestTone(string path)
    {
        var sampleCount = (int)(SampleRate * DurationSeconds);
        var byteRate = SampleRate * ChannelCount * BitsPerSample / 8;
        var blockAlign = (short)(ChannelCount * BitsPerSample / 8);
        var dataSize = sampleCount * blockAlign;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(ChannelCount);
        writer.Write(SampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(BitsPerSample);

        writer.Write("data"u8);
        writer.Write(dataSize);

        for (var i = 0; i < sampleCount; i++)
        {
            var value = Math.Sin(2 * Math.PI * Frequency * i / SampleRate);
            writer.Write((short)(Amplitude * value));
        }
    }
}

