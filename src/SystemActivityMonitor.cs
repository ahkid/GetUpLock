using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SquatLockTiny
{
    // Independent reasons: opening the lid must not undo a locked session, and
    // an unattended maintenance wake must not start reminders or the camera.
    internal sealed class SystemActivityState
    {
        internal static readonly Guid Lid = new Guid("BA3E0F4D-B817-4094-A2D1-D56379E6A0F3");
        internal static readonly Guid Display = new Guid("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5");
        bool sleeping, lidClosed, displayOff, locked, disconnected;
        internal bool IsInactive { get { return sleeping || lidClosed || displayOff || locked || disconnected; } }
        internal event Action<bool> Changed;

        void Update(Action change)
        {
            bool before = IsInactive;
            change();
            if (before != IsInactive && Changed != null) Changed(IsInactive);
        }
        internal void Power(int code)
        {
            Update(delegate {
                if (code == 4 || code == 18) sleeping = true; // suspend / unattended wake
                // PBT_APMRESUMEAUTOMATIC (18) alone does not imply user presence.
                if (code == 7) sleeping = false; // PBT_APMRESUMESUSPEND
            });
        }
        internal void Setting(Guid setting, int value)
        {
            Update(delegate {
                if (setting == Lid && (value == 0 || value == 1)) lidClosed = value == 0;
                if (setting == Display && value >= 0 && value <= 2)
                {
                    displayOff = value == 0;
                    if (value == 1) sleeping = false;
                }
            });
        }
        internal void Session(int code)
        {
            Update(delegate {
                if (code == 7) locked = true;
                if (code == 8) { locked = false; sleeping = false; }
                if (code == 2 || code == 4) disconnected = true;
                if (code == 1 || code == 3) disconnected = false;
            });
        }
    }

    // A hidden top-level window receives broadcasts even when LockForm has
    // never been shown. Message-only windows do not receive power broadcasts.
    internal sealed class SystemActivityMonitor : NativeWindow, IDisposable
    {
        readonly SystemActivityState state;
        readonly List<IntPtr> registrations = new List<IntPtr>();
        bool sessionRegistered;
        internal SystemActivityMonitor(SystemActivityState state)
        {
            this.state = state;
            CreateHandle(new CreateParams { Caption = "GetUpLock power notifications", ExStyle = 0x80 });
            Register(SystemActivityState.Lid);
            Register(SystemActivityState.Display);
            sessionRegistered = WTSRegisterSessionNotification(Handle, 0);
            if (!sessionRegistered) Program.Log("Session notification registration failed: " + Marshal.GetLastWin32Error());
            ReadInitialSession();
        }
        void Register(Guid setting)
        {
            IntPtr registration = RegisterPowerSettingNotification(Handle, ref setting, 0);
            if (registration != IntPtr.Zero) registrations.Add(registration);
            else Program.Log("Power notification registration failed: " + setting + " / " + Marshal.GetLastWin32Error());
        }
        void ReadInitialSession()
        {
            IntPtr buffer; int bytes;
            // WTSINFOEX: DWORD Level; aligned union containing SessionId,
            // WTS_CONNECTSTATE_CLASS and LONG SessionFlags (Win10/11).
            if (!WTSQuerySessionInformation(IntPtr.Zero, -1, 25, out buffer, out bytes)) return;
            try
            {
                if (bytes < 20 || Marshal.ReadInt32(buffer) != 1) return;
                if (Marshal.ReadInt32(buffer, 12) == 4) state.Session(2); // WTSDisconnected
                if (Marshal.ReadInt32(buffer, 16) == 0) state.Session(7); // WTS_SESSIONSTATE_LOCK
            }
            finally { WTSFreeMemory(buffer); }
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x218)
            {
                int code = message.WParam.ToInt32();
                if (code == 0x8013 && message.LParam != IntPtr.Zero)
                {
                    // POWERBROADCAST_SETTING has a 16-byte GUID, a DWORD length,
                    // then the DWORD value. Reject unsupported payload sizes.
                    if (Marshal.ReadInt32(message.LParam, 16) == 4)
                    {
                        Guid setting = (Guid)Marshal.PtrToStructure(message.LParam, typeof(Guid));
                        state.Setting(setting, Marshal.ReadInt32(message.LParam, 20));
                    }
                }
                else state.Power(code);
                message.Result = new IntPtr(1);
                return;
            }
            if (message.Msg == 0x2B1) state.Session(message.WParam.ToInt32());
            base.WndProc(ref message);
        }
        public void Dispose()
        {
            foreach (IntPtr registration in registrations) UnregisterPowerSettingNotification(registration);
            registrations.Clear();
            if (sessionRegistered) { WTSUnRegisterSessionNotification(Handle); sessionRegistered = false; }
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr RegisterPowerSettingNotification(IntPtr window, ref Guid setting, int flags);
        [DllImport("user32.dll")] static extern bool UnregisterPowerSettingNotification(IntPtr registration);
        [DllImport("wtsapi32.dll", SetLastError = true)] static extern bool WTSRegisterSessionNotification(IntPtr window, int flags);
        [DllImport("wtsapi32.dll")] static extern bool WTSUnRegisterSessionNotification(IntPtr window);
        [DllImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW")] static extern bool WTSQuerySessionInformation(IntPtr server, int session, int info, out IntPtr buffer, out int bytes);
        [DllImport("wtsapi32.dll")] static extern void WTSFreeMemory(IntPtr buffer);
    }
}
