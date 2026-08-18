using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CrispMic;

public sealed class RNNoiseProcessor : IDisposable
{
    public const int FrameSize = 480; // 10ms at 48kHz

    private IntPtr _denoiseState;
    private bool _disposed;

    static RNNoiseProcessor()
    {
        NativeLibrary.SetDllImportResolver(typeof(RNNoiseProcessor).Assembly, DllImportResolver);
    }

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == "rnnoise")
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch = Environment.Is64BitProcess ? "win-x64" : "win-x86";

            string[] candidatePaths = [
                Path.Combine(baseDir, "rnnoise.dll"),
                Path.Combine(baseDir, "runtimes", arch, "native", "rnnoise.dll"),
                Path.Combine(baseDir, arch, "rnnoise.dll")
            ];

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    if (NativeLibrary.TryLoad(path, out IntPtr handle))
                    {
                        return handle;
                    }
                }
            }
        }
        return IntPtr.Zero;
    }

    public RNNoiseProcessor()
    {
        _denoiseState = NativeMethods.rnnoise_create(IntPtr.Zero);
        if (_denoiseState == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to initialize RNNoise state.");
        }
    }

    /// <summary>
    /// Process 480 samples of audio.
    /// Input and output are scaled in standard float audio [-1.0, 1.0].
    /// Returns voice activity detection probability (0.0 to 1.0).
    /// </summary>
    public float ProcessFrame(ReadOnlySpan<float> input, Span<float> output)
    {
        if (_disposed || _denoiseState == IntPtr.Zero) return 0f;

        float[] inRaw = new float[FrameSize];
        float[] outRaw = new float[FrameSize];

        for (int i = 0; i < FrameSize; i++)
        {
            inRaw[i] = input[i] * 32767.0f;
        }

        float vad = NativeMethods.rnnoise_process_frame(_denoiseState, outRaw, inRaw);

        for (int i = 0; i < FrameSize; i++)
        {
            output[i] = outRaw[i] / 32767.0f;
        }

        return vad;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_denoiseState != IntPtr.Zero)
            {
                NativeMethods.rnnoise_destroy(_denoiseState);
                _denoiseState = IntPtr.Zero;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~RNNoiseProcessor()
    {
        Dispose();
    }

    private static class NativeMethods
    {
        private const string LibraryName = "rnnoise";

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr rnnoise_create(IntPtr model);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rnnoise_destroy(IntPtr st);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float rnnoise_process_frame(IntPtr st, [Out] float[] output, [In] float[] input);
    }
}
