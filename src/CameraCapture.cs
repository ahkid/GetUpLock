using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace SquatLockTiny
{
    // DirectShow owns all COM objects on one MTA thread. Stream callbacks only
    // publish a single owned Bitmap; an unread previous frame is disposed.
    public sealed class CameraCapture : IDisposable
    {
        readonly int index;
        readonly object sync = new object();
        readonly ManualResetEvent quit = new ManualResetEvent(false);
        readonly Thread worker;
        Bitmap latest;
        string error = "";
        int disposed, copying;
        bool quitClosed;
        volatile bool stopping;
        int width, height, stride;
        bool bottomUp;
        long lastFrameTicks;
        GrabberCallback callback;

        public CameraCapture(int index)
        {
            this.index = Math.Max(0, index);
            worker = new Thread(Run);
            worker.IsBackground = true;
            worker.Name = "GetUpLock DirectShow";
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
        }

        public string Error { get { lock (sync) return error; } }
        public bool IsAlive { get { return worker.IsAlive; } }
        public bool WaitForExit(int milliseconds)
        {
            if (Thread.CurrentThread == worker) return false;
            bool exited = worker.Join(milliseconds);
            if (exited) CloseQuitHandle();
            return exited;
        }
        public Bitmap TakeLatest()
        {
            lock (sync) { Bitmap b = latest; latest = null; return b; }
        }
        void Fail(string message) { lock (sync) error = message; }

        public void Stop()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            stopping = true;
            lock (sync)
            {
                if (!quitClosed) quit.Set();
                if (latest != null) latest.Dispose(); latest = null;
            }
        }
        public void Dispose()
        {
            Stop();
            // Native driver shutdown can be slow. Never release its COM objects
            // on the caller's thread or wait indefinitely.
            WaitForExit(1500);
        }

        void CloseQuitHandle()
        {
            lock (sync)
            {
                if (quitClosed) return;
                quitClosed = true;
                quit.Close();
            }
        }

        void Run()
        {
            object graphObject = null, builderObject = null, grabObject = null, sinkObject = null;
            IBaseFilter camera = null;
            IMediaControl control = null;
            ISampleGrabber grab = null;
            int initialized = CoInitializeEx(IntPtr.Zero, 0);
            try
            {
                if (stopping) return;
                camera = OpenCamera(index);
                graphObject = Activator.CreateInstance(Type.GetTypeFromCLSID(DsIds.FilterGraph, true));
                IGraphBuilder graph = (IGraphBuilder)graphObject;
                control = (IMediaControl)graphObject;
                builderObject = Activator.CreateInstance(Type.GetTypeFromCLSID(DsIds.CaptureGraphBuilder, true));
                ICaptureGraphBuilder2 builder = (ICaptureGraphBuilder2)builderObject;
                Check(builder.SetFiltergraph(graph), Strings.BuildCameraPipeline);
                Check(graph.AddFilter(camera, "Camera"), Strings.AddCamera);
                RequestFormat(builder, camera);
                if (stopping) return;
                grabObject = Activator.CreateInstance(Type.GetTypeFromCLSID(DsIds.SampleGrabber, true));
                grab = (ISampleGrabber)grabObject;
                IBaseFilter grabFilter = (IBaseFilter)grabObject;
                Check(graph.AddFilter(grabFilter, "Frame capture"), Strings.AddFrameCapture);
                AMMediaType requested = new AMMediaType();
                requested.majorType = DsIds.Video;
                requested.subType = DsIds.Rgb24;
                requested.formatType = DsIds.VideoInfo;
                Check(grab.SetMediaType(requested), Strings.SetFrameFormat);
                Check(grab.SetOneShot(false), Strings.SetContinuousCapture);
                Check(grab.SetBufferSamples(false), Strings.DisableBuffer);
                sinkObject = Activator.CreateInstance(Type.GetTypeFromCLSID(DsIds.NullRenderer, true));
                IBaseFilter sink = (IBaseFilter)sinkObject;
                Check(graph.AddFilter(sink, "Null renderer"), Strings.AddRenderer);
                Guid category = DsIds.Capture, video = DsIds.Video;
                Check(builder.RenderStream(ref category, ref video, camera, grabFilter, sink), Strings.ConnectCamera);
                AMMediaType connected = new AMMediaType();
                try
                {
                    Check(grab.GetConnectedMediaType(connected), Strings.ReadFrameFormat);
                    if (connected.formatPtr == IntPtr.Zero ||
                        connected.formatType != DsIds.VideoInfo || connected.formatSize < 88)
                        throw new InvalidOperationException(Strings.UnsupportedCameraFormat);
                    VideoInfoHeader info = (VideoInfoHeader)Marshal.PtrToStructure(connected.formatPtr, typeof(VideoInfoHeader));
                    width = info.bitmap.width;
                    height = Math.Abs(info.bitmap.height);
                    if (width < 1 || height < 1 || width > 4096 || height > 4096 || info.bitmap.bitCount != 24)
                        throw new InvalidOperationException(Strings.UnsupportedCameraImage);
                    stride = (width * 3 + 3) & ~3;
                    bottomUp = info.bitmap.height > 0;
                }
                finally { connected.Free(); }
                callback = new GrabberCallback(this);
                Check(grab.SetCallback(callback, 1), Strings.RegisterFrameCallback);
                if (stopping) return;
                Check(control.Run(), Strings.StartCapture);
                quit.WaitOne();
            }
            catch (Exception ex)
            {
                if (!stopping) Fail(Strings.CameraUnavailableDetail + ex.Message + Strings.SkipAfterCameraError);
            }
            finally
            {
                stopping = true;
                try { if (grab != null) grab.SetCallback(null, 1); } catch { }
                try { if (control != null) control.Stop(); } catch { }
                callback = null;
                // One RCW per COM object; do not release interface aliases twice.
                Release(sinkObject);
                Release(grabObject);
                Release(camera);
                Release(builderObject);
                Release(graphObject);
                if (initialized >= 0) CoUninitialize();
                // A slow driver may outlive Dispose's finite wait. Close the
                // wait handle here as well, exactly once, after native teardown.
                CloseQuitHandle();
            }
        }

        static IBaseFilter OpenCamera(int selected)
        {
            object devices = null;
            IEnumMoniker enumerator = null;
            IMoniker current = null;
            try
            {
                devices = Activator.CreateInstance(Type.GetTypeFromCLSID(DsIds.SystemDeviceEnum, true));
                Guid category = DsIds.VideoInputDevice;
                int hr = ((ICreateDevEnum)devices).CreateClassEnumerator(ref category, out enumerator, 0);
                if (hr != 0 || enumerator == null) throw new InvalidOperationException(Strings.CameraNotFound);
                IMoniker[] found = new IMoniker[1];
                int count = 0;
                while (enumerator.Next(1, found, IntPtr.Zero) == 0)
                {
                    current = found[0];
                    if (count++ == selected)
                    {
                        object result;
                        Guid iid = typeof(IBaseFilter).GUID;
                        current.BindToObject(null, null, ref iid, out result);
                        return (IBaseFilter)result;
                    }
                    Release(current); current = null;
                }
                throw new InvalidOperationException(Strings.CameraIndexNotFound + selected + ")");
            }
            finally { Release(current); Release(enumerator); Release(devices); }
        }

        static void RequestFormat(ICaptureGraphBuilder2 builder, IBaseFilter camera)
        {
            object result = null;
            try
            {
                Guid category = DsIds.Capture, video = DsIds.Video, iid = typeof(IAMStreamConfig).GUID;
                if (builder.FindInterface(ref category, ref video, camera, ref iid, out result) < 0 || result == null) return;
                IAMStreamConfig config = (IAMStreamConfig)result;
                AMMediaType format;
                if (config.GetFormat(out format) < 0 || format == null) return;
                try
                {
                    if (format.formatType != DsIds.VideoInfo || format.formatPtr == IntPtr.Zero || format.formatSize < 88) return;
                    VideoInfoHeader info = (VideoInfoHeader)Marshal.PtrToStructure(format.formatPtr, typeof(VideoInfoHeader));
                    info.averageFrameTime = 333333; // 30 fps, in 100-nanosecond units.
                    info.bitmap.width = 640;
                    info.bitmap.height = info.bitmap.height < 0 ? -480 : 480;
                    // Do not invent compressed format size fields. Negotiate only
                    // known raw RGB formats, otherwise keep the driver's default.
                    if (info.bitmap.compression == 0 && info.bitmap.bitCount > 0)
                    {
                        info.bitmap.sizeImage = (((640 * info.bitmap.bitCount + 31) / 32) * 4) * 480;
                        format.sampleSize = info.bitmap.sizeImage;
                        Marshal.StructureToPtr(info, format.formatPtr, false);
                        config.SetFormat(format); // Unsupported requests retain default format.
                    }
                }
                finally { format.Free(); }
            }
            catch { /* Default driver format is a valid fallback. */ }
            finally { Release(result); }
        }

        internal void OnBuffer(IntPtr buffer, int length)
        {
            if (stopping || buffer == IntPtr.Zero || length < stride * height) return;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now - Interlocked.Read(ref lastFrameTicks) < System.Diagnostics.Stopwatch.Frequency / 30) return;
            if (Interlocked.Exchange(ref copying, 1) != 0) return;
            Bitmap bitmap = null;
            try
            {
                if (stopping) return;
                Interlocked.Exchange(ref lastFrameTicks, now);
                bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                try
                {
                    for (int y = 0; y < height; y++)
                    {
                        int sourceY = bottomUp ? height - 1 - y : y;
                        CopyMemory(IntPtr.Add(data.Scan0, y * data.Stride),
                                   IntPtr.Add(buffer, sourceY * stride), new UIntPtr((uint)(width * 3)));
                    }
                }
                finally { bitmap.UnlockBits(data); }
                lock (sync)
                {
                    if (!stopping)
                    {
                        if (latest != null) latest.Dispose();
                        latest = bitmap; bitmap = null;
                    }
                }
            }
            catch (Exception ex) { if (!stopping) Fail(Strings.FrameReadFailed + ex.Message); }
            finally { if (bitmap != null) bitmap.Dispose(); Interlocked.Exchange(ref copying, 0); }
        }

        static void Check(int hr, string step) { if (hr < 0) throw new COMException(step + Strings.OperationFailed + hr.ToString("X8") + ")", hr); }
        static void Release(object value) { if (value != null && Marshal.IsComObject(value)) { try { Marshal.FinalReleaseComObject(value); } catch { } } }
        [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr reserved, uint flags);
        [DllImport("ole32.dll")] static extern void CoUninitialize();
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] static extern void CopyMemory(IntPtr destination, IntPtr source, UIntPtr length);
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class GrabberCallback : ISampleGrabberCB
    {
        readonly CameraCapture owner;
        internal GrabberCallback(CameraCapture owner) { this.owner = owner; }
        public int SampleCB(double sampleTime, IntPtr sample) { return 0; }
        public int BufferCB(double sampleTime, IntPtr buffer, int length) { owner.OnBuffer(buffer, length); return 0; }
    }

    internal static class DsIds
    {
        internal static readonly Guid FilterGraph = new Guid("E436EBB3-524F-11CE-9F53-0020AF0BA770");
        internal static readonly Guid CaptureGraphBuilder = new Guid("BF87B6E1-8C27-11D0-B3F0-00AA003761C5");
        internal static readonly Guid SystemDeviceEnum = new Guid("62BE5D10-60EB-11D0-BD3B-00A0C911CE86");
        internal static readonly Guid VideoInputDevice = new Guid("860BB310-5D01-11D0-BD3B-00A0C911CE86");
        internal static readonly Guid SampleGrabber = new Guid("C1F400A0-3F08-11D3-9F0B-006008039E37");
        internal static readonly Guid NullRenderer = new Guid("C1F400A4-3F08-11D3-9F0B-006008039E37");
        internal static readonly Guid Capture = new Guid("FB6C4281-0353-11D1-905F-0000C0CC16BA");
        internal static readonly Guid Video = new Guid("73646976-0000-0010-8000-00AA00389B71");
        internal static readonly Guid Rgb24 = new Guid("E436EB7D-524F-11CE-9F53-0020AF0BA770");
        internal static readonly Guid VideoInfo = new Guid("05589F80-C356-11CE-BF01-00AA0055595A");
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DsRect { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        public int size, width, height;
        public short planes, bitCount;
        public int compression, sizeImage, xPelsPerMeter, yPelsPerMeter, clrUsed, clrImportant;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct VideoInfoHeader
    {
        public DsRect source, target;
        public int bitRate, bitErrorRate;
        public long averageFrameTime;
        public BitmapInfoHeader bitmap;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class AMMediaType
    {
        public Guid majorType, subType;
        [MarshalAs(UnmanagedType.Bool)] public bool fixedSizeSamples;
        [MarshalAs(UnmanagedType.Bool)] public bool temporalCompression;
        public int sampleSize;
        public Guid formatType;
        public IntPtr unknown;
        public int formatSize;
        public IntPtr formatPtr;
        internal void Free()
        {
            if (formatPtr != IntPtr.Zero) { Marshal.FreeCoTaskMem(formatPtr); formatPtr = IntPtr.Zero; }
            if (unknown != IntPtr.Zero) { Marshal.Release(unknown); unknown = IntPtr.Zero; }
        }
    }

    [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICreateDevEnum
    {
        [PreserveSig] int CreateClassEnumerator(ref Guid category, out IEnumMoniker enumerator, int flags);
    }

    [ComImport, Guid("56A86895-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IBaseFilter
    {
        [PreserveSig] int GetClassID(out Guid classID);
        [PreserveSig] int Stop();
        [PreserveSig] int Pause();
        [PreserveSig] int Run(long start);
        [PreserveSig] int GetState(int timeout, out int state);
        [PreserveSig] int SetSyncSource([MarshalAs(UnmanagedType.Interface)] object clock);
        [PreserveSig] int GetSyncSource([MarshalAs(UnmanagedType.Interface)] out object clock);
        [PreserveSig] int EnumPins(out IntPtr enumerator);
        [PreserveSig] int FindPin([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr pin);
        [PreserveSig] int QueryFilterInfo(IntPtr info);
        [PreserveSig] int JoinFilterGraph([MarshalAs(UnmanagedType.Interface)] object graph, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int QueryVendorInfo([MarshalAs(UnmanagedType.LPWStr)] out string vendor);
    }

    [ComImport, Guid("56A868A9-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IGraphBuilder
    {
        [PreserveSig] int AddFilter(IBaseFilter filter, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int RemoveFilter(IBaseFilter filter);
        [PreserveSig] int EnumFilters(out IntPtr enumerator);
        [PreserveSig] int FindFilterByName([MarshalAs(UnmanagedType.LPWStr)] string name, out IBaseFilter filter);
        [PreserveSig] int ConnectDirect(IntPtr outputPin, IntPtr inputPin, AMMediaType type);
        [PreserveSig] int Reconnect(IntPtr pin);
        [PreserveSig] int Disconnect(IntPtr pin);
        [PreserveSig] int SetDefaultSyncSource();
        [PreserveSig] int Connect(IntPtr outputPin, IntPtr inputPin);
        [PreserveSig] int Render(IntPtr outputPin);
        [PreserveSig] int RenderFile([MarshalAs(UnmanagedType.LPWStr)] string file, [MarshalAs(UnmanagedType.LPWStr)] string playlist);
        [PreserveSig] int AddSourceFilter([MarshalAs(UnmanagedType.LPWStr)] string file, [MarshalAs(UnmanagedType.LPWStr)] string name, out IBaseFilter filter);
        [PreserveSig] int SetLogFile(IntPtr file);
        [PreserveSig] int Abort();
        [PreserveSig] int ShouldOperationContinue();
    }

    [ComImport, Guid("93E5A4E0-2D50-11D2-ABFA-00A0C9C6E38D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICaptureGraphBuilder2
    {
        [PreserveSig] int SetFiltergraph(IGraphBuilder graph);
        [PreserveSig] int GetFiltergraph(out IGraphBuilder graph);
        [PreserveSig] int SetOutputFileName(ref Guid type, [MarshalAs(UnmanagedType.LPWStr)] string name, out IBaseFilter filter, out IntPtr sink);
        [PreserveSig] int FindInterface(ref Guid category, ref Guid mediaType, IBaseFilter filter, ref Guid iid,
            [MarshalAs(UnmanagedType.IUnknown)] out object result);
        [PreserveSig] int RenderStream(ref Guid category, ref Guid mediaType,
            [MarshalAs(UnmanagedType.IUnknown)] object source, IBaseFilter intermediate, IBaseFilter renderer);
        [PreserveSig] int ControlStream(ref Guid category, ref Guid mediaType, IBaseFilter filter,
            IntPtr start, IntPtr stop, short startCookie, short stopCookie);
        [PreserveSig] int AllocCapFile([MarshalAs(UnmanagedType.LPWStr)] string file, long size);
        [PreserveSig] int CopyCaptureFile([MarshalAs(UnmanagedType.LPWStr)] string oldFile,
            [MarshalAs(UnmanagedType.LPWStr)] string newFile, int allowEscAbort, [MarshalAs(UnmanagedType.IUnknown)] object callback);
        [PreserveSig] int FindPin([MarshalAs(UnmanagedType.IUnknown)] object source, int direction,
            ref Guid category, ref Guid mediaType, [MarshalAs(UnmanagedType.Bool)] bool unconnected,
            int number, out IntPtr pin);
    }

    [ComImport, Guid("56A868B1-0AD4-11CE-B03A-0020AF0BA770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    internal interface IMediaControl
    {
        [PreserveSig] int Run();
        [PreserveSig] int Pause();
        [PreserveSig] int Stop();
        [PreserveSig] int GetState(int timeout, out int state);
        [PreserveSig] int RenderFile([MarshalAs(UnmanagedType.BStr)] string file);
        [PreserveSig] int AddSourceFilter([MarshalAs(UnmanagedType.BStr)] string file, [MarshalAs(UnmanagedType.IDispatch)] out object filter);
        [PreserveSig] int get_FilterCollection([MarshalAs(UnmanagedType.IDispatch)] out object collection);
        [PreserveSig] int get_RegFilterCollection([MarshalAs(UnmanagedType.IDispatch)] out object collection);
        [PreserveSig] int StopWhenReady();
    }

    [ComImport, Guid("C6E13340-30AC-11D0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAMStreamConfig
    {
        [PreserveSig] int SetFormat([In] AMMediaType type);
        [PreserveSig] int GetFormat([Out] out AMMediaType type);
        [PreserveSig] int GetNumberOfCapabilities(out int count, out int size);
        [PreserveSig] int GetStreamCaps(int index, [Out] out AMMediaType type, IntPtr caps);
    }

    [ComImport, Guid("6B652FFF-11FE-4FCE-92AD-0266B5D7C78F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISampleGrabber
    {
        [PreserveSig] int SetOneShot([MarshalAs(UnmanagedType.Bool)] bool oneShot);
        [PreserveSig] int SetMediaType([In] AMMediaType type);
        [PreserveSig] int GetConnectedMediaType([In, Out] AMMediaType type);
        [PreserveSig] int SetBufferSamples([MarshalAs(UnmanagedType.Bool)] bool buffer);
        [PreserveSig] int GetCurrentBuffer(ref int size, IntPtr buffer);
        [PreserveSig] int GetCurrentSample(out IntPtr sample);
        [PreserveSig] int SetCallback(ISampleGrabberCB callback, int kind);
    }

    [ComVisible(true), Guid("0579154A-2B53-4994-B0D0-E773148EFF85"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISampleGrabberCB
    {
        [PreserveSig] int SampleCB(double sampleTime, IntPtr sample);
        [PreserveSig] int BufferCB(double sampleTime, IntPtr buffer, int length);
    }
}
