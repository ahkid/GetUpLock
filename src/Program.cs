using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SquatLockTiny
{
    internal static class Program
    {
        public const string Version = "1.3.13";
        public const long BuildNumber = 2026111105;
        public const string MutexName = Edition.InstanceMutex;
        public static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
        private static string logPath;
        private static readonly object logSync = new object();

        [DllImport("shcore.dll")] private static extern int SetProcessDpiAwareness(int awareness);
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("kernel32.dll")] private static extern ushort SetThreadUILanguage(ushort languageId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileInt(string section, string key, int fallback, string fileName);

        [STAThread]
        private static int Main(string[] args)
        {
            bool smoke = Array.IndexOf(args, "--smoke-test") >= 0 || Array.IndexOf(args, "--camera-check") >= 0;
            bool setupStartup = Array.IndexOf(args, "--configure-installed-startup") >= 0;
            try
            {
                var uiCulture = CultureInfo.GetCultureInfo(Edition.CultureName);
                Thread.CurrentThread.CurrentUICulture = uiCulture;
                SetThreadUILanguage((ushort)uiCulture.LCID);
                string output = Argument(args, "--smoke-test") ?? Argument(args, "--camera-check");
                logPath = Path.Combine(smoke && output != null ? Path.GetFullPath(output) :
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Edition.Id), "squatlock.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                // The installer can hold the update mutex while registering startup.
                if (setupStartup)
                    return SyncAutostart(Config.DefaultAutostart, new StateStore()) ? 0 : 1;
                try { SetProcessDpiAwareness(2); } catch { SetProcessDPIAware(); }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                if (smoke)
                {
                    if (String.IsNullOrEmpty(output)) throw new ArgumentException("--smoke-test needs an output directory");
                    return Array.IndexOf(args,"--camera-check") >= 0 ? CameraChecks.Run(Path.GetFullPath(output)) : SmokeTests.Run(Path.GetFullPath(output));
                }
                try
                {
                    using(Mutex updater=Mutex.OpenExisting(Edition.UpdateMutex))
                    {MessageBox.Show(Strings.UpdateInProgress,Strings.ProductName,MessageBoxButtons.OK,MessageBoxIcon.Information);return 0;}
                }
                catch(WaitHandleCannotBeOpenedException){}
                if (Array.IndexOf(args, "--enable-autostart") >= 0) { SetAutostartPreference(true,new StateStore()); return 0; }
                if (Array.IndexOf(args, "--disable-autostart") >= 0) { SetAutostartPreference(false,new StateStore()); return 0; }
                bool created;
                using (Mutex singleInstance = new Mutex(false, MutexName, out created))
                {
                    if (!created)
                    {
                        if(Array.IndexOf(args,"--after-install")>=0)return 0;
                        MessageBox.Show(Strings.AlreadyRunning, Strings.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return 0;
                    }
                    Config config = Config.Load(AppDir);
                    if (!IsPortable())
                        SyncAutostart(config.Bool("autostart", Config.DefaultAutostart), config.Store);
                    StateStore store = new StateStore(null);
                    using (TrayApplication app = new TrayApplication(config, store,
                        Array.IndexOf(args, "--demo") >= 0, false))
                    {
                        if (Array.IndexOf(args, "--now") >= 0) app.StartRequested = true;
                        if(Array.IndexOf(args,"--after-install")>=0)app.ShowInstalledNotice();
                        if(Array.IndexOf(args,"--after-update")>=0)app.ShowUpdatedNotice();
                        Log(Strings.StartedLog + Version + Strings.RuntimeLog);
                        Application.Run(app);
                    }
                }
                return 0;
            }
            catch (Exception error)
            {
                Log(error.ToString());
                if (!smoke && !setupStartup) MessageBox.Show(Strings.StartFailed + error.Message + Strings.LogLocation + logPath, Strings.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        internal static string Argument(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        internal static bool IsPortable()
        {
            string mode = Path.Combine(AppDir, "install-mode.ini");
            return GetPrivateProfileInt("Install", "Portable", 1, mode) != 0;
        }

        internal static void Log(string message)
        {
            try
            {
                if (logPath == null) return;
                lock (logSync) File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine);
            }
            catch { }
        }

        internal static bool AutostartEnabled()
        {
            try{using(RegistryKey key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                return key!=null&&key.GetValue(Edition.Id)!=null;}catch{return false;}
        }
        internal static void SetAutostartPreference(bool enabled,StateStore store)
        {
            bool before=AutostartEnabled();SetAutostart(enabled);
            try{store.Update(new System.Collections.Generic.Dictionary<string,object>{{"autostart",enabled}});}
            catch{SetAutostart(before);throw;}
        }

        private static void SetAutostart(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (enabled) key.SetValue(Edition.Id, "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue(Edition.Id, false);
            }
        }

        private static bool SyncAutostart(bool defaultEnabled, StateStore store)
        {
            try
            {
                object preference = Value.Get(store.Read(), "autostart");
                object current = null;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    if (key != null) current = key.GetValue(Edition.Id);
                string expected = "\"" + Application.ExecutablePath + "\"";
                bool enabled = preference is bool ? (bool)preference : defaultEnabled || current != null;
                if (enabled && !String.Equals(current as string, expected, StringComparison.OrdinalIgnoreCase))
                    SetAutostart(true);
                else if (!enabled && current != null)
                    SetAutostart(false);
                return true;
            }
            catch (Exception error) { Log(Strings.StartupSyncFailed + error.Message); return false; }
        }
    }
}
