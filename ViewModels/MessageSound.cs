namespace DedLauncher.ViewModels;

/// <summary>
/// Генерирует короткий двухтоновый «бип-бип» для уведомлений о сообщениях.
/// Свой WAV вместо системного Asterisk — тот зависит от звуковой схемы
/// Windows и на части машин не проигрывается.
/// </summary>
public static class MessageSound
{
    private const int SampleRate = 44100;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    public static byte[] BuildWav()
    {
        var samples = new List<short>();

        // Два коротких тона: 880 Гц и 1174 Гц
        AppendTone(samples, 880, 0.09);
        AppendTone(samples, 1174, 0.12);

        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms))
        {
            int dataSize = samples.Count * 2;
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);                       // PCM
            bw.Write((short)Channels);
            bw.Write(SampleRate);
            bw.Write(SampleRate * Channels * BitsPerSample / 8);
            bw.Write((short)(Channels * BitsPerSample / 8));
            bw.Write((short)BitsPerSample);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);
            foreach (var s in samples)
                bw.Write(s);
        }
        return ms.ToArray();
    }

    private static void AppendTone(List<short> samples, double freq, double seconds)
    {
        int count = (int)(SampleRate * seconds);
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / SampleRate;
            // Плавное затухание, чтобы не щёлкало на конце
            double envelope = 1.0 - (double)i / count;
            envelope *= envelope;
            double value = Math.Sin(2 * Math.PI * freq * t) * envelope * 0.6;
            samples.Add((short)(value * short.MaxValue));
        }
    }
}
