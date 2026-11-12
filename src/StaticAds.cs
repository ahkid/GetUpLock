using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SquatLockTiny
{
    internal sealed class StaticAds : IDisposable
    {
        private static readonly Uri AdDirectory = new Uri(Edition.Cdn + "/ads/");
        private readonly HttpClient client;
        private readonly Random random = new Random();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

        internal StaticAds() : this(new HttpClientHandler { AllowAutoRedirect=false, UseCookies=false }) {}
        internal StaticAds(HttpMessageHandler handler)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            client = new HttpClient(handler);
        }

        private async Task<byte[]> GetBytesAsync(Uri url, int limit)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(6));
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException();
                    using (deadline.Token.Register(response.Dispose))
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var buffer = new MemoryStream())
                    {
                        var chunk = new byte[8192];
                        int n;
                        while ((n = await stream.ReadAsync(chunk, 0, chunk.Length, deadline.Token).ConfigureAwait(false)) > 0)
                        {
                            if (buffer.Length + n > limit) throw new InvalidDataException();
                            buffer.Write(chunk, 0, n);
                        }
                        return buffer.ToArray();
                    }
                }
            }
        }

        internal async Task<Image> LoadAsync()
        {
            int slot = random.Next(1, 11);
            string stamp = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            var url = new Uri(AdDirectory, "ad" + slot.ToString(CultureInfo.InvariantCulture) + Edition.PosterSuffix + ".jpg?t=" + stamp);
            byte[] data = await GetBytesAsync(url, 4 * 1024 * 1024).ConfigureAwait(false);
            using (var stream = new MemoryStream(data))
            using (var image = Image.FromStream(stream))
            {
                if (image.Width > 4096 || image.Height > 4096) throw new InvalidDataException();
                return new Bitmap(image);
            }
        }

        public void Dispose()
        {
            lifetime.Cancel();
            client.Dispose();
            lifetime.Dispose();
        }
    }
}
