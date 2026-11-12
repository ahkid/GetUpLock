// Native bindings match MediaPipe 1.1.1's Apache-2.0 C API (ctypes definitions
// in tasks/python/vision/pose_landmarker.py and components/containers/landmark_c.py).
// This wrapper runs the original model and DLL. It does not use Python/OpenCV.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SquatLockTiny
{
    public sealed class PoseDetector : IDisposable
    {
        private const string Library = "libmediapipe.dll";
        private IntPtr handle;
        private IntPtr modelBuffer;
        private long lastTimestamp = -1;
        private readonly int ownerThread;

        // C bool is one byte; default sequential alignment supplies the C padding.
        [StructLayout(LayoutKind.Sequential)]
        private struct BaseOptions
        {
            public IntPtr ModelAssetBuffer;
            public uint ModelAssetBufferCount;
            public IntPtr ModelAssetPath;
            public int FileDescriptor;
            public int Delegate;
            public int HostEnvironment;
            public int HostSystem;
            public IntPtr HostVersion;
            public IntPtr CaBundlePath;
            public IntPtr AppId;
            public IntPtr AppVersion;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Options
        {
            public BaseOptions Base;
            public int RunningMode;
            public int NumPoses;
            public float MinDetectionConfidence;
            public float MinPresenceConfidence;
            public float MinTrackingConfidence;
            public byte OutputSegmentationMasks;
            public IntPtr ResultCallback;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Result
        {
            public IntPtr SegmentationMasks;
            public uint SegmentationMasksCount;
            public IntPtr PoseLandmarks;
            public uint PoseLandmarksCount;
            public IntPtr WorldLandmarks;
            public uint WorldLandmarksCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LandmarkList
        {
            public IntPtr Landmarks;
            public uint Count;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeLandmark
        {
            public float X;
            public float Y;
            public float Z;
            public byte HasVisibility;
            public float Visibility;
            public byte HasPresence;
            public float Presence;
            public IntPtr Name;
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int MpPoseLandmarkerCreate(ref Options options, out IntPtr landmarker, out IntPtr error);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int MpPoseLandmarkerDetectForVideo(IntPtr landmarker, IntPtr image, IntPtr processingOptions,
            long timestampMs, ref Result result, out IntPtr error);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void MpPoseLandmarkerCloseResult(ref Result result);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int MpPoseLandmarkerClose(IntPtr landmarker, out IntPtr error);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern int MpImageCreateFromUint8Data(int format, int width, int height,
            IntPtr data, int dataSize, out IntPtr image, out IntPtr error);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void MpImageFree(IntPtr image);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern void MpErrorFree(IntPtr error);

        public PoseDetector(string modelPath)
        {
            if (IntPtr.Size != 8) throw new PlatformNotSupportedException(Strings.Needs64Bit);
            if (!File.Exists(modelPath)) throw new FileNotFoundException(Strings.ModelNotFound, modelPath);
            ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            ValidateAbi();
            // The upstream C library's Windows file opener fails on non-ASCII
            // paths. Let .NET open UTF-16 paths and supply the unchanged bytes.
            // Retain this buffer until the landmarker has finished closing.
            byte[] model = File.ReadAllBytes(Path.GetFullPath(modelPath));
            modelBuffer = Marshal.AllocHGlobal(model.Length);
            Marshal.Copy(model, 0, modelBuffer, model.Length);
            Options options = new Options();
            options.Base.ModelAssetBuffer = modelBuffer;
            options.Base.ModelAssetBufferCount = checked((uint)model.Length);
            options.Base.FileDescriptor = -1;
            options.Base.Delegate = 0; // CPU
            options.Base.HostSystem = 3; // Windows
            options.RunningMode = 2; // VIDEO (not the enum's zero-based ordinal)
            options.NumPoses = 1;
            options.MinDetectionConfidence = 0.5f;
            options.MinPresenceConfidence = 0.5f;
            options.MinTrackingConfidence = 0.5f;
            IntPtr error;
            try
            {
                Check(MpPoseLandmarkerCreate(ref options, out handle, out error), error);
                if (handle == IntPtr.Zero) throw new InvalidOperationException(Strings.ModelCreateFailed);
            }
            catch
            {
                // On failure the C API owns any partial construction; it returns no valid handle.
                handle = IntPtr.Zero;
                Marshal.FreeHGlobal(modelBuffer);
                modelBuffer = IntPtr.Zero;
                throw;
            }
        }

        public Landmark[] Detect(byte[] rgb, int width, int height, long timestampMs)
        {
            CheckThread();
            if (handle == IntPtr.Zero) throw new ObjectDisposedException("PoseDetector");
            if (rgb == null || width <= 0 || height <= 0 || rgb.Length != checked(width * height * 3))
                throw new ArgumentException(Strings.RgbMismatch);
            timestampMs = Math.Max(timestampMs, checked(lastTimestamp + 1));
            lastTimestamp = timestampMs;
            GCHandle pinned = GCHandle.Alloc(rgb, GCHandleType.Pinned);
            IntPtr image = IntPtr.Zero;
            Result result = new Result();
            bool resultReady = false;
            try
            {
                IntPtr error;
                Check(MpImageCreateFromUint8Data(1, width, height, pinned.AddrOfPinnedObject(), rgb.Length, out image, out error), error);
                Check(MpPoseLandmarkerDetectForVideo(handle, image, IntPtr.Zero, timestampMs, ref result, out error), error);
                resultReady = true;
                if (result.PoseLandmarksCount == 0 || result.PoseLandmarks == IntPtr.Zero) return null;
                LandmarkList list = (LandmarkList)Marshal.PtrToStructure(result.PoseLandmarks, typeof(LandmarkList));
                if (list.Count != 33 || list.Landmarks == IntPtr.Zero)
                    throw new InvalidOperationException(Strings.UnexpectedLandmarks);
                Landmark[] landmarks = new Landmark[list.Count];
                int stride = Marshal.SizeOf(typeof(NativeLandmark));
                for (int i = 0; i < landmarks.Length; i++)
                {
                    NativeLandmark point = (NativeLandmark)Marshal.PtrToStructure(IntPtr.Add(list.Landmarks, i * stride), typeof(NativeLandmark));
                    landmarks[i] = new Landmark { X = point.X, Y = point.Y, Visibility = point.HasVisibility != 0 ? point.Visibility : 1.0f };
                }
                return landmarks;
            }
            finally
            {
                if (resultReady) MpPoseLandmarkerCloseResult(ref result);
                if (image != IntPtr.Zero) MpImageFree(image);
                pinned.Free();
            }
        }

        public void Dispose()
        {
            CheckThread();
            if (handle == IntPtr.Zero) return;
            IntPtr closing = handle;
            handle = IntPtr.Zero;
            try
            {
                IntPtr error;
                Check(MpPoseLandmarkerClose(closing, out error), error);
            }
            finally
            {
                if (modelBuffer != IntPtr.Zero) Marshal.FreeHGlobal(modelBuffer);
                modelBuffer = IntPtr.Zero;
            }
        }

        private void CheckThread()
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException(Strings.PoseThread);
        }

        private static void Check(int status, IntPtr error)
        {
            string message = null;
            try
            {
                if (error != IntPtr.Zero)
                {
                    int length = 0;
                    while (Marshal.ReadByte(error, length) != 0 && length < 65536) length++;
                    byte[] bytes = new byte[length];
                    Marshal.Copy(error, bytes, 0, length);
                    message = Encoding.UTF8.GetString(bytes);
                }
            }
            finally { if (error != IntPtr.Zero) MpErrorFree(error); }
            if (status != 0) throw new InvalidOperationException(message ?? (Strings.PoseEngineError + status));
        }

        internal static void ValidateAbi()
        {
            if (Marshal.SizeOf(typeof(BaseOptions)) != 72 || Marshal.SizeOf(typeof(Options)) != 104 ||
                Marshal.SizeOf(typeof(Result)) != 48 || Marshal.SizeOf(typeof(LandmarkList)) != 16 ||
                Marshal.SizeOf(typeof(NativeLandmark)) != 40 ||
                Marshal.OffsetOf(typeof(NativeLandmark), "Visibility").ToInt32() != 16 ||
                Marshal.OffsetOf(typeof(NativeLandmark), "Presence").ToInt32() != 24)
                throw new PlatformNotSupportedException(Strings.NativeLayoutMismatch);
        }
    }
}
