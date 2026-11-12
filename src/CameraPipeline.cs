using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace SquatLockTiny
{
    internal sealed class FramePacket : IDisposable
    {
        public Bitmap Image;
        public Landmark[] Landmarks;
        public void Dispose() { if (Image != null) Image.Dispose(); Image = null; }
    }

    internal sealed class CameraPipeline : IDisposable
    {
        private readonly Thread worker;
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        private readonly object sync = new object();
        private readonly int cameraIndex;
        private readonly string modelPath;
        private readonly bool synthetic;
        private readonly double minVisibility;
        private volatile CameraCapture captureOwner;
        private FramePacket latest;
        private string error = "";
        private bool disposed;
        public bool IsAlive { get { CameraCapture capture = captureOwner; return worker.IsAlive || (capture != null && capture.IsAlive); } }
        public string Error { get { lock (sync) return error; } }

        public CameraPipeline(int cameraIndex, string modelPath, bool synthetic, double minVisibility=.5)
        {
            this.cameraIndex = cameraIndex;
            this.modelPath = modelPath;
            this.synthetic = synthetic;
            this.minVisibility = minVisibility;
            worker = new Thread(Run) { IsBackground = true, Name = "GetUpLockCamera" };
            worker.SetApartmentState(ApartmentState.MTA);
        }
        public void Start() { worker.Start(); }
        public void Stop()
        {
            lock (sync)
            {
                if (disposed) return;
                stop.Set();
                if (latest != null) latest.Dispose(); latest = null;
            }
            // Stop capture immediately, even if inference is still finishing a frame.
            CameraCapture capture=captureOwner;if(capture!=null)capture.Stop();
        }
        public void Dispose()
        {
            Stop();
            if (IsAlive) return; // 下一次开始前释放；UI 不等待摄像头驱动。
            if (captureOwner != null) captureOwner.Dispose();
            lock (sync) { if (!disposed) { disposed = true; stop.Dispose(); } }
        }
        public bool Wait(int milliseconds)
        {
            Stopwatch watch = Stopwatch.StartNew();
            if (!worker.Join(milliseconds)) return false;
            CameraCapture capture = captureOwner;
            return capture == null || capture.WaitForExit(Math.Max(0,milliseconds-(int)watch.ElapsedMilliseconds));
        }
        public FramePacket TakeLatest()
        {
            lock (sync) { FramePacket packet = latest; latest = null; return packet; }
        }
        private void Run()
        {
            CameraCapture capture = null;
            PoseDetector detector = null;
            try
            {
                if (stop.WaitOne(0)) return;
                if (!synthetic) { capture = new CameraCapture(cameraIndex); captureOwner = capture; }
                if (stop.WaitOne(0)) return;
                detector = new PoseDetector(modelPath);
                Stopwatch clock = Stopwatch.StartNew();
                while (!stop.WaitOne(0))
                {
                    long started = clock.ElapsedMilliseconds;
                    Bitmap source = synthetic ? new Bitmap(640, 480, PixelFormat.Format24bppRgb) : capture.TakeLatest();
                    if (source == null)
                    {
                        if (!String.IsNullOrEmpty(capture.Error)) throw new IOException(capture.Error);
                        stop.WaitOne(10);
                        continue;
                    }
                    Bitmap frame = null;
                    try
                    {
                        using (source) frame = PrepareFrame(source);
                        byte[] rgb = RgbBytes(frame);
                        Landmark[] landmarks = detector.Detect(rgb, frame.Width, frame.Height, clock.ElapsedMilliseconds);
                        if (stop.WaitOne(0)) break;
                        DrawSkeleton(frame, landmarks, minVisibility);
                        FramePacket packet = new FramePacket { Image = frame, Landmarks = landmarks };
                        frame = null;
                        lock (sync)
                        {
                            if (stop.WaitOne(0)) packet.Dispose();
                            else { if (latest != null) latest.Dispose(); latest = packet; }
                        }
                    }
                    finally { if (frame != null) frame.Dispose(); }
                    stop.WaitOne((int)Math.Max(0, 33 - (clock.ElapsedMilliseconds - started)));
                }
            }
            catch (Exception exception)
            {
                Program.Log(Strings.CameraThread + exception);
                lock (sync) error = Strings.CameraPermissions;
            }
            finally
            {
                if (capture != null) try { capture.Dispose(); } catch (Exception ex) { Program.Log(ex.ToString()); }
                if (detector != null) try { detector.Dispose(); } catch (Exception ex) { Program.Log(ex.ToString()); }
            }
        }

        internal static Bitmap PrepareFrame(Bitmap source)
        {
            double scale = Math.Min(1, Math.Min(640.0 / source.Width, 480.0 / source.Height));
            Bitmap frame = new Bitmap(Math.Max(1, (int)Math.Round(source.Width * scale)),
                Math.Max(1, (int)Math.Round(source.Height * scale)), PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(frame))
            {
                graphics.InterpolationMode = InterpolationMode.Bilinear;
                graphics.DrawImage(source, new Rectangle(0, 0, frame.Width, frame.Height));
            }
            frame.RotateFlip(RotateFlipType.RotateNoneFlipX);
            return frame;
        }

        internal static byte[] RgbBytes(Bitmap bitmap)
        {
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] rgb = new byte[bitmap.Width * bitmap.Height * 3];
                byte[] row = new byte[bitmap.Width * 3];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    int offset = y * row.Length;
                    for (int x = 0; x < row.Length; x += 3)
                    {
                        rgb[offset + x] = row[x + 2];
                        rgb[offset + x + 1] = row[x + 1];
                        rgb[offset + x + 2] = row[x];
                    }
                }
                return rgb;
            }
            finally { bitmap.UnlockBits(data); }
        }

        private static bool Tracked(Landmark p,double visibility)
        { return p!=null && p.Visibility>=visibility && !Double.IsNaN(p.X) && !Double.IsNaN(p.Y) && !Double.IsInfinity(p.X) && !Double.IsInfinity(p.Y); }

        private static readonly int[,] Connections = { {11,12}, {11,23}, {12,24}, {23,24}, {23,25}, {25,27}, {24,26}, {26,28} };
        internal static void DrawSkeleton(Bitmap frame, Landmark[] points, double minVisibility=.5)
        {
            if (points == null || points.Length < 33) return;
            using (Graphics graphics = Graphics.FromImage(frame))
            using (Pen pen = new Pen(Color.Lime, 4))
            {
                for (int i = 0; i < Connections.GetLength(0); i++)
                {
                    Landmark a = points[Connections[i, 0]], b = points[Connections[i, 1]];
                    if (!Tracked(a,minVisibility) || !Tracked(b,minVisibility)) continue;
                    graphics.DrawLine(pen, (float)(a.X * frame.Width), (float)(a.Y * frame.Height),
                        (float)(b.X * frame.Width), (float)(b.Y * frame.Height));
                }
                foreach (int i in new int[] { 0,11,12,23,24,25,26,27,28 })
                    if (Tracked(points[i],minVisibility)) graphics.FillEllipse(Brushes.Red, (float)(points[i].X * frame.Width) - 6,
                        (float)(points[i].Y * frame.Height) - 6, 12, 12);
            }
        }
    }
}
