using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace SquatLockTiny
{
    internal sealed class UpdateException : Exception
    {internal UpdateException(string text):base(text){}}
    internal sealed class UpdatePackage
    {
        internal static readonly string[] RequiredPaths={"GetUpLock.exe.config","libmediapipe.dll","assets/alert.wav","assets/pose_landmarker_lite.task","licenses/MediaPipe-LICENSE.txt","licenses/MediaPipe-NOTICE.txt"};
        internal readonly string Url,Sha256;
        internal readonly long Size;
        internal readonly Dictionary<string,string> Required;
        internal bool Incremental {get{return Required!=null;}}
        internal UpdatePackage(string url,string hash,long size,Dictionary<string,string> required=null){Url=url;Sha256=hash;Size=size;Required=required;}
        internal static UpdatePackage Parse(Dictionary<string,object> data,bool incremental,long build)
        {
            if(data==null)throw new UpdateException(Strings.InvalidPackage);
            string url=Value.Get(data,"download_url") as string,hash=Value.Get(data,"sha256") as string;
            long size;string sizeText=Convert.ToString(Value.Get(data,"size_bytes"),CultureInfo.InvariantCulture);
            if(hash==null||!Regex.IsMatch(hash,@"^[a-f0-9]{64}\z")||!Regex.IsMatch(sizeText,@"^[0-9]{1,8}\z")||!Int64.TryParse(sizeText,out size)||size<=0||size>=99000000)
                throw new UpdateException(Strings.InvalidChecksumInfo);
            if(incremental?url!=Edition.Cdn+Edition.UpdateDirectory+Edition.Id+"-Update-"+build+".exe":!UpdateInfo.ValidDownloadUrl(url))
                throw new UpdateException(Strings.InvalidPackageUrl);
            Dictionary<string,string> required=null;
            if(incremental)
            {
                var raw=Value.Get(data,"required_files") as Dictionary<string,object>;
                if(raw==null||raw.Count!=RequiredPaths.Length)throw new UpdateException(Strings.InvalidIncremental);
                required=new Dictionary<string,string>();
                foreach(string path in RequiredPaths)
                {
                    string digest=Value.Get(raw,path) as string;
                    if(digest==null||!Regex.IsMatch(digest,@"^[a-f0-9]{64}\z"))throw new UpdateException(Strings.InvalidIncremental);
                    required.Add(path,digest);
                }
            }
            return new UpdatePackage(url,hash,size,required);
        }
        internal static string HashFile(string path)
        {using(var input=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();}
        internal bool Matches(string directory)
        {
            if(!Incremental)return true;
            try{foreach(var pair in Required)if(HashFile(Path.Combine(directory,pair.Key))!=pair.Value)return false;return true;}
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
        }
    }
    internal sealed class UpdateInfo
    {
        internal readonly string Version,Notes,DownloadUrl;
        internal readonly long Build;
        internal UpdatePackage FullPackage,IncrementalPackage;
        private UpdateInfo(string version,long build,string notes,string url){Version=version;Build=build;Notes=notes;DownloadUrl=url;}
        internal static System.Version ParseVersion(string value)
        {
            if(value==null||!Regex.IsMatch(value,@"^[0-9]{1,6}(\.[0-9]{1,6}){1,3}\z"))throw new UpdateException(Strings.InvalidVersionInfo);
            var pieces=value.Split('.');int[] parts=new int[4];for(int i=0;i<pieces.Length;i++)parts[i]=Int32.Parse(pieces[i],CultureInfo.InvariantCulture);
            return new System.Version(parts[0],parts[1],parts[2],parts[3]);
        }
        internal bool IsNewer(string current,long build)
        {int comparison=ParseVersion(Version).CompareTo(ParseVersion(current));return comparison>0||(comparison==0&&Build>build);}
        internal static UpdateInfo Parse(string json)
        {
            if(json==null||Encoding.UTF8.GetByteCount(json)>UpdateService.MaxBytes)throw new UpdateException(Strings.ManifestTooLarge);
            Dictionary<string,object> data;
            try{data=new JavaScriptSerializer{MaxJsonLength=UpdateService.MaxBytes,RecursionLimit=8}.DeserializeObject(json) as Dictionary<string,object>;}
            catch{throw new UpdateException(Strings.InvalidVersionInfo);}
            if(data==null||!Object.Equals(Value.Get(data,"ok"),true))throw new UpdateException(Strings.ReleaseMissing);
            string version=Value.Get(data,"latest_version") as string;ParseVersion(version);
            long build;string buildText=Convert.ToString(Value.Get(data,"build",0),CultureInfo.InvariantCulture);
            if(!Regex.IsMatch(buildText,@"^[0-9]{1,16}\z")||!Int64.TryParse(buildText,out build))throw new UpdateException(Strings.InvalidBuild);
            string url=Value.Get(data,"download_url") as string;
            if(!ValidDownloadUrl(url))throw new UpdateException(Strings.MissingDownload);
            object raw=Value.Get(data,"notes","");if(!(raw is string)||((string)raw).Length>1200)throw new UpdateException(Strings.InvalidNotes);
            string notes=(string)raw;
            foreach(char c in notes)if(Char.IsControl(c)&&c!='\n'&&c!='\r'&&c!='\t')throw new UpdateException(Strings.InvalidNotes);
            var info=new UpdateInfo(version,build,notes.Trim(),url);
            if(Object.Equals(Value.Get(data,"in_app_update"),1))
            {
                info.FullPackage=UpdatePackage.Parse(data,false,build);
                if(data.ContainsKey("incremental"))info.IncrementalPackage=UpdatePackage.Parse(Value.Get(data,"incremental") as Dictionary<string,object>,true,build);
            }
            return info;
        }
        internal UpdatePackage SelectPackage(string directory)
        {return IncrementalPackage!=null&&IncrementalPackage.Matches(directory)?IncrementalPackage:FullPackage;}
        internal static bool ValidDownloadUrl(string url)
        {
            if(url!=null)foreach(char c in url)if(Char.IsWhiteSpace(c)||Char.IsControl(c))return false;
            Uri uri;
            return url!=null&&url.Length<=512&&url.IndexOf('%')<0&&Uri.TryCreate(url,UriKind.Absolute,out uri)&&
                uri.Scheme==Uri.UriSchemeHttps&&String.Equals(uri.Host,"ads.xjwisdomsoft.cn",StringComparison.OrdinalIgnoreCase)&&
                uri.Port==443&&uri.UserInfo.Length==0&&uri.Fragment.Length==0&&
                Regex.IsMatch(uri.AbsolutePath,"^"+Regex.Escape(Edition.UpdateDirectory+Edition.Id)+@"-Setup-[0-9]+(\.[0-9]+){1,3}-[0-9]{1,16}\.exe\z")&&
                (uri.Query.Length==0||Regex.IsMatch(uri.Query,@"^\?build=[0-9]{1,16}\z"));
        }
    }
    internal sealed class UpdateService : IDisposable
    {
        internal const int MaxBytes=32768;
        internal static readonly Uri Endpoint=new Uri(Edition.Cdn+Edition.UpdateDirectory+"release.json");
        private readonly HttpClient client;
        private readonly CancellationTokenSource shutdown=new CancellationTokenSource();
        private readonly CancellationToken stop;
        private readonly int timeoutMs;
        private bool disposed;
        internal UpdateService():this(CreateHandler(),8000){}
        internal UpdateService(HttpMessageHandler handler,int timeoutMs)
        {
            this.timeoutMs=timeoutMs;stop=shutdown.Token;client=new HttpClient(handler,true);client.Timeout=Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Edition.Id);
        }
        internal static HttpClientHandler CreateHandler()
        {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            return new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,UseDefaultCredentials=false,Credentials=null,
                AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate};
        }
        internal async Task<UpdateInfo> CheckAsync()
        {
            stop.ThrowIfCancellationRequested();
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop))
            {
                deadline.CancelAfter(timeoutMs);
                var work=Task.Run(delegate{return FetchAsync(deadline.Token);},deadline.Token);
                try
                {
                    Task finished=await Task.WhenAny(work,Task.Delay(timeoutMs,stop)).ConfigureAwait(false);
                    if(finished!=work)
                    {
                        deadline.Cancel();ObserveFailure(work);
                        stop.ThrowIfCancellationRequested();throw new UpdateException(Strings.UpdateTimeout);
                    }
                    var result=await work.ConfigureAwait(false);stop.ThrowIfCancellationRequested();return result;
                }
                catch(OperationCanceledException){stop.ThrowIfCancellationRequested();throw new UpdateException(Strings.UpdateTimeout);}
                catch(UpdateException){throw;}
                catch{stop.ThrowIfCancellationRequested();throw new UpdateException(Strings.ReleaseUnreachable);}
            }
        }
        private static void ObserveFailure(Task work)
        {work.ContinueWith(delegate(Task failed){var ignored=failed.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);}
        internal async Task<string> DownloadAsync(UpdatePackage package,string directory,IProgress<int> progress,CancellationToken cancellation,int downloadTimeoutMs=300000)
        {
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop,cancellation))
            {
                deadline.CancelAfter(downloadTimeoutMs);
                var work=Task.Run(delegate{return DownloadFileAsync(package,directory,progress,deadline.Token);},deadline.Token);
                var finished=await Task.WhenAny(work,Task.Delay(downloadTimeoutMs,deadline.Token)).ConfigureAwait(false);
                if(finished!=work){deadline.Cancel();ObserveFailure(work);cancellation.ThrowIfCancellationRequested();stop.ThrowIfCancellationRequested();throw new UpdateException(Strings.DownloadTimeout);}
                try{return await work.ConfigureAwait(false);}
                catch(OperationCanceledException){cancellation.ThrowIfCancellationRequested();stop.ThrowIfCancellationRequested();throw new UpdateException(Strings.DownloadTimeout);}
                catch{cancellation.ThrowIfCancellationRequested();stop.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested)throw new UpdateException(Strings.DownloadTimeout);throw;}
            }
        }
        private async Task<string> DownloadFileAsync(UpdatePackage package,string directory,IProgress<int> progress,CancellationToken cancellation)
        {
            Directory.CreateDirectory(directory);
            string partial=Path.Combine(directory,"package.part"),ready=Path.Combine(directory,"package.exe");
            try
            {
                using(var request=new HttpRequestMessage(HttpMethod.Get,package.Url))
                using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellation).ConfigureAwait(false))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if(response.StatusCode!=HttpStatusCode.OK||response.Content==null)throw new UpdateException(Strings.PackageFailed);
                    long? advertised=response.Content.Headers.ContentLength;
                    if(advertised.HasValue&&advertised.Value!=package.Size)throw new UpdateException(Strings.PackageSizeMismatch);
                    using(cancellation.Register(delegate{response.Dispose();}))
                    using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {
                        byte[] buffer=new byte[65536];long total=0;int count;
                        while((count=await input.ReadAsync(buffer,0,buffer.Length,cancellation).ConfigureAwait(false))>0)
                        {
                            total+=count;if(total>package.Size)throw new UpdateException(Strings.PackageSizeMismatch);
                            await output.WriteAsync(buffer,0,count,cancellation).ConfigureAwait(false);
                            if(progress!=null)progress.Report((int)(total*100/package.Size));
                        }
                        if(total!=package.Size)throw new UpdateException(Strings.IncompletePackage);
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                if(UpdatePackage.HashFile(partial)!=package.Sha256)throw new UpdateException(Strings.ChecksumFailed);
                cancellation.ThrowIfCancellationRequested();File.Move(partial,ready);return ready;
            }
            catch{if(File.Exists(partial))File.Delete(partial);throw;}
        }
        private async Task<UpdateInfo> FetchAsync(CancellationToken cancellation)
        {
            using(var request=new HttpRequestMessage(HttpMethod.Get,Endpoint))
            {
                request.Headers.CacheControl=new System.Net.Http.Headers.CacheControlHeaderValue{NoCache=true,NoStore=true};
                request.Headers.Accept.ParseAdd("application/json");
                using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellation).ConfigureAwait(false))
                {
                    if(response.StatusCode!=HttpStatusCode.OK)throw new UpdateException(Strings.ReleaseUnavailable);
                    if(response.Content==null||response.Content.Headers.ContentLength>MaxBytes)throw new UpdateException(Strings.ManifestTooLarge);
                    using(cancellation.Register(delegate{response.Dispose();}))
                    using(var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using(var output=new MemoryStream())
                    {
                        byte[] buffer=new byte[4096];int read;
                        while((read=await input.ReadAsync(buffer,0,buffer.Length,cancellation).ConfigureAwait(false))>0)
                        {
                            if(output.Length+read>MaxBytes)throw new UpdateException(Strings.ManifestTooLarge);output.Write(buffer,0,read);
                        }
                        return UpdateInfo.Parse(new UTF8Encoding(false,true).GetString(output.ToArray()));
                    }
                }
            }
        }
        public void Dispose(){if(disposed)return;disposed=true;shutdown.Cancel();client.Dispose();shutdown.Dispose();}
    }
}
