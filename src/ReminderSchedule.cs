using System;
using System.Diagnostics;

namespace SquatLockTiny
{
    // Pause belongs to this process only. Every launch/resume starts a full interval;
    // legacy reminder_pause data is deliberately neither read nor written.
    internal sealed class ReminderSchedule
    {
        readonly Func<DateTime> utcNow;
        readonly Func<long> ticks;
        readonly long frequency;
        int intervalSeconds;
        long deadline;
        internal bool IsPaused { get; private set; }
        internal bool IsSystemInactive { get; private set; }
        internal DateTime? PauseUntilUtc { get; private set; }
        internal int? PauseMinutes { get; private set; }

        internal ReminderSchedule(int intervalSeconds, Func<DateTime> utcNow=null,
                                  Func<long> ticks=null, long frequency=0)
        {
            this.utcNow=utcNow??(()=>DateTime.UtcNow);
            this.ticks=ticks??(()=>Stopwatch.GetTimestamp());this.frequency=frequency>0?frequency:Stopwatch.Frequency;
            SetInterval(intervalSeconds);
        }

        internal long SecondsRemaining { get { return IsSystemInactive?intervalSeconds:Math.Max(0,(long)Math.Ceiling((deadline-ticks())/(double)frequency)); } }
        internal long PauseSecondsRemaining { get { return PauseUntilUtc.HasValue?Math.Max(0,(long)Math.Ceiling((PauseUntilUtc.Value-utcNow().ToUniversalTime()).TotalSeconds)):0; } }
        internal void Restart(){deadline=ticks()+(long)intervalSeconds*frequency;}
        internal void SetSystemInactive(bool inactive)
        {
            if(IsSystemInactive==inactive)return;
            IsSystemInactive=inactive;Restart();
        }
        internal void SetInterval(int seconds)
        {if(seconds<1||seconds>86400)throw new ArgumentOutOfRangeException("seconds");intervalSeconds=seconds;Restart();}

        internal void Pause(int? minutes)
        {
            if(minutes.HasValue&&minutes.Value!=30&&minutes.Value!=60)throw new ArgumentOutOfRangeException("minutes");
            DateTime? until=minutes.HasValue?(DateTime?)utcNow().ToUniversalTime().AddMinutes(minutes.Value):null;
            IsPaused=true;PauseUntilUtc=until;PauseMinutes=minutes;
        }
        internal void Resume()
        {
            IsPaused=false;PauseUntilUtc=null;PauseMinutes=null;Restart();
        }
        internal bool ResumeIfExpired()
        {
            if(IsSystemInactive||!IsPaused||!PauseUntilUtc.HasValue||PauseUntilUtc.Value>utcNow().ToUniversalTime())return false;
            Resume();return true;
        }
    }
}
