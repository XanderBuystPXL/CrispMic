using System;

namespace CrispMic;

/// <summary>
/// High-performance Biquad IIR Filter (Direct Form II Transposed)
/// </summary>
public class BiquadFilter
{
    private float a1, a2, b0, b1, b2;
    private float z1, z2;

    public void Reset()
    {
        z1 = 0;
        z2 = 0;
    }

    public float Process(float inSample)
    {
        float outSample = inSample * b0 + z1;
        z1 = inSample * b1 - outSample * a1 + z2;
        z2 = inSample * b2 - outSample * a2;
        return outSample;
    }

    public void SetLowShelf(float sampleRate, float frequency, float gainDb, float q = 0.707f)
    {
        if (Math.Abs(gainDb) < 0.01f)
        {
            SetPassthrough();
            return;
        }

        float a = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);
        float sqrtA = MathF.Sqrt(a);

        float b0_raw = a * ((a + 1f) - (a - 1f) * cosW0 + 2f * sqrtA * alpha);
        float b1_raw = 2f * a * ((a - 1f) - (a + 1f) * cosW0);
        float b2_raw = a * ((a + 1f) - (a - 1f) * cosW0 - 2f * sqrtA * alpha);
        float a0_raw = (a + 1f) + (a - 1f) * cosW0 + 2f * sqrtA * alpha;
        float a1_raw = -2f * ((a - 1f) + (a + 1f) * cosW0);
        float a2_raw = (a + 1f) + (a - 1f) * cosW0 - 2f * sqrtA * alpha;

        Normalize(b0_raw, b1_raw, b2_raw, a0_raw, a1_raw, a2_raw);
    }

    public void SetPeakingEq(float sampleRate, float frequency, float gainDb, float q = 1.0f)
    {
        if (Math.Abs(gainDb) < 0.01f)
        {
            SetPassthrough();
            return;
        }

        float a = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);

        float b0_raw = 1f + alpha * a;
        float b1_raw = -2f * cosW0;
        float b2_raw = 1f - alpha * a;
        float a0_raw = 1f + alpha / a;
        float a1_raw = -2f * cosW0;
        float a2_raw = 1f - alpha / a;

        Normalize(b0_raw, b1_raw, b2_raw, a0_raw, a1_raw, a2_raw);
    }

    public void SetHighShelf(float sampleRate, float frequency, float gainDb, float q = 0.707f)
    {
        if (Math.Abs(gainDb) < 0.01f)
        {
            SetPassthrough();
            return;
        }

        float a = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * frequency / sampleRate;
        float cosW0 = MathF.Cos(w0);
        float sinW0 = MathF.Sin(w0);
        float alpha = sinW0 / (2f * q);
        float sqrtA = MathF.Sqrt(a);

        float b0_raw = a * ((a + 1f) + (a - 1f) * cosW0 + 2f * sqrtA * alpha);
        float b1_raw = -2f * a * ((a - 1f) + (a + 1f) * cosW0);
        float b2_raw = a * ((a + 1f) + (a - 1f) * cosW0 - 2f * sqrtA * alpha);
        float a0_raw = (a + 1f) - (a - 1f) * cosW0 + 2f * sqrtA * alpha;
        float a1_raw = 2f * ((a - 1f) - (a + 1f) * cosW0);
        float a2_raw = (a + 1f) - (a - 1f) * cosW0 - 2f * sqrtA * alpha;

        Normalize(b0_raw, b1_raw, b2_raw, a0_raw, a1_raw, a2_raw);
    }

    private void SetPassthrough()
    {
        b0 = 1f;
        b1 = 0f;
        b2 = 0f;
        a1 = 0f;
        a2 = 0f;
    }

    private void Normalize(float b0_raw, float b1_raw, float b2_raw, float a0_raw, float a1_raw, float a2_raw)
    {
        b0 = b0_raw / a0_raw;
        b1 = b1_raw / a0_raw;
        b2 = b2_raw / a0_raw;
        a1 = a1_raw / a0_raw;
        a2 = a2_raw / a0_raw;
    }
}
