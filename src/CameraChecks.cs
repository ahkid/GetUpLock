using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace SquatLockTiny
{
    internal static class CameraChecks
    {
        // 仅显式诊断入口读取真实摄像头；不保存图片、视频或个人积分。
        internal static int Run(string output)
        {
            Directory.CreateDirectory(output);
            Dictionary<string,object> report = new Dictionary<string,object>();
            Config config = Config.Load(Program.AppDir,null,Path.Combine(output,"isolated-state.json"));
            int frames = 0, poses = 0;
            long first = -1, last = -1;
            Stopwatch clock = Stopwatch.StartNew();
            CameraPipeline pipeline = new CameraPipeline(config.Int("camera_index",0),Path.Combine(Program.AppDir,"assets","pose_landmarker_lite.task"),false);
            try
            {
                pipeline.Start();
                while (clock.ElapsedMilliseconds < 10000)
                {
                    using (FramePacket packet = pipeline.TakeLatest())
                    {
                        if (packet != null)
                        {
                            frames++; if (first < 0) first = clock.ElapsedMilliseconds; last = clock.ElapsedMilliseconds;
                            if (packet.Landmarks != null) poses++;
                        }
                    }
                    if (!String.IsNullOrEmpty(pipeline.Error)) throw new IOException(pipeline.Error);
                    Thread.Sleep(5);
                }
                report["ok"] = frames >= 2;
                report["frames"] = frames; report["pose_frames"] = poses;
                report["first_frame_ms"] = first;
                report["processed_fps"] = frames > 1 && last > first ? Math.Round((frames-1)*1000.0/(last-first),2) : 0;
                report["images_saved"] = false; report["network_used"] = false;
            }
            catch (Exception error) { report["ok"] = false; report["error"] = error.ToString(); Program.Log(error.ToString()); }
            finally
            {
                pipeline.Stop(); report["worker_closed"] = pipeline.Wait(10000); pipeline.Dispose();
            }
            File.WriteAllText(Path.Combine(output,"camera-result.json"),new JavaScriptSerializer().Serialize(report));
            return Convert.ToBoolean(report["ok"]) && Convert.ToBoolean(report["worker_closed"]) ? 0 : 1;
        }
    }
}
