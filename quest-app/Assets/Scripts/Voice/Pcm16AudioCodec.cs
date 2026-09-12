using System;

namespace QuestAI.Voice
{
    public static class Pcm16AudioCodec
    {
        public static byte[] ResampleToPcm16(float[] source, int sourceRate, int targetRate)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<byte>();
            if (sourceRate <= 0 || targetRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceRate));

            int outputSamples = Math.Max(1, (int)Math.Round(source.Length * (double)targetRate / sourceRate));
            var output = new byte[outputSamples * 2];
            double ratio = sourceRate / (double)targetRate;

            for (int i = 0; i < outputSamples; i++)
            {
                double position = i * ratio;
                int left = Math.Min((int)position, source.Length - 1);
                int right = Math.Min(left + 1, source.Length - 1);
                float fraction = (float)(position - left);
                float sample = source[left] + (source[right] - source[left]) * fraction;
                sample = Math.Max(-1f, Math.Min(1f, sample));
                short pcm = (short)Math.Round(sample * short.MaxValue);
                output[i * 2] = (byte)(pcm & 0xff);
                output[i * 2 + 1] = (byte)((pcm >> 8) & 0xff);
            }
            return output;
        }

        public static void DecodePcm16(byte[] source, Action<float> writeSample)
        {
            if (source == null || writeSample == null)
                return;
            for (int i = 0; i + 1 < source.Length; i += 2)
            {
                short value = (short)(source[i] | (source[i + 1] << 8));
                writeSample(value / 32768f);
            }
        }
    }
}
