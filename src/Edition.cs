using System.Diagnostics;

namespace SquatLockTiny
{
    internal static class Edition
    {
        internal const string Cdn = "https://ads.xjwisdomsoft.cn";
#if ENGLISH
        internal const string CultureName = "en-US";
        internal const string Id = "GetUpLockPublicEN";
        internal const string UpdateDirectory = "/updates/windows/en/";
        internal const string PosterSuffix = "en";
        internal const string FontFamily = "Segoe UI";
        internal const string InstanceMutex = @"Global\GetUpLockPublicENSingleInstance";
        internal const string FeedbackUrl = "https://tally.so/r/VpQzxg";
        internal static ProcessStartInfo FeedbackRequest()
        {
            return new ProcessStartInfo(FeedbackUrl) { UseShellExecute = true };
        }
#else
        internal const string CultureName = "zh-CN";
        internal const string Id = "GetUpLockPublic";
        internal const string UpdateDirectory = "/updates/windows/";
        internal const string PosterSuffix = "";
        internal const string FontFamily = "Microsoft YaHei UI";
        internal const string InstanceMutex = @"Global\SquatLockSingleInstance";
#endif
        internal const string UpdateMutex = @"Global\" + Id + "UpdateInProgress";
    }
}
