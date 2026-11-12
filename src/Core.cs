// Native offline core, C# 5 / .NET Framework 4.8. State keys match Python release.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace SquatLockTiny
{
    public static class Value
    {
        public static object Get(IDictionary<string,object> d,string k,object fallback=null) { object v;return d!=null&&d.TryGetValue(k,out v)?v:fallback; }
        public static int Int(object v,int fallback=0)
        {
            if(v is string)
            {
                int parsed;
                return Int32.TryParse((string)v,NumberStyles.Integer,CultureInfo.InvariantCulture,out parsed)?parsed:fallback;
            }
            try
            {
                if(v==null)return fallback;
                double number=Convert.ToDouble(v,CultureInfo.InvariantCulture);
                return Double.IsNaN(number)||Double.IsInfinity(number)||number<Int32.MinValue||number>Int32.MaxValue?fallback:(int)number;
            }
            catch{return fallback;}
        }
        public static double Number(object v,double fallback=0) {try{return v==null?fallback:Convert.ToDouble(v,CultureInfo.InvariantCulture);}catch{return fallback;} }
        public static bool Bool(object v) {if(v==null)return false;if(v is bool)return(bool)v;if(v is string)return((string)v).Length!=0;if(v is System.Collections.ICollection)return((System.Collections.ICollection)v).Count!=0;try{return Convert.ToDouble(v,CultureInfo.InvariantCulture)!=0;}catch{return true;} }
        public static string Text(object v,string fallback="") {return v==null?fallback:Convert.ToString(v,CultureInfo.InvariantCulture);}
        public static string Cut(string s,int n)
        {
            int end=0,count=0;
            while(end<s.Length&&count<n)
            {
                if(Char.IsHighSurrogate(s[end])&&end+1<s.Length&&Char.IsLowSurrogate(s[end+1]))end++;
                end++;count++;
            }
            return end<s.Length?s.Substring(0,end):s;
        }
        public static Dictionary<string,object> Dict(object v) {return v as Dictionary<string,object>;}
    }
    public sealed class StateStore
    {
        static readonly object Gate=new object();
        public readonly string Path;
        public Action<Dictionary<string,object>> BeforeWrite;
        public StateStore(string path=null) {Path=path??DefaultPath;}
        public static string DefaultPath {get{var root=Environment.GetEnvironmentVariable("LOCALAPPDATA");if(String.IsNullOrEmpty(root))root=System.IO.Path.GetTempPath();return System.IO.Path.Combine(root,Edition.Id,"state.json");}}
        public static Dictionary<string,object> ReadFile(string path)
        {
            try{return new JavaScriptSerializer{MaxJsonLength=16777216}.DeserializeObject(File.ReadAllText(path,Encoding.UTF8)) as Dictionary<string,object>??new Dictionary<string,object>();}
            catch{return new Dictionary<string,object>();}
        }
        public Dictionary<string,object> Read() {lock(Gate)return ReadFile(Path);}
        public Dictionary<string,object> Update(IDictionary<string,object> updates)
        {
            return Mutate(delegate(Dictionary<string,object> data){foreach(var item in updates)data[item.Key]=item.Value;});
        }
        public Dictionary<string,object> Mutate(Action<Dictionary<string,object>> change)
        {
            lock(Gate)
            {
                var data=ReadFile(Path);change(data);
                if(BeforeWrite!=null)BeforeWrite(data);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)));
                var temp=Path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    var bytes=new UTF8Encoding(false).GetBytes(new JavaScriptSerializer{MaxJsonLength=16777216}.Serialize(data));
                    using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
                    if(File.Exists(Path))File.Replace(temp,Path,null);else File.Move(temp,Path);
                }
                finally{if(File.Exists(temp))File.Delete(temp);}
                return data;
            }
        }
    }
    public sealed class Config
    {
        public const bool DefaultAutostart = true;
        public Dictionary<string,object> Values {get;private set;}
        public string AppDir {get;private set;}
        public string BundleDir {get;private set;}
        public StateStore Store {get;private set;}
        public Config(){Values=Defaults();AppDir=AppDomain.CurrentDomain.BaseDirectory;BundleDir=AppDir;Store=new StateStore();}
        public static Dictionary<string,object> Defaults()
        {
            return new Dictionary<string,object>{
                {"interval_minutes",20},{"demo_interval_seconds",30},{"target_reps",10},{"unlock_password","1234"},{"camera_enabled",true},
                {"max_consecutive_skips",1},{"autostart",DefaultAutostart},{"camera_index",0},{"down_angle",100},{"up_angle",160},
                {"drop_fraction",.5},{"return_fraction",.15},{"shallow_fraction",.4},{"smoothing",.5},
                {"calibration_frames",30},{"min_visibility",.5},{"shoulder_drop_fraction",.6},{"shoulder_return_fraction",.2},
                {"bottom_edge",.8},{"points_complete",10},{"points_password_skip",-5},{"points_challenge_skip",-5},
                {"points_floor",0},{"hide_ads",false}};
        }
        public static Config Load(string appDir,string bundleDir=null,string statePath=null)
        {
            var cfg=new Config{AppDir=System.IO.Path.GetFullPath(appDir),BundleDir=System.IO.Path.GetFullPath(bundleDir??appDir),Store=new StateStore(statePath)};
            cfg.MergeFile(System.IO.Path.Combine(cfg.BundleDir,"config.json"));
            if(!String.Equals(cfg.AppDir,cfg.BundleDir,StringComparison.OrdinalIgnoreCase))cfg.MergeFile(System.IO.Path.Combine(cfg.AppDir,"config.json"));
            var data=cfg.Store.Read();int minutes=Value.Int(Value.Get(data,"interval_minutes"),-1);
            cfg.Values["interval_minutes"]=ValidInterval(minutes)?minutes:NormalizeInterval(cfg.Int("interval_minutes"));
            cfg.Values["camera_enabled"]=!Object.Equals(Value.Get(data,"camera_enabled"),false);
            return cfg;
        }
        void MergeFile(string path)
        {
            var data=StateStore.ReadFile(path);
            if(!data.ContainsKey("unlock_password")&&data.ContainsKey("parent_password"))data["unlock_password"]=data["parent_password"];
            data.Remove("parent_password");foreach(var item in data)Values[item.Key]=item.Value;
        }
        public int Int(string key,int fallback=0){return Value.Int(Value.Get(Values,key),fallback);}
        public double Number(string key,double fallback=0){return Value.Number(Value.Get(Values,key),fallback);}
        public bool Bool(string key,bool fallback=false){return Value.Bool(Value.Get(Values,key,fallback));}
        public string Text(string key,string fallback=""){return Value.Text(Value.Get(Values,key),fallback);}
        public bool PasswordOk(string password){return password!=null&&password==Value.Text(Value.Get(Values,"unlock_password",Value.Get(Values,"parent_password","")),"None");}
        public static bool ValidInterval(int minutes){return minutes==20||minutes==25||minutes==30||minutes==45;}
        public static int NormalizeInterval(int minutes){return ValidInterval(minutes)?minutes:20;}
        public int SaveInterval(int minutes){minutes=NormalizeInterval(minutes);Store.Update(new Dictionary<string,object>{{"interval_minutes",minutes}});Values["interval_minutes"]=minutes;return minutes;}
        public bool CameraEnabled {get{return Bool("camera_enabled",true);}}
        public void SaveCameraEnabled(bool enabled){Store.Update(new Dictionary<string,object>{{"camera_enabled",enabled}});Values["camera_enabled"]=enabled;}
        public string AssetPath(string name){var p=System.IO.Path.Combine(AppDir,"assets",name);return File.Exists(p)?p:System.IO.Path.Combine(BundleDir,"assets",name);}
    }
    public sealed class ScoreBoard
    {
        readonly StateStore store;
        DateTime? todayOverride;
        Dictionary<string,object> player;
        public string Day {get;private set;}
        public string PendingSession {get;private set;}
        public bool RecoveredInterruption {get;private set;}
        private string pendingMode = "auto";
        private int pendingTarget = 10;
        public ScoreBoard(StateStore stateStore,DateTime? today=null)
        {
            store=stateStore;todayOverride=today;var data=store.Read();var raw=Value.Dict(Value.Get(data,"personal_profile"));bool migrated=raw==null;
            if(migrated){var players=Value.Dict(Value.Get(data,"players"));raw=players==null?data:Value.Dict(Value.Get(players,Value.Text(Value.Get(data,"active_player"))=="b"?"b":"a"));}
            if(raw==null)raw=new Dictionary<string,object>();
            var name=Value.Text(Value.Get(raw,"name"));if(name.Length==0)name=Value.Text(Value.Get(raw,"nickname"));if(name.Length==0)name=Strings.DefaultNickname;
            player=new Dictionary<string,object>{{"name",Value.Cut(name,32)}};
            foreach(var key in new[]{"points","sets_today","total_sets","password_skips_today","challenge_skips_today","exit_skips_today","days_participated","streak_days"})player[key]=Math.Max(0,Value.Int(Value.Get(raw,key)));
            player["total_sets"]=Math.Max(Get("total_sets"),Get("sets_today"));player["points_delta_today"]=Value.Int(Value.Get(raw,"points_delta_today"));
            player["participated_today"]=Value.Bool(Value.Get(raw,"participated_today"));player["last_complete_day"]=Value.Cut(Value.Text(Value.Get(raw,"last_complete_day")),10);
            var day=Value.Text(Value.Get(data,"personal_score_day"));if(day.Length==0)day=Value.Text(Value.Get(data,"score_day"));
            Day=Value.Cut(day,10);var pending=Value.Get(data,"active_squat_session") as string;PendingSession=String.IsNullOrEmpty(pending)?null:pending;
            EnsureDay();if(PendingSession!=null){FinishSession(PendingSession,"exit");RecoveredInterruption=true;}else if(migrated)Save();
        }
        int Get(string key){return Value.Int(Value.Get(player,key));}
        public Dictionary<string,object> Current(){return player;}
        void Save()
        {
            store.Update(new Dictionary<string,object>{
                {"personal_profile", new Dictionary<string,object>(player)},
                {"personal_score_day", Day}, {"active_squat_session", PendingSession}
            });
        }
        public bool EnsureDay(DateTime? today=null)
        {
            if(today.HasValue)todayOverride=today;var current=(todayOverride??DateTime.Today).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);if(current==Day)return false;
            foreach(var key in new[]{"sets_today","password_skips_today","challenge_skips_today","exit_skips_today","points_delta_today"})player[key]=0;
            player["participated_today"]=false;Day=current;Save();return true;
        }
        public string BeginSession(string mode="auto",int target=10)
        {
            if(mode!="auto"&&mode!="manual")throw new ArgumentException("Invalid completion mode");
            EnsureDay();if(PendingSession==null)
            {
                pendingMode=mode;pendingTarget=Math.Max(1,Math.Min(1000,target));
                PendingSession=Guid.NewGuid().ToString("N");
                try { Save(); } catch { PendingSession=null; throw; }
            }
            return PendingSession;
        }
        public void MarkManual() { pendingMode = "manual"; }
        internal bool CancelForSystemInactivity(string sessionId)
        {
            if(String.IsNullOrEmpty(sessionId)||sessionId!=PendingSession)return false;
            PendingSession=null;
            try{Save();}catch{PendingSession=sessionId;throw;}
            return true;
        }
        public bool FinishSession(string sessionId,string outcome,int? recognizedCount=null)
        {
            if(String.IsNullOrEmpty(sessionId)||sessionId!=PendingSession)return false;
            if(outcome!="complete"&&outcome!="password"&&outcome!="challenge"&&outcome!="exit")throw new ArgumentException("Unknown outcome");
            EnsureDay();var before=new Dictionary<string,object>(player);var pending=PendingSession;
            if(outcome=="complete" && pendingMode=="auto" && (!recognizedCount.HasValue || recognizedCount.Value<pendingTarget))
                throw new InvalidOperationException("Automatic completion requires recognized repetitions");
            try { PendingSession=null; Record(outcome); Save(); }
            catch { player=before; PendingSession=pending; throw; }
            return true;
        }
        void Record(string outcome)
        {
            if(!Value.Bool(player["participated_today"])){player["days_participated"]=Get("days_participated")+1;player["participated_today"]=true;}
            if(outcome=="complete")
            {
                player["sets_today"]=Get("sets_today")+1;player["total_sets"]=Get("total_sets")+1;
                if(Value.Text(player["last_complete_day"])!=Day){player["streak_days"]=Value.Text(player["last_complete_day"])==Yesterday?Get("streak_days")+1:1;player["last_complete_day"]=Day;}
            }
            else{string key=outcome+"_skips_today";player[key]=Get(key)+1;}
        }
        string Yesterday{get{return DateTime.ParseExact(Day,"yyyy-MM-dd",CultureInfo.InvariantCulture).AddDays(-1).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);}}
        public Dictionary<string,object> Profile()
        {
            EnsureDay();var p=new Dictionary<string,object>(player);p["skips_today"]=Get("password_skips_today")+Get("challenge_skips_today")+Get("exit_skips_today");
            var last=Value.Text(p["last_complete_day"]);if(last!=Day&&last!=Yesterday)p["streak_days"]=0;return p;
        }
        public string ProfileText(){var p=Profile();return String.Format(Strings.ProfileSummary,p["name"],p["sets_today"],p["total_sets"],p["skips_today"],p["streak_days"],p["days_participated"]);}
        public string ProfileNotifyText(){var p=Profile();return String.Format(Strings.TodayProfile,p["name"],p["sets_today"]);}
        public string TipSuffix(){EnsureDay();return String.Format(Strings.TodaySets,player["sets_today"]);}
    }
    public sealed class SkipPolicy
    {
        readonly StateStore store;
        public int Maximum{get;private set;}public int Count{get;private set;}
        public SkipPolicy(StateStore stateStore,int maximum=1){store=stateStore;Maximum=Math.Max(0,maximum);Count=Math.Max(0,Value.Int(Value.Get(store.Read(),"consecutive_skips")));}
        public bool CanPasswordSkip{get{return Count<Maximum;}}public bool NeedsChallenge{get{return Count>=Maximum;}}public int Remaining{get{return Math.Max(0,Maximum-Count);}}
        public string StatusText{get{return CanPasswordSkip?Strings.QuickSkipAvailable:Strings.QuickSkipChallenge;}}
        void Save(){store.Update(new Dictionary<string,object>{{"consecutive_skips",Count}});}
        public void RecordSkip(){int before=Count;Count++;try{Save();}catch{Count=before;throw;}}
        public void RecordComplete(){int before=Count;Count=0;try{Save();}catch{Count=before;throw;}}
    }
    public static class AlphabetChallenge
    {
        public const string Alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public static string Match(string input)
        {
            var letters=new StringBuilder();foreach(var ch in input??"")if(Char.IsLetter(ch))letters.Append(Char.ToUpperInvariant(ch));
            int length=Math.Min(Alphabet.Length,letters.Length);for(int i=0;i<length;i++)if(letters[i]!=Alphabet[i])return null;return Alphabet.Substring(0,length);
        }
    }
    public sealed class Landmark
    {
        public double X,Y,Visibility;
        public Landmark(){Visibility=1;}
        public Landmark(double x,double y,double visibility=1){X=x;Y=y;Visibility=visibility;}
    }

    public sealed class SquatCounter
    {
        public const string KneeMode="knee",VerticalMode="vertical",ShoulderMode="shoulder",NoneMode="none";
        readonly double downAngle,upAngle,dropFraction,returnFraction,shallowFraction,smoothing,minVisibility,shoulderDropFraction,shoulderReturnFraction,bottomEdge;
        readonly int calibrationFrames;
        public int Count{get;private set;}public string State{get;private set;}public string Mode{get;private set;}
        public double? LastAngle{get;private set;}public bool HalfSquat{get;private set;}public bool OutOfFrame{get;private set;}
        double peak;
        const int ConfirmFrames=3,MissingLimit=30,QuickCalibrationFrames=8;
        string cycleMode,observationMode;
        int upFrames,downFrames,missingFrames;
        bool armed;
        public bool TrackingLost{get;private set;}
        double? baseHip,baseSh,torso,drop,sWidth,sDrop,sLastY;
        double? rawDrop,rawShoulderDrop,lastPairHip,lastPairShoulder;
        public bool Ready{get{return armed&&!Calibrating&&!TrackingLost;}}
        readonly List<double?[]> calibration=new List<double?[]>(),sCalibration=new List<double?[]>();
        readonly Dictionary<string,double> sBase=new Dictionary<string,double>();
        public SquatCounter(Config cfg=null)
        {
            cfg=cfg??new Config();downAngle=cfg.Number("down_angle",100);upAngle=cfg.Number("up_angle",160);
            dropFraction=cfg.Number("drop_fraction",.5);returnFraction=cfg.Number("return_fraction",.15);
            shallowFraction=cfg.Number("shallow_fraction",.4);smoothing=cfg.Number("smoothing",.5);
            calibrationFrames=Math.Max(1,cfg.Int("calibration_frames",30));minVisibility=cfg.Number("min_visibility",.5);
            shoulderDropFraction=cfg.Number("shoulder_drop_fraction",.6);shoulderReturnFraction=cfg.Number("shoulder_return_fraction",.2);
            bottomEdge=cfg.Number("bottom_edge",.8);Reset();
        }
        public void Reset(){Count=0;State="up";Mode=NoneMode;peak=0;HalfSquat=false;LastAngle=null;baseHip=baseSh=torso=drop=sWidth=sDrop=sLastY=null;rawDrop=rawShoulderDrop=lastPairHip=lastPairShoulder=null;calibration.Clear();sCalibration.Clear();sBase.Clear();OutOfFrame=false;TrackingLost=false;missingFrames=0;ResetCycle();}
        static double Hypot(double x,double y){return Math.Sqrt(x*x+y*y);}
        static bool Visible(Landmark p,double v){return p!=null&&p.Visibility>=v&&!Double.IsNaN(p.X)&&!Double.IsNaN(p.Y)&&!Double.IsInfinity(p.X)&&!Double.IsInfinity(p.Y);}
        public static double AngleBetween(Landmark a,Landmark b,Landmark c){double angle=Math.Abs((Math.Atan2(c.Y-b.Y,c.X-b.X)-Math.Atan2(a.Y-b.Y,a.X-b.X))*180/Math.PI);return angle>180?360-angle:angle;}
        public static double? KneeAngle(Landmark[] p,double v=.5)
        {
            if(p==null||p.Length<29)return null;double sum=0;int count=0;
            foreach(var indices in new[]{new[]{23,25,27},new[]{24,26,28}})if(indices.All(i=>Visible(p[i],v))){sum+=AngleBetween(p[indices[0]],p[indices[1]],p[indices[2]]);count++;}
            return count==0?(double?)null:sum/count;
        }
        double[] Midpoint(Landmark[] p,int i,int j){var available=new[]{p[i],p[j]}.Where(x=>Visible(x,minVisibility)).ToArray();return available.Length==0?null:new[]{available.Average(x=>x.X),available.Average(x=>x.Y)};}
        bool CalibrationReady(List<double?[]> samples,bool shoulders=false)
        {
            if(samples.Count>=calibrationFrames)return true;
            if(samples.Count<QuickCalibrationFrames)return false;
            var window=samples.Skip(samples.Count-QuickCalibrationFrames).ToArray();
            int index=shoulders?1:2;double scale=window.Average(row=>row[index].Value);
            if(scale<=0||window.Max(row=>row[0].Value)-window.Min(row=>row[0].Value)>scale*.06)return false;
            if(window.Max(row=>row[index].Value)-window.Min(row=>row[index].Value)>scale*.1)return false;
            if(!shoulders)
            {
                if(window.Any(row=>!row[1].HasValue))return false;
                if(window.Max(row=>row[1].Value)-window.Min(row=>row[1].Value)>scale*.06)return false;
            }
            samples.RemoveRange(0,samples.Count-QuickCalibrationFrames);return true;
        }
        void ResetCycle()
        {
            State="up";cycleMode=observationMode=NoneMode;armed=false;
            upFrames=downFrames=0;peak=0;
        }
        bool Missing()
        {
            // Missing landmarks are not evidence of either half of a squat.
            upFrames=downFrames=0;missingFrames++;TrackingLost=cycleMode!=NoneMode;
            if(missingFrames>=MissingLimit)
            {
                ResetCycle();baseHip=baseSh=torso=drop=sWidth=sDrop=sLastY=null;rawDrop=rawShoulderDrop=lastPairHip=lastPairShoulder=null;
                calibration.Clear();sCalibration.Clear();sBase.Clear();OutOfFrame=TrackingLost=false;
            }
            return false;
        }
        bool Step(double progress,bool isDown,bool isUp)
        {
            if(Double.IsNaN(progress)||Double.IsInfinity(progress))return Missing();
            missingFrames=0;TrackingLost=false;
            if(observationMode!=Mode){ResetCycle();observationMode=Mode;}
            if(State=="up")
            {
                if(!armed)
                {
                    upFrames=isUp?upFrames+1:0;
                    if(upFrames>=ConfirmFrames){armed=true;upFrames=0;}
                    return false;
                }
                peak=Math.Max(peak,progress);
                // Pin the tracker at descent onset, before the bottom threshold.
                if(cycleMode==NoneMode&&!isUp&&progress>0)cycleMode=Mode;
                if(isDown)
                {
                    cycleMode=Mode;downFrames++;upFrames=0;
                    if(downFrames>=ConfirmFrames)State="down";
                }
                else
                {
                    downFrames=0;upFrames=isUp?upFrames+1:0;
                    if(upFrames>=ConfirmFrames)
                    {
                        if(peak>=shallowFraction)HalfSquat=true;
                        peak=0;cycleMode=NoneMode;upFrames=0;
                    }
                }
            }
            else
            {
                upFrames=isUp?upFrames+1:0;
                if(upFrames>=ConfirmFrames){Count++;ResetCycle();observationMode=Mode;armed=true;return true;}
            }
            return false;
        }
        public bool Update(double? angle)
        {
            LastAngle=angle;
            if(!angle.HasValue||Double.IsNaN(angle.Value)||Double.IsInfinity(angle.Value)||angle.Value<0||angle.Value>180)return Missing();
            if(cycleMode!=NoneMode&&cycleMode!=KneeMode)return Missing();
            Mode=KneeMode;
            return Step((180-angle.Value)/Math.Max(1,180-downAngle),angle.Value<downAngle,angle.Value>upAngle);
        }
        public bool UpdateLandmarks(Landmark[] p)
        {
            if(p==null||p.Length<29)
            {
                if(cycleMode!=NoneMode&&cycleMode!=ShoulderMode)return Missing();
                return Shoulder(null);
            }
            var shoulder=Midpoint(p,11,12);var hip=Midpoint(p,23,24);
            double? width=Visible(p[11],minVisibility)&&Visible(p[12],minVisibility)?(double?)Hypot(p[11].X-p[12].X,p[11].Y-p[12].Y):null;
            var angle=KneeAngle(p,minVisibility);double? verticalDrop=null;
            if(hip!=null||cycleMode==VerticalMode)verticalDrop=VerticalDrop(shoulder,hip,width);
            if(hip!=null&&State=="up"&&Mode!=ShoulderMode){var head=HeadPoint(p);if(head!=null)ShoulderDrop(head);}
            // One tracker owns descent through completion. Hidden hips can use
            // shoulder displacement anchored at the last jointly visible pair.
            if(cycleMode==ShoulderMode){LastAngle=null;return Shoulder(p);}
            if(cycleMode==KneeMode)
            {
                if(!angle.HasValue){LastAngle=null;return Missing();}
                OutOfFrame=false;return Update(angle);
            }
            if(cycleMode==VerticalMode)
            {
                LastAngle=null;
                if(shoulder==null||!verticalDrop.HasValue)return Missing();
                OutOfFrame=false;Mode=VerticalMode;
                return Step(verticalDrop.Value/dropFraction,rawDrop.Value>=dropFraction,rawDrop.Value<=returnFraction);
            }
            if(angle.HasValue){OutOfFrame=false;return Update(angle);}LastAngle=null;
            if(hip!=null&&shoulder!=null){OutOfFrame=false;Mode=VerticalMode;if(!verticalDrop.HasValue)return false;return Step(verticalDrop.Value/dropFraction,rawDrop.Value>=dropFraction,rawDrop.Value<=returnFraction);}
            return Shoulder(p);
        }
        sealed class Head{public string Key;public double Y;public double? Width,NoseY;}
        Head HeadPoint(Landmark[] p)
        {
            if(p==null)return null;var l=p[11];var r=p[12];var n=p[0];
            double? w=Visible(l,minVisibility)&&Visible(r,minVisibility)?(double?)Hypot(l.X-r.X,l.Y-r.Y):null;
            if(w.HasValue&&w.Value!=0)return new Head{Key="sh",Y=(l.Y+r.Y)/2,Width=w,NoseY=Visible(n,minVisibility)?(double?)n.Y:null};
            if(Visible(n,minVisibility))return new Head{Key="nose",Y=n.Y,Width=null,NoseY=n.Y};return null;
        }
        double? ShoulderDrop(Head h)
        {
            if(!sWidth.HasValue)
            {
                if(h.Width.HasValue&&h.Width.Value!=0)
                {
                    if(sCalibration.Count>0)
                    {
                        var first=sCalibration[0];
                        if(Math.Abs(h.Width.Value/first[1].Value-1)>.2||Math.Abs(h.Y-first[0].Value)>first[1].Value*.15)sCalibration.Clear();
                    }
                    sCalibration.Add(new double?[]{h.Y,h.Width,h.NoseY});
                    if(CalibrationReady(sCalibration,true))
                    {
                        sBase["sh"]=sCalibration.Average(c=>c[0].Value);sWidth=sCalibration.Average(c=>c[1].Value);
                        var noses=sCalibration.Where(c=>c[2].HasValue).ToArray();if(noses.Length>0)sBase["nose"]=noses.Average(c=>c[2].Value);sCalibration.Clear();
                    }
                }
                return null;
            }
            if(h.Width.HasValue&&h.Width.Value!=0&&State=="up"&&cycleMode==NoneMode&&Math.Abs(h.Width.Value/sWidth.Value-1)>.3)
            {sWidth=null;sBase.Clear();sDrop=null;sCalibration.Clear();sCalibration.Add(new double?[]{h.Y,h.Width,h.NoseY});return null;}
            if(!sBase.ContainsKey(h.Key))return null;
            double raw=(h.Y-sBase[h.Key])/sWidth.Value;rawShoulderDrop=raw;sDrop=sDrop.HasValue?smoothing*raw+(1-smoothing)*sDrop.Value:raw;sLastY=h.Y;
            if(State=="up"&&Math.Abs(sDrop.Value)<shoulderReturnFraction/2)
            {
                const double k=.05;sBase[h.Key]=(1-k)*sBase[h.Key]+k*h.Y;
                if(h.Width.HasValue&&h.Width.Value!=0)sWidth=(1-k)*sWidth.Value+k*h.Width.Value;
                if(h.Key=="sh"&&h.NoseY.HasValue)sBase["nose"]=sBase.ContainsKey("nose")?(1-k)*sBase["nose"]+k*h.NoseY.Value:h.NoseY.Value;
            }
            return sDrop;
        }
        bool Shoulder(Landmark[] p)
        {
            var h=HeadPoint(p);
            if(h==null)
            {
                bool goingDown=(sDrop.HasValue&&sDrop.Value>shoulderReturnFraction)||(sLastY.HasValue&&sLastY.Value>bottomEdge);
                if(sWidth.HasValue&&(goingDown||OutOfFrame)){Mode=ShoulderMode;OutOfFrame=true;return Missing();}
                Mode=NoneMode;return Missing();
            }
            Mode=ShoulderMode;OutOfFrame=false;var d=ShoulderDrop(h);if(!d.HasValue)return false;
            return Step(d.Value/shoulderDropFraction,rawShoulderDrop.Value>=shoulderDropFraction,rawShoulderDrop.Value<=shoulderReturnFraction);
        }
        double? VerticalDrop(double[] sh,double[] hip,double? width)
        {
            if(sh==null)return null;double? currentTorso=null;
            if(hip!=null)currentTorso=Hypot(hip[0]-sh[0],hip[1]-sh[1]);else if(torso.HasValue)currentTorso=torso;else if(width.HasValue&&width.Value!=0)currentTorso=width.Value*1.3;
            if(!currentTorso.HasValue||currentTorso.Value<1e-3)return null;
            if(!baseSh.HasValue)
            {
                if(calibration.Count>0)
                {
                    var first=calibration[0];bool moved=Math.Abs(sh[1]-first[0].Value)>first[2].Value*.15;
                    if(hip!=null&&first[1].HasValue)moved=moved||Math.Abs(hip[1]-first[1].Value)>first[2].Value*.15;
                    if(moved||Math.Abs(currentTorso.Value/first[2].Value-1)>.2)calibration.Clear();
                }
                calibration.Add(new double?[]{sh[1],hip!=null?(double?)hip[1]:null,currentTorso});
                if(CalibrationReady(calibration))
                {
                    baseSh=calibration.Average(c=>c[0].Value);var hips=calibration.Where(c=>c[1].HasValue).ToArray();baseHip=hips.Length==0?(double?)null:hips.Average(c=>c[1].Value);
                    torso=calibration.Average(c=>c[2].Value);lastPairHip=hip!=null?(double?)hip[1]:null;lastPairShoulder=sh[1];calibration.Clear();
                }
                return null;
            }
            double raw;
            if(hip!=null&&baseHip.HasValue){raw=(hip[1]-baseHip.Value)/torso.Value;lastPairHip=hip[1];lastPairShoulder=sh[1];}
            else if(baseHip.HasValue&&lastPairHip.HasValue&&lastPairShoulder.HasValue)
                raw=(lastPairHip.Value-baseHip.Value+sh[1]-lastPairShoulder.Value)/torso.Value;
            else return null;
            rawDrop=raw;
            drop=drop.HasValue?smoothing*raw+(1-smoothing)*drop.Value:raw;
            if(State=="up"&&Math.Abs(drop.Value)<returnFraction/2)
            {
                const double k=.05;baseSh=(1-k)*baseSh.Value+k*sh[1];
                if(hip!=null){baseHip=baseHip.HasValue?(1-k)*baseHip.Value+k*hip[1]:hip[1];torso=(1-k)*torso.Value+k*currentTorso.Value;}
            }
            return drop;
        }
        public void ResetHalf(){HalfSquat=false;}
        public bool Calibrating{get{return Mode==ShoulderMode?!sWidth.HasValue:Mode==VerticalMode?!baseSh.HasValue:false;}}
        public string ModeName{get{return Mode==KneeMode?Strings.KneeModeName:Mode==VerticalMode?Strings.TorsoModeName:Mode==ShoulderMode?Strings.ShoulderModeName:Strings.PersonMissing;}}
        public string StatusText{get{return ModeName+(TrackingLost?Strings.TrackingLostSuffix:Calibrating?Strings.CalibratingSuffix:!armed&&Mode!=NoneMode?Strings.StandSuffix:State=="down"?Strings.RiseSuffix:armed?Strings.ReadySuffix:"");}}
    }
}
