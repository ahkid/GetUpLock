using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SquatLockTiny
{
    internal static class SmokeTests
    {
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
        }

        private sealed class PosterServer : HttpMessageHandler
        {
            internal int Requests;
            internal Task<HttpResponseMessage> Response;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Check(request.Method == HttpMethod.Get && request.RequestUri.Scheme == "https" &&
                    request.RequestUri.Host == "ads.xjwisdomsoft.cn" &&
                    Regex.IsMatch(request.RequestUri.PathAndQuery, @"^/ads/ad([1-9]|10)" + Edition.PosterSuffix + @"\.jpg\?t=[0-9]+$"),
                    "Unexpected poster request");
                Requests++;
                if (Response != null) return Response;
                using (var image = new Bitmap(16, 24))
                using (var bytes = new MemoryStream())
                {
                    image.Save(bytes, System.Drawing.Imaging.ImageFormat.Jpeg);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content = new ByteArrayContent(bytes.ToArray())});
                }
            }
        }

        private static void CheckPlaceholder(PictureBox box, string screenshot)
        {
            Check(box.Visible && box.Image == null && box.Width > 50 && box.Height > 50, "Missing poster area");
            using (var bitmap = new Bitmap(box.Width, box.Height))
            {
                box.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                int orange = 0;
                for (int y = 0; y < bitmap.Height; y += 2)
                    for (int x = 0; x < bitmap.Width; x += 2)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        if (pixel.R > 200 && pixel.G > 95 && pixel.G < 190 && pixel.B < 70) orange++;
                    }
                Check(orange > 12, "The offline poster did not paint its exercise icon");
                if (screenshot != null) bitmap.Save(screenshot);
            }
        }

        private static void CheckPosterWindow(Config config, StateStore store, string output)
        {
            var pending = new TaskCompletionSource<HttpResponseMessage>();
            var handler = new PosterServer { Response = pending.Task };
            using (var ads = new StaticAds(handler))
            using (var window = new LockForm(config, new ScoreBoard(store), new SkipPolicy(store, 1), true, ads))
            {
                window.StartGroup();
                var box = (PictureBox)window.Controls.Find("AdPoster", true).Single();
                CheckPlaceholder(box, Path.Combine(output, "poster-offline.png"));
                using (var image = new Bitmap(60, 100))
                using (var bytes = new MemoryStream())
                {
                    using (Graphics g = Graphics.FromImage(image)) g.Clear(Color.SteelBlue);
                    image.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
                    pending.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes.ToArray()) });
                }
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                while (box.Image == null && timeout.ElapsedMilliseconds < 4000)
                {
                    Application.DoEvents();
                    Thread.Sleep(5);
                }
                Check(box.Image != null, "The downloaded poster did not replace the placeholder");
                using (var rendered = new Bitmap(box.Width, box.Height))
                {
                    box.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size));
                    Check(rendered.GetPixel(box.Width/2, box.Height/2).ToArgb() == Color.SteelBlue.ToArgb(), "Poster was not painted");
                }
                window.AbortSession();
                handler.Response = Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                window.StartGroup();
                Application.DoEvents();
                CheckPlaceholder(box, null);
                window.AbortSession();
                Check(handler.Requests == 2, "Each group should request one poster");
            }
        }

        internal static int Run(string output)
        {
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                Check(process.ProcessName == "GetUpLock", "Unexpected Windows process name");
                Check(process.MainModule.FileVersionInfo.FileDescription == "GetUpLock", "Unexpected Windows process description");
            }
            Directory.CreateDirectory(output);
            var store = new StateStore(Path.Combine(output, Guid.NewGuid().ToString("N") + ".json"));
            Config config = Config.Load(Program.AppDir, null, store.Path);
            config.SaveCameraEnabled(false);
            CheckPosterWindow(config, store, output);
            using (var app = new TrayApplication(config, store, false, true))
            {
                var entries = app.Menu.Items.Cast<ToolStripItem>().Select(x => x.Text).ToList();
#if ENGLISH
                Check(entries.Count == 8 && entries[0] == "Start now" && entries.Contains("Feedback") && entries.Contains("Check for updates"), "Unexpected English tray menu");
#else
                Check(entries.Count == 8 && entries[0] == "现在就做" && entries.Contains("客户反馈") && entries.Contains("检查更新"), "Unexpected Chinese tray menu");
#endif
                string file = "-Setup-" + Program.Version + "-" + Program.BuildNumber + ".exe";
                string zhPackage = Edition.Cdn + "/updates/windows/GetUpLockPublic" + file;
                string enPackage = Edition.Cdn + "/updates/windows/en/GetUpLockPublicEN" + file;
#if ENGLISH
                Check(UpdateInfo.ValidDownloadUrl(enPackage) && !UpdateInfo.ValidDownloadUrl(zhPackage), "Wrong English update channel");
#else
                Check(UpdateInfo.ValidDownloadUrl(zhPackage) && !UpdateInfo.ValidDownloadUrl(enPackage), "Wrong Chinese update channel");
#endif
                app.StartGroup();
                Check(app.Window.Active && app.Window.ManualSession, "Manual session did not start");
                using (var preview = new Bitmap(app.Window.Width, app.Window.Height))
                {
                    app.Window.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
                    preview.Save(Path.Combine(output, "break-window.png"));
                }
                Check(app.Window.AbortSession(), "Manual session did not finish");
            }
#if ENGLISH
            var browser = Edition.FeedbackRequest();
            Check(browser.UseShellExecute && browser.FileName == "https://tally.so/r/VpQzxg", "English feedback destination");
#else
            using (var feedback = new FeedbackDialog())
            {
                var image = feedback.Controls.OfType<PictureBox>().Single().Image;
                Check(image.Width == image.Height && image.Width >= 300, "Missing feedback code");
                feedback.Show();
                using (var view = new Bitmap(feedback.Width, feedback.Height))
                {
                    feedback.DrawToBitmap(view, new Rectangle(Point.Empty, view.Size));
                    view.Save(Path.Combine(output, "feedback.png"));
                }
                feedback.Close();
            }
#endif
            var handler = new PosterServer();
            using (var ads = new StaticAds(handler))
            {
                for (int i = 0; i < 20; i++)
                    using (var image = ads.LoadAsync().GetAwaiter().GetResult())
                        Check(image.Width == 16, "Could not load a poster");
                Check(handler.Requests == 20, "Unexpected requests per poster");
            }
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS");
            return 0;
        }
    }
}
