using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SquatLockTiny
{
    internal static class Theme
    {
        // Warm charcoal and the existing 久坐深蹲锁 brand orange.
        internal static readonly Color Background = Color.FromArgb(36,37,34);
        internal static readonly Color Surface = Color.FromArgb(48,49,45);
        internal static readonly Color Ink = Color.FromArgb(231,228,219);
        internal static readonly Color Heading = Ink;
        internal static readonly Color Accent = Color.FromArgb(255,154,20);
        internal static readonly Color ButtonAccent = Color.FromArgb(255,154,20);
        internal static readonly Color SecondaryAccent = Color.FromArgb(139,148,132);
        internal static readonly Color SoftAccent = Color.FromArgb(67,52,30);
        internal static readonly Color Muted = Color.FromArgb(174,173,162);
        internal static readonly Color Line = Color.FromArgb(65,66,60);
        internal static readonly Color Green = Color.FromArgb(159,181,155);
        internal static float Scale { get { using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero)) return Math.Max(1,graphics.DpiY/96f); } }
        internal static Font Font(float points, bool bold) { return new Font(Edition.FontFamily, points, bold ? FontStyle.Bold : FontStyle.Regular); }
        internal static Label Label(string text, float points, bool bold)
        {
            return new Label { Text = text, Font = Font(points, bold), ForeColor = bold ? Heading : Ink,
                BackColor = Color.Transparent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = false, AutoSize = true };
        }
        internal static Button Button(string text, bool primary = true)
        {
            return new RoundedButton(text,primary);
        }
        internal static GraphicsPath Round(RectangleF bounds,float radius)
        {
            float d=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));
            GraphicsPath path=new GraphicsPath();
            if(d<=0){path.AddRectangle(bounds);return path;}
            path.AddArc(bounds.Left,bounds.Top,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);
            path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);path.CloseFigure();return path;
        }
        internal static Control Field(TextBox input)
        {
            float s=Scale;
            RoundedPanel shell=new RoundedPanel {Dock=DockStyle.Top,Height=(int)(46*s),Padding=new Padding((int)(14*s),(int)(11*s),(int)(14*s),(int)(8*s)),Margin=new Padding(0,4,0,16)};
            input.BorderStyle=BorderStyle.None;input.BackColor=Surface;input.ForeColor=Ink;input.Dock=DockStyle.Fill;
            input.Enter+=delegate{shell.FocusedField=true;shell.Invalidate();};input.Leave+=delegate{shell.FocusedField=false;shell.Invalidate();};
            shell.Controls.Add(input);return shell;
        }
    }

    internal sealed class RoundedButton : Button
    {
        private readonly bool primary;
        private bool hovered,pressed;
        internal RoundedButton(string text,bool primary)
        {
            this.primary=primary;Text=text;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Font=Theme.Font(10,false);
            Padding=new Padding(16,8,16,8);MinimumSize=new Size((int)(80*Theme.Scale),(int)(40*Theme.Scale));
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;
            BackColor=primary?Theme.ButtonAccent:Theme.Surface;ForeColor=primary?Theme.Surface:Theme.Ink;Cursor=Cursors.Hand;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        }
        protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hovered=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){pressed=true;Invalidate();}base.OnKeyDown(e);}
        protected override void OnKeyUp(KeyEventArgs e){pressed=false;Invalidate();base.OnKeyUp(e);}
        protected override void OnLostFocus(EventArgs e){pressed=false;Invalidate();base.OnLostFocus(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            Color fill=!Enabled?Theme.Line:primary?(pressed?Color.FromArgb(217,128,12):hovered?Color.FromArgb(255,174,60):Theme.ButtonAccent):(pressed?Theme.Line:hovered?Theme.SoftAccent:Theme.Surface);
            float radius=Math.Min(10*Theme.Scale,Height*.25f);
            using(GraphicsPath path=Theme.Round(new RectangleF(1,1,Width-3,Height-3),radius))
            using(SolidBrush brush=new SolidBrush(fill))
            using(Pen border=new Pen(primary?fill:Theme.Line)) {e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(border,path);}
            if(Focused&&ShowFocusCues)using(GraphicsPath path=Theme.Round(new RectangleF(3,3,Width-7,Height-7),Math.Max(2,radius-2)))
                using(Pen focus=new Pen(primary?Theme.Surface:Theme.Accent,1)){focus.DashStyle=DashStyle.Dot;e.Graphics.DrawPath(focus,path);}
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        internal bool FocusedField;
        internal RoundedPanel(){DoubleBuffered=true;BackColor=Theme.Surface;}
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(GraphicsPath path=Theme.Round(new RectangleF(1,1,Width-3,Height-3),14*Theme.Scale))
            using(SolidBrush brush=new SolidBrush(BackColor))
            using(Pen border=new Pen(FocusedField?Theme.Accent:Theme.Line,FocusedField?2:1)){e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(border,path);}
        }
    }

    internal sealed class TrayMenuColors : ProfessionalColorTable
    {
        internal TrayMenuColors(){UseSystemColors=false;}
        public override Color ToolStripDropDownBackground {get{return Color.White;}}
        public override Color ImageMarginGradientBegin {get{return Color.White;}}
        public override Color ImageMarginGradientMiddle {get{return Color.White;}}
        public override Color ImageMarginGradientEnd {get{return Color.White;}}
        public override Color MenuBorder {get{return Color.FromArgb(230,230,230);}}
        public override Color MenuItemBorder {get{return Theme.Accent;}}
        public override Color MenuItemSelected {get{return Color.FromArgb(255,243,224);}}
        public override Color MenuItemSelectedGradientBegin {get{return MenuItemSelected;}}
        public override Color MenuItemSelectedGradientEnd {get{return MenuItemSelected;}}
        public override Color MenuItemPressedGradientBegin {get{return MenuItemSelected;}}
        public override Color MenuItemPressedGradientMiddle {get{return MenuItemSelected;}}
        public override Color MenuItemPressedGradientEnd {get{return MenuItemSelected;}}
        public override Color CheckBackground {get{return MenuItemSelected;}}
        public override Color CheckSelectedBackground {get{return MenuItemSelected;}}
        public override Color CheckPressedBackground {get{return MenuItemSelected;}}
        public override Color SeparatorDark {get{return MenuBorder;}}
        public override Color SeparatorLight {get{return Color.White;}}
    }

    internal sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
    {
        internal TrayMenuRenderer():base(new TrayMenuColors()){RoundedEdges=false;}
        internal void Apply(ToolStrip menu)
        {
            menu.BackColor=Color.White;menu.ForeColor=Theme.Accent;menu.Renderer=this;
            foreach(ToolStripItem item in menu.Items)
            {
                item.ForeColor=Theme.Accent;
                ToolStripMenuItem submenu=item as ToolStripMenuItem;
                if(submenu!=null&&submenu.HasDropDownItems)Apply(submenu.DropDown);
            }
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // Keep the requested orange for every menu state, including disabled entries.
            TextRenderer.DrawText(e.Graphics,e.Text,e.TextFont,e.TextRectangle,Theme.Accent,e.TextFormat);
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor=Theme.Accent;base.OnRenderArrow(e);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r=e.ImageRectangle;
            using(Pen pen=new Pen(Theme.Accent,Math.Max(1.5f,r.Width/8f)))
            {
                e.Graphics.DrawLines(pen,new PointF[]{new PointF(r.Left+r.Width*.2f,r.Top+r.Height*.5f),
                    new PointF(r.Left+r.Width*.42f,r.Top+r.Height*.72f),new PointF(r.Left+r.Width*.8f,r.Top+r.Height*.25f)});
            }
        }
    }

    internal sealed class InputDialog : Form
    {
        private readonly TextBox input;
        public string Value { get { return input.Text; } }
        public InputDialog(string title, string prompt, bool password)
        {
            Text = title; Font = Theme.Font(11, false); BackColor = Theme.Background; ForeColor = Theme.Ink;
            FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = false; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = false; TopMost = true;
            float scale = Theme.Scale;
            ClientSize = new Size((int)(460*scale), (int)(220*scale)); AutoScaleMode = AutoScaleMode.None;
            Label label = Theme.Label(prompt, 12, true); label.AutoSize = false; label.Dock = DockStyle.None;label.TextAlign=ContentAlignment.MiddleLeft;
            label.SetBounds((int)(28*scale),(int)(16*scale),(int)(404*scale),(int)(55*scale));
            input = new TextBox { UseSystemPasswordChar = password, Font = Theme.Font(12, false),AccessibleName=Strings.Password };
            Control field=Theme.Field(input);field.Dock=DockStyle.None;field.SetBounds((int)(28*scale),(int)(80*scale),(int)(404*scale),(int)(46*scale));
            Button ok = Theme.Button(Strings.OK); ok.SetBounds((int)(332*scale), (int)(152*scale), (int)(100*scale), (int)(40*scale)); ok.DialogResult = DialogResult.OK;
            Button cancel = Theme.Button(Strings.Cancel,false); cancel.SetBounds((int)(220*scale), (int)(152*scale), (int)(100*scale), (int)(40*scale)); cancel.DialogResult = DialogResult.Cancel;
            Controls.AddRange(new Control[] { label, field, cancel, ok }); AcceptButton = ok; CancelButton = cancel;
            Shown += delegate { input.Focus(); };
        }
    }

    internal sealed class AlphabetDialog : Form
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private readonly TextBox input;
        private readonly Label progress;
        private readonly Label message;
        private bool clearing;
        public AlphabetDialog()
        {
            float scale = Theme.Scale;
            Text = Strings.ChallengeTitle; ClientSize = new Size((int)(550*scale), (int)(310*scale)); AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Background; ForeColor = Theme.Ink; TopMost = true; ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedDialog; MinimizeBox = false; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44*scale)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44*scale));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60*scale)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46*scale));
            layout.Controls.Add(Theme.Label(Strings.ChallengePrompt, 15, true), 0, 0);
            progress = Theme.Label("", 14, true); progress.ForeColor = Theme.Accent; layout.Controls.Add(progress, 0, 1);
            input = new TextBox { Font = Theme.Font(15, false),AccessibleName=Strings.ChallengeInput }; layout.Controls.Add(Theme.Field(input), 0, 2);
            message = Theme.Label(Strings.ChallengeHelp, 10, false);message.ForeColor=Theme.Muted; layout.Controls.Add(message, 0, 3);
            Button cancel = Theme.Button(Strings.Cancel,false); cancel.Anchor = AnchorStyles.Right; cancel.DialogResult = DialogResult.Cancel;
            layout.Controls.Add(cancel, 0, 4); Controls.Add(layout); CancelButton = cancel;
            input.TextChanged += delegate { CheckInput(); }; Shown += delegate { input.Focus(); };
        }
        private void CheckInput()
        {
            if (clearing) return;
            string letters = "";
            foreach (char c in input.Text) if (Char.IsLetter(c)) letters += Char.ToUpperInvariant(c);
            string first = letters.Substring(0, Math.Min(Alphabet.Length, letters.Length));
            if (!Alphabet.StartsWith(first, StringComparison.Ordinal))
            {
                clearing = true; input.Clear(); clearing = false; progress.Text = "";
                message.Text = Strings.ChallengeRestart; return;
            }
            progress.Text = first;
            if (first == Alphabet) { DialogResult = DialogResult.OK; Close(); }
        }
    }

    internal sealed class AdPosterBox : PictureBox
    {
        internal AdPosterBox()
        {
            Name = "AdPoster";
            SizeMode = PictureBoxSizeMode.Zoom;
            BackColor = Theme.Background;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Image != null || Width < 2 || Height < 2) return;

            // Paint the local reminder immediately, including during an outstanding request.
            const float width = 300, height = 534;
            float scale = Math.Min(ClientSize.Width / width, ClientSize.Height / height);
            var g = e.Graphics;
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform((ClientSize.Width - width * scale) / 2, (ClientSize.Height - height * scale) / 2);
                g.ScaleTransform(scale, scale);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var card = Theme.Round(new RectangleF(1, 1, width - 2, height - 2), 18))
                using (var surface = new SolidBrush(Theme.Surface))
                using (var border = new Pen(Theme.Line))
                {
                    g.FillPath(surface, card);
                    g.DrawPath(border, card);
                }
                using (var tint = new SolidBrush(Theme.SoftAccent))
                    g.FillEllipse(tint, 98, 145, 104, 104);
                using (var stroke = new Pen(Theme.Accent, 4))
                {
                    stroke.StartCap = stroke.EndCap = LineCap.Round;
                    stroke.LineJoin = LineJoin.Round;
                    g.DrawEllipse(stroke, 148, 164, 14, 14);
                    g.DrawLines(stroke, new[] { new Point(152, 185), new Point(137, 205),
                        new Point(163, 213), new Point(154, 231), new Point(173, 231) });
                    g.DrawLines(stroke, new[] { new Point(152, 186), new Point(167, 197), new Point(183, 194) });
                }
                using (var font = new Font(Edition.FontFamily, 27, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var ink = new SolidBrush(Theme.Heading))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(Strings.BreakPrompt, font, ink, new RectangleF(12, 270, width - 24, 64), format);
            }
            finally { g.Restore(state); }
        }
    }

    internal sealed class CameraView : Control
    {
        private Bitmap image;
        internal float UiScale=Theme.Scale;
        internal int? Countdown {get;set;}
        public bool HasImage { get { return image != null; } }
        public CameraView()
        {
            DoubleBuffered = true; BackColor = Theme.Background; ForeColor = Theme.Muted;
            Dock = DockStyle.Fill; Font = Theme.Font(16, false); Text = Strings.OpeningCamera;
        }
        public void SetFrame(Bitmap value) { Bitmap previous = image; image = value; if (previous != null) previous.Dispose(); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            float s=UiScale;int pad=(int)(18*s),footer=(int)(32*s);
            using(GraphicsPath card=Theme.Round(new RectangleF(1,1,Width-3,Height-3),18*s))
            using(SolidBrush brush=new SolidBrush(Theme.Surface))using(Pen border=new Pen(Theme.Line)){g.FillPath(brush,card);g.DrawPath(border,card);}
            using(Font privacy=Theme.Font(9*s/Theme.Scale,false))TextRenderer.DrawText(g,Strings.CameraPrivacy,privacy,new Rectangle(pad,Height-footer,Width-pad*2,footer),Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            Rectangle viewport=new Rectangle(pad,pad,Math.Max(1,Width-pad*2),Math.Max(1,Height-pad-footer));
            using(GraphicsPath clip=Theme.Round(viewport,10*s))
            {
                GraphicsState state=g.Save();g.SetClip(clip);
                using(SolidBrush tint=new SolidBrush(Theme.Background))g.FillRectangle(tint,viewport);
                if(Countdown.HasValue)
                {
                    using(var numberFont=new Font("Segoe UI",Math.Max(36,Math.Min(viewport.Width*.26f,viewport.Height*.36f)),FontStyle.Bold,GraphicsUnit.Pixel))
                        TextRenderer.DrawText(g,Countdown.Value.ToString(),numberFont,viewport,Theme.Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
                    using(var hintFont=Theme.Font(13*s/Theme.Scale,false))
                        TextRenderer.DrawText(g,Strings.CountdownHint,hintFont,new Rectangle(viewport.Left,viewport.Top+(int)(viewport.Height*.76),viewport.Width,(int)(40*s)),Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
                }
                else if (image != null)
                {
                    float scale = Math.Min((float)viewport.Width / image.Width, (float)viewport.Height / image.Height);
                    int width = Math.Max(1, (int)(image.Width * scale)), height = Math.Max(1, (int)(image.Height * scale));
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.DrawImage(image, new Rectangle(viewport.Left+(viewport.Width-width)/2,viewport.Top+(viewport.Height-height)/2,width,height));
                }
                else TextRenderer.DrawText(g, Text, Font, viewport, ForeColor,TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                g.Restore(state);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing && image != null) image.Dispose(); image = null; base.Dispose(disposing); }
    }

    internal class CoverForm : Form
    {
        public bool AllowClose;
        public CoverForm()
        {
            BackColor = Theme.Background; ForeColor = Theme.Ink; FormBorderStyle = FormBorderStyle.None;
            TopMost = true; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            Controls.Add(Theme.Label(Strings.PrimaryScreenPrompt, 28, true));
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            base.OnFormClosing(e);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape || keyData == (Keys.Alt | Keys.F4)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    internal sealed class LockForm : Form
    {
        private readonly Config config;
        private readonly ScoreBoard board;
        private readonly SkipPolicy policy;
        private readonly bool diagnostic;
        private readonly Label title, message, mode;
        private readonly Button skip,cameraButton;
        private readonly System.Windows.Forms.Timer countdownTimer;
        private readonly Func<double> monotonicSeconds;
        private double? countdownDeadline;
        private bool manualSession;
        private readonly SoundPlayer alert = new SoundPlayer(Path.Combine(Program.AppDir,"assets","alert.wav"));
        private string interruptedSession;
        private readonly ToolTip skipTip=new ToolTip();
        private readonly TableLayoutPanel cameraRow;
        private readonly PictureBox Ads;
        private readonly StaticAds catalog;
        private int adGeneration;
        internal readonly CameraView Video;
        internal readonly SquatCounter Counter;
        private readonly System.Windows.Forms.Timer frames, completion, foreground;
        private readonly List<CoverForm> covers = new List<CoverForm>();
        private CameraPipeline pipeline;
        private bool cameraStarted, settled, allowClose, screenHooks;
        private string session;
        internal bool Active { get; private set; }
        internal bool AskingDialog;
        internal event Action GroupEnded;
        internal event Action<string> Notification;
        internal event Action DisableCameraRequested;
        internal bool ManualSession {get{return manualSession;}}
        internal bool WorkerAlive { get { return pipeline != null && pipeline.IsAlive; } }
        internal bool Settled { get { return settled; } }

        internal LockForm(Config config, ScoreBoard board, SkipPolicy policy, bool diagnostic, StaticAds catalog=null,Func<double> monotonicSeconds=null)
        {
            this.monotonicSeconds=monotonicSeconds??delegate{return (double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;};
            this.config = config; this.board = board; this.policy = policy; this.diagnostic = diagnostic; this.catalog=catalog;
            Text = Strings.ProductName; BackColor = Theme.Background; ForeColor = Theme.Ink; AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = diagnostic ? FormBorderStyle.FixedSingle : FormBorderStyle.None;
            ShowInTaskbar = false; TopMost = !diagnostic; StartPosition = FormStartPosition.Manual;
            KeyPreview = true; Counter = new SquatCounter(config);
            float s=Theme.Scale;
            title = Theme.Label("", 32, true);title.Font=new Font("Segoe UI",32,FontStyle.Bold);title.TextAlign=ContentAlignment.MiddleRight;title.AutoSize=false;
            message = Theme.Label("", 11, false);message.ForeColor=Theme.Muted;message.TextAlign=ContentAlignment.MiddleLeft;message.AutoSize=false;
            mode = Theme.Label("", 26, true);mode.TextAlign=ContentAlignment.MiddleLeft;mode.AutoSize=false;
            message.Visible=false;
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding((int)(24*s),(int)(8*s),(int)(24*s),(int)(8*s)), ColumnCount = 1, RowCount = 4 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,32*s));root.RowStyles.Add(new RowStyle(SizeType.Absolute,64*s));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,12*s));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            TableLayoutPanel brand=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};
            brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));brand.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,104*s));brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,92*s));
            brand.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            Label wordmark=Theme.Label(Strings.ProductName,11,true);wordmark.TextAlign=ContentAlignment.MiddleLeft;
            Bitmap brandImage;
            using(Icon brandIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath))brandImage=brandIcon.ToBitmap();
            wordmark.Paint+=delegate(object sender,PaintEventArgs e){int size=Math.Min(32,wordmark.Height-4);e.Graphics.DrawImage(brandImage,new Rectangle(0,(wordmark.Height-size)/2,size,size));};
            wordmark.Padding=new Padding(40,0,0,0);
            Disposed+=delegate{brandImage.Dispose();};
            Label countCaption=Theme.Label(Strings.SetCaption,9,false);countCaption.TextAlign=ContentAlignment.MiddleRight;countCaption.ForeColor=Theme.Muted;
            countCaption.Margin=new Padding(3,0,(int)(16*s),0);
            skip=Theme.Button(Strings.SkipButton,false);skip.Font=Theme.Font(9,false);skip.Anchor=AnchorStyles.Right;skip.AutoSize=false;skip.Name="SkipGroup";skip.Margin=Padding.Empty;
            skip.MinimumSize=new Size((int)(92*s),(int)(30*s));skip.Size=skip.MinimumSize;
            skip.Click+=delegate{RequestSkip();};
            cameraButton=Theme.Button(Strings.CameraOffButton,false);cameraButton.Font=Theme.Font(9,false);cameraButton.Anchor=AnchorStyles.Right;cameraButton.AutoSize=false;cameraButton.Name="DisableCamera";cameraButton.Margin=new Padding(0,0,(int)(12*s),0);
            cameraButton.MinimumSize=skip.MinimumSize;cameraButton.Size=skip.Size;
            cameraButton.Click+=delegate{if(DisableCameraRequested!=null)DisableCameraRequested();};
            brand.Controls.Add(wordmark,0,0);brand.Controls.Add(countCaption,1,0);brand.Controls.Add(cameraButton,2,0);brand.Controls.Add(skip,3,0);root.Controls.Add(brand,0,0);
            TableLayoutPanel hero=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,72));hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,28));
            TableLayoutPanel introduction=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=Padding.Empty};
            introduction.RowStyles.Add(new RowStyle(SizeType.Percent,100));introduction.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            message.AutoSize=true;
            introduction.Controls.Add(mode,0,0);introduction.Controls.Add(message,0,1);hero.Controls.Add(introduction,0,0);
            title.Margin=new Padding((int)(24*s),0,0,0);
            hero.Controls.Add(title,1,0);root.Controls.Add(hero,0,1);
            cameraRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            cameraRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28)); cameraRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
            cameraRow.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            Ads = new AdPosterBox { Dock = DockStyle.Fill, Margin = new Padding(0,0,(int)(18*s),0) };
            Video = new CameraView(); cameraRow.Controls.Add(Ads,0,0);
            cameraRow.Controls.Add(Video,1,0);
            Video.Margin=Padding.Empty;root.Controls.Add(cameraRow,0,3);
            Controls.Add(root);
            // Keep the exercise area usable on compact screens and at high display scaling.
            float previousScale=-1;
            SizeChanged+=delegate
            {
                float fit=Math.Max(.65f,Math.Min(s,Math.Min(ClientSize.Width/960f,ClientSize.Height/680f)));
                if(Math.Abs(previousScale-fit)<.01f)return;previousScale=fit;root.SuspendLayout();
                root.Padding=new Padding((int)(24*fit),(int)(8*fit),(int)(24*fit),(int)(8*fit));
                root.RowStyles[0].Height=32*fit;root.RowStyles[1].Height=(message.Visible?104:64)*fit;root.RowStyles[2].Height=12*fit;
                title.Margin=new Padding((int)(24*fit),0,0,0);
                message.MaximumSize=new Size((int)(ClientSize.Width*.65),0);
                Ads.Margin=new Padding(0,0,(int)(18*fit),0);Video.UiScale=fit;
                Control[] labels={wordmark,message,countCaption,title,mode,skip,Video,cameraButton};
                float[] sizes={11,11,9,32,26,9,16,9};
                for(int i=0;i<labels.Length;i++)
                {
                    Font old=labels[i].Font;
                    labels[i].Font=new Font(old.FontFamily,sizes[i]*fit/s,old.Style);old.Dispose();
                }
                brand.ColumnStyles[2].Width=104*fit;brand.ColumnStyles[3].Width=92*fit;countCaption.Margin=new Padding(3,0,(int)(16*fit),0);
                skip.MinimumSize=new Size((int)(92*fit),(int)(30*fit));skip.Size=skip.MinimumSize;
                cameraButton.MinimumSize=skip.MinimumSize;cameraButton.Size=skip.Size;cameraButton.Margin=new Padding(0,0,(int)(12*fit),0);
                root.ResumeLayout(true);Video.Invalidate();Ads.Invalidate();
            };
            message.VisibleChanged+=delegate {root.RowStyles[1].Height=(message.Visible?104:64)*(previousScale>0?previousScale:s);};
            frames = new System.Windows.Forms.Timer { Interval = 33 }; frames.Tick += delegate { NextFrame(); };
            countdownTimer=new System.Windows.Forms.Timer {Interval=100};countdownTimer.Tick+=delegate{UpdateCountdown();};
            completion = new System.Windows.Forms.Timer { Interval = 1500 }; completion.Tick += delegate { completion.Stop(); EndGroup(); };
            foreground = new System.Windows.Forms.Timer { Interval = 700 }; foreground.Tick += delegate
            {
                if (Active && !AskingDialog && !diagnostic) { TopMost = true; BringToFront(); Activate(); }
            };
        }

        internal void StartGroup()
        {
            if (Active) return;
            if (!ClearInterruptedSession()) return;
            manualSession=!config.CameraEnabled;
            try { session = board.BeginSession(manualSession?"manual":"auto",config.Int("target_reps",10)); }
            catch (Exception error) { Inform(Strings.StartRecordFailed + error.Message); return; }
            settled = false; Active = true; cameraStarted = false; Counter.Reset();
            SetPoster(null);
            completion.Stop();countdownTimer.Stop();countdownDeadline=null;Video.Countdown=null; Video.SetFrame(null); Video.Text = Strings.OpeningCamera;
            title.ForeColor=Theme.Accent;title.Text = "0 / " + config.Int("target_reps",10);
            message.Text = "";message.Visible=false;
            mode.Text = Strings.StandStill; UpdateStatus();
            cameraButton.Visible=!manualSession;cameraButton.Enabled=true;
            if(manualSession)RenderManual();
            if (diagnostic) { ClientSize = new Size((int)(1080*Theme.Scale),(int)(720*Theme.Scale)); Location = new Point(-20000,-20000); }
            else { Bounds = Screen.PrimaryScreen.Bounds; RebuildCovers(); AttachScreens(); }
            Show(); if (!diagnostic) Activate();
            EnsureGroupAd();
            if(!manualSession)frames.Start(); if (!diagnostic) foreground.Start();
            if (!diagnostic) try { alert.Play(); } catch { }
        }

        private void StartCameraWhenReady()
        {
            if (pipeline != null && pipeline.IsAlive) { Video.Text = Strings.ClosingCamera; return; }
            if (pipeline != null) pipeline.Dispose();
            pipeline = new CameraPipeline(config.Int("camera_index",0), Path.Combine(Program.AppDir,"assets","pose_landmarker_lite.task"), diagnostic, config.Number("min_visibility",.5));
            cameraStarted = true; pipeline.Start();
        }
        private void NextFrame()
        {
            if (!Active || settled || manualSession || !config.CameraEnabled) return;
            if (!cameraStarted) { StartCameraWhenReady(); return; }
            if (!String.IsNullOrEmpty(pipeline.Error)) { mode.Text=Strings.CameraUnavailable;message.Text = pipeline.Error;message.Visible=true; Video.Text = Strings.CameraUnavailable; Video.Invalidate(); }
            using (FramePacket packet = pipeline.TakeLatest())
            {
                if (packet == null) return;
                bool completedOne = Counter.UpdateLandmarks(packet.Landmarks);
                message.Text="";message.Visible=false;
                if (completedOne) { title.Text = Counter.Count + " / " + config.Int("target_reps",10); }
                if (Counter.TrackingLost||Counter.OutOfFrame) mode.Text=Strings.StayInFrame;
                else if (packet.Landmarks == null) mode.Text=Strings.EnterFrame;
                else if (Counter.Calibrating) mode.Text=Strings.Calibrating;
                else if (Counter.HalfSquat) { mode.Text=Strings.ShallowRep;Counter.ResetHalf(); }
                else if (Counter.State=="down") mode.Text=Strings.SquatDetected;
                else mode.Text=Counter.Ready?Strings.ReadyToSquat:Strings.StandStill;
                Video.SetFrame(packet.Image); packet.Image = null;
                TryComplete();
            }
        }
        internal bool TryComplete()
        {
            if (!Active || settled || manualSession || Counter.Count < config.Int("target_reps",10)) return false;
            return CompleteGroup();
        }
        internal bool CompleteManual()
        {
            if(!Active||settled||!manualSession||!countdownDeadline.HasValue||monotonicSeconds()<countdownDeadline.Value)return false;
            return CompleteGroup();
        }
        private bool CompleteGroup()
        {
            if (!Settle("complete")) return false;
            title.Text = Strings.Done;title.ForeColor=Theme.Green;message.Text="";message.Visible=false;mode.Text=manualSession?Strings.TimerFinished:Strings.SetFinished; UpdateStatus();
            cameraButton.Enabled=false;if (pipeline != null) pipeline.Stop(); frames.Stop();
            if(manualSession){countdownTimer.Stop();EndGroup();}else completion.Start();
            Inform(manualSession?Strings.TimerUnlocked:Strings.SetUnlocked); return true;
        }
        private void RenderManual()
        {
            cameraButton.Visible=false;
            Video.SetFrame(null);Video.Text=Strings.CameraOff;
            mode.Text=Strings.BreakPrompt;message.Text=Counter.Count>0?Strings.PartialCountPrefix+Counter.Count+Strings.PartialCountSuffix:Strings.TimerUnlockHint;message.Visible=true;
            title.Text=Strings.TwentySeconds;
            if(!countdownDeadline.HasValue)countdownDeadline=monotonicSeconds()+20;
            Video.Countdown=20;Video.Invalidate();countdownTimer.Start();
        }
        internal void UpdateCountdown()
        {
            if(!Active||settled||!manualSession||!countdownDeadline.HasValue)return;
            Video.Countdown=Math.Max(0,(int)Math.Ceiling(countdownDeadline.Value-monotonicSeconds()));Video.Invalidate();
            if(Video.Countdown.Value==0)CompleteManual();
        }
        internal void ApplyCameraPreference()
        {
            if(config.CameraEnabled||!Active||settled)return;
            // Countdown sessions never claim camera-verified repetitions.
            manualSession=true;frames.Stop();if(pipeline!=null)pipeline.Stop();RenderManual();board.MarkManual();
        }
        private bool Settle(string outcome)
        {
            if (settled) return true;
            if (!Active || String.IsNullOrEmpty(session)) return false;
            try
            {
                if(manualSession)board.MarkManual();
                if (!board.FinishSession(session,outcome,manualSession?(int?)null:Counter.Count)) return false;
                settled = true;
                try { if (outcome == "complete") policy.RecordComplete(); else policy.RecordSkip(); }
                catch (Exception ex) { Program.Log(ex.ToString()); }
                return true;
            }
            catch (Exception error)
            {
                Program.Log(error.ToString()); message.Text = Strings.SaveFailed;message.Visible=true; return false;
            }
        }
        internal bool AbortSession()
        {
            if (Active && !settled && !Settle("exit")) return false;
            EndGroup(); return true;
        }
        internal void InterruptForSystemInactivity()
        {
            if(Active&&!settled)interruptedSession=session;
            // Release devices and hide all windows even if saving fails.
            EndGroup();ClearInterruptedSession();
        }
        private bool ClearInterruptedSession()
        {
            if(interruptedSession==null)return true;
            try
            {
                board.CancelForSystemInactivity(interruptedSession);
                interruptedSession=null;return true;
            }
            catch(Exception error){Program.Log(error.ToString());return false;}
        }
        internal bool FinishSkip(string outcome)
        {
            if (!Active || settled) return false;
            if (outcome != "password" && outcome != "challenge") throw new ArgumentException("Invalid skip outcome");
            if (outcome == "password" && !policy.CanPasswordSkip) return false;
            if (!Settle(outcome)) return false;
            Inform(Strings.Skipped); EndGroup(); return true;
        }
        private void RequestSkip()
        {
            if (!Active || settled || AskingDialog) return;
            AskingDialog = true;
            try
            {
                if (policy.CanPasswordSkip)
                {
                    using (InputDialog dialog = new InputDialog(Strings.SkipTitle, config.PasswordOk("1234") ? Strings.DefaultSkipPassword : Strings.CustomSkipPassword, true))
                    {
                        if (dialog.ShowDialog(this) != DialogResult.OK || !Active || settled) return;
                        if (!config.PasswordOk(dialog.Value)) { Inform(Strings.WrongPassword); return; }
                        FinishSkip("password");
                    }
                }
                else using (AlphabetDialog dialog = new AlphabetDialog())
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK && Active && !settled) FinishSkip("challenge");
                }
            }
            finally { AskingDialog = false; }
        }
        private void EndGroup()
        {
            bool wasActive = Active; Active = false;
            alert.Stop();
            adGeneration++;
            SetPoster(null);
            frames.Stop(); completion.Stop(); foreground.Stop();countdownTimer.Stop();countdownDeadline=null; if (pipeline != null) pipeline.Stop();
            foreach (Form dialog in OwnedForms)
            {
                if (!dialog.IsDisposed) { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); }
            }
            Hide(); ClearCovers(); DetachScreens();
            if (wasActive && GroupEnded != null) GroupEnded();
        }
        private void SetPoster(Image image)
        {
            var previous = Ads.Image;
            Ads.Image = image;
            Ads.AccessibleName = image == null ? Strings.BreakPrompt : Strings.Poster;
            if (previous != null) previous.Dispose();
        }
        private async void EnsureGroupAd()
        {
            if (catalog == null) return;
            int generation = ++adGeneration;
            try
            {
                Image image = await catalog.LoadAsync();
                if (IsDisposed || !Active || generation != adGeneration)
                {
                    if (image != null) image.Dispose();
                    return;
                }
                SetPoster(image);
            }
            catch (Exception ex) { Program.Log(Strings.PosterUnavailable + ex.Message); }
        }
        private void UpdateStatus()
        {
            string hint = (policy.CanPasswordSkip ? Strings.PasswordSkipTip : Strings.ChallengeSkipTip) +
                Strings.SkipHotkey;
            skip.AccessibleDescription=hint;skipTip.SetToolTip(skip,hint);
        }
        private void Inform(string text) { if (Notification != null) Notification(text); }
        private void RebuildCovers()
        {
            ClearCovers();
            if (!Active || diagnostic) return;
            Bounds = Screen.PrimaryScreen.Bounds;
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.Primary) continue;
                CoverForm cover = new CoverForm { Bounds = screen.Bounds }; covers.Add(cover); cover.Show();
            }
        }
        private void ClearCovers()
        {
            foreach (CoverForm cover in covers) { cover.AllowClose = true; cover.Close(); cover.Dispose(); }
            covers.Clear();
        }
        private void AttachScreens() { if (!screenHooks) { SystemEvents.DisplaySettingsChanged += ScreensChanged; screenHooks = true; } }
        private void DetachScreens() { if (screenHooks) { SystemEvents.DisplaySettingsChanged -= ScreensChanged; screenHooks = false; } }
        private void ScreensChanged(object sender, EventArgs args)
        {
            if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { if (Active) RebuildCovers(); });
        }
        internal void WaitForCamera(int milliseconds) { if (pipeline != null) pipeline.Wait(milliseconds); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Shift | Keys.Alt | Keys.Q)) { RequestSkip(); return true; }
            if (keyData == Keys.Escape || keyData == (Keys.Alt | Keys.F4)) return true;
            return base.ProcessCmdKey(ref msg,keyData);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowClose && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            else if (Active) AbortSession();
            base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                allowClose = true; if (Active) AbortSession();
                frames.Dispose(); completion.Dispose(); foreground.Dispose();countdownTimer.Dispose();skipTip.Dispose();alert.Dispose(); ClearCovers(); DetachScreens();
                if (pipeline != null) { pipeline.Stop(); pipeline.Wait(2000); pipeline.Dispose(); }
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class TrayApplication : ApplicationContext
    {
        private readonly Config config;
        private readonly bool demo, diagnostic;
        private readonly NotifyIcon tray;
        private readonly Icon trayIcon;
        private readonly ToolStripMenuItem cameraMenu;
        private readonly StaticAds catalog;
        private readonly ToolStripMenuItem updateMenu;
        private readonly ToolStripMenuItem pauseMenu, pauseManual, pause30, pause60, resumeMenu;
        internal readonly ReminderSchedule Reminders;
        internal readonly SystemActivityState SystemActivity = new SystemActivityState();
        private readonly SystemActivityMonitor activityMonitor;
        private readonly UpdateService updates;
        private bool disposed;
        private readonly System.Windows.Forms.Timer tick;
        private readonly Dictionary<int,ToolStripMenuItem> intervals = new Dictionary<int,ToolStripMenuItem>();
        internal readonly ContextMenuStrip Menu;
        internal readonly ScoreBoard Board;
        internal readonly SkipPolicy Policy;
        internal readonly LockForm Window;
        internal bool StartRequested;

        internal TrayApplication(Config config, StateStore store, bool demo, bool diagnostic, ReminderSchedule reminderSchedule=null)
        {
            this.config = config; this.demo = demo; this.diagnostic = diagnostic;
            Reminders=reminderSchedule??new ReminderSchedule(demo?config.Int("demo_interval_seconds",30):config.Int("interval_minutes",20)*60);
            catalog = diagnostic ? null : new StaticAds();
            updates = diagnostic ? null : new UpdateService();
            Board = new ScoreBoard(store);
            Policy = new SkipPolicy(store,config.Int("max_consecutive_skips",1));
            Window = new LockForm(config,Board,Policy,diagnostic,catalog); Window.GroupEnded += RestartTimer; Window.Notification += Notify;
            Window.DisableCameraRequested+=ToggleCamera;
            Menu = new ContextMenuStrip { Font = Theme.Font(10,false),Padding=new Padding(6) };
            Menu.Items.Add(Strings.StartNow,null,delegate { StartGroup(); });
            cameraMenu=new ToolStripMenuItem();cameraMenu.Name="CameraToggle";cameraMenu.Click+=delegate{ToggleCamera();};Menu.Items.Add(cameraMenu);
            ToolStripMenuItem reminders = new ToolStripMenuItem(Strings.ReminderInterval);
            foreach (int minutes in new int[] {20,25,30,45})
            {
                int captured = minutes;
                ToolStripMenuItem item = new ToolStripMenuItem(minutes + Strings.Minutes) { Checked = minutes == config.Int("interval_minutes",20), Enabled = !demo };
                item.Click += delegate { SetInterval(captured); }; intervals.Add(minutes,item); reminders.DropDownItems.Add(item);
            }
            Menu.Items.Add(reminders);
            pauseMenu=new ToolStripMenuItem(Strings.PauseReminders);
            pauseManual=new ToolStripMenuItem(Strings.PauseUntilRestart,null,delegate{PauseReminders(null);});
            pause30=new ToolStripMenuItem(Strings.PauseHalfHour,null,delegate{PauseReminders(30);});
            pause60=new ToolStripMenuItem(Strings.PauseHour,null,delegate{PauseReminders(60);});
            resumeMenu=new ToolStripMenuItem(Strings.ResumeReminders,null,delegate{ResumeReminders();});
            pauseMenu.DropDownItems.AddRange(new ToolStripItem[]{pauseManual,pause30,pause60,new ToolStripSeparator(),resumeMenu});
            Menu.Items.Add(pauseMenu);Menu.Opening+=delegate{RefreshReminderUi();};
            var startup=new ToolStripMenuItem(Strings.StartWithWindows){Checked=!diagnostic&&Program.AutostartEnabled(),Enabled=!diagnostic};
            startup.Click+=delegate
            {
                bool enabled=!startup.Checked;
                try{Program.SetAutostartPreference(enabled,store);startup.Checked=enabled;Notify(enabled?Strings.StartupOn:Strings.StartupOff);}
                catch(Exception){Notify(Strings.StartupSaveFailed);}
            };
            Menu.Items.Add(startup);
            Menu.Items.Add(Strings.Feedback,null,delegate{ShowFeedback();});
            updateMenu=new ToolStripMenuItem(Strings.CheckUpdates){Enabled=!diagnostic};updateMenu.Click+=delegate{CheckUpdate();};Menu.Items.Add(updateMenu);
            Menu.Items.Add(Strings.Quit,null,delegate { QuitWithPassword(); });
            new TrayMenuRenderer().Apply(Menu);
            trayIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            tray = new NotifyIcon { Icon = trayIcon, Text = Strings.ProductName, ContextMenuStrip = Menu, Visible = !diagnostic };
            tick = new System.Windows.Forms.Timer { Interval = 1000 }; tick.Tick += delegate { Tick(); }; RestartTimer(); if (!diagnostic) tick.Start();
            if (Board.RecoveredInterruption) Notify(Strings.Recovered);
            SystemActivity.Changed+=SystemActivityChanged;
            if(!diagnostic)
            {
                activityMonitor=new SystemActivityMonitor(SystemActivity);
            }
        }
        private void ToggleCamera()
        {
            if(Window.AskingDialog)return;
            Window.AskingDialog=true;
            try
            {
                bool enabled=!config.CameraEnabled;
                if(!enabled&&MessageBox.Show(Window.Active?Window:null,
                    Strings.CameraSwitchBody,
                    Strings.CameraSwitchTitle,MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
                config.SaveCameraEnabled(enabled);
                Window.ApplyCameraPreference();RefreshReminderUi();
                Notify(enabled?(Window.Active?Strings.CameraNextSet:Strings.CameraEnabled):Strings.TimerEnabled);
            }
            catch(Exception error){Program.Log(error.ToString());Notify(Strings.ModeSaveFailed);}
            finally{Window.AskingDialog=false;}
        }
        internal void StartGroup() { if (!Reminders.IsSystemInactive&&!Window.Active&&!Window.AskingDialog){Window.StartGroup();RefreshReminderUi();} }
        private void SystemActivityChanged(bool inactive)
        {
            if(disposed)return;
            Reminders.SetSystemInactive(inactive);StartRequested=false;
            if(inactive){tick.Stop();Window.InterruptForSystemInactivity();}
            else if(!diagnostic)tick.Start();
            RefreshReminderUi();
            Program.Log(inactive?Strings.SystemPaused:Strings.SystemResumed);
        }
        internal void ShowInstalledNotice(){Notify(Strings.InstalledNotice);}
        internal void ShowUpdatedNotice(){Notify(Strings.UpdatedNotice);}
        internal bool PauseReminders(int? minutes)
        {
            if(Window.Active||Window.AskingDialog){Notify(Strings.FinishBeforePause);return false;}
            try
            {
                Reminders.Pause(minutes);RefreshReminderUi();
                Notify(minutes.HasValue?Strings.PausedFor+minutes.Value+Strings.PausedUntil:Strings.ManualPause);return true;
            }
            catch(Exception error){Program.Log(error.ToString());Notify(Strings.PauseFailed);return false;}
        }
        internal bool ResumeReminders()
        {
            try{Reminders.Resume();RefreshReminderUi();Notify(Strings.ResumedIn+config.Int("interval_minutes",20)+Strings.ReminderMinutes);return true;}
            catch(Exception error){Program.Log(error.ToString());Notify(Strings.ResumeFailed);return false;}
        }
        private void RefreshReminderUi()
        {
            if(disposed)return;
            cameraMenu.Text=config.CameraEnabled?Strings.DisableCamera:Strings.EnableCamera;
            pauseMenu.Text=Reminders.IsPaused?Strings.Paused:Strings.PauseReminders;
            pauseMenu.Enabled=!Window.Active&&!Window.AskingDialog;
            pauseManual.Checked=Reminders.IsPaused&&!Reminders.PauseUntilUtc.HasValue;
            pause30.Checked=Reminders.IsPaused&&Reminders.PauseMinutes==30;
            pause60.Checked=Reminders.IsPaused&&Reminders.PauseMinutes==60;
            resumeMenu.Enabled=Reminders.IsPaused;
            string text;
            if(Reminders.IsSystemInactive)text=Strings.InactiveTip;
            else if(Window.Active)text=Strings.ExercisingTip;
            else if(Reminders.IsPaused)
            {
                long remaining=Reminders.PauseSecondsRemaining;
                text=Reminders.PauseUntilUtc.HasValue?Strings.TimedPauseTip+(remaining/60)+Strings.MinuteUnit+(remaining%60)+Strings.ResumeSeconds:Strings.PausedTip;
            }
            else{long remaining=Reminders.SecondsRemaining;text=Strings.NextTip+(remaining/60)+Strings.MinuteUnit+(remaining%60)+Strings.NextSeconds;}
            text+=config.CameraEnabled?Strings.TrackingTip:Strings.TimerTip;tray.Text=text.Substring(0,Math.Min(63,text.Length));
        }
        private void RestartTimer()
        {
            int seconds = demo ? config.Int("demo_interval_seconds",30) : config.Int("interval_minutes",20) * 60;
            Reminders.SetInterval(seconds);RefreshReminderUi();
        }
        internal void Tick()
        {
            try
            {
                if(Reminders.IsSystemInactive){StartRequested=false;return;}
                if (StartRequested) { StartRequested = false; StartGroup(); }
                Board.EnsureDay();
                if(Reminders.ResumeIfExpired())Notify(Strings.AutoResumed+config.Int("interval_minutes",20)+Strings.MinutesPeriod);
                if (!Reminders.IsPaused && !Window.Active && !Window.AskingDialog && Reminders.SecondsRemaining <= 0) { StartGroup(); if (!Window.Active) RestartTimer(); }
                RefreshReminderUi();
            }
            catch (Exception error) { Program.Log(error.ToString()); }
        }
        private void SetInterval(int minutes)
        {
            if (demo) return;
            try { config.SaveInterval(minutes); foreach (KeyValuePair<int,ToolStripMenuItem> item in intervals) item.Value.Checked = item.Key == minutes;
                if (!Window.Active) RestartTimer(); Notify(Strings.IntervalSet + minutes + Strings.Minutes); }
            catch (Exception error) { Program.Log(error.ToString()); Notify(Strings.IntervalFailed); }
        }
        private void ShowFeedback()
        {
            if(Window.Active||Window.AskingDialog){Notify(Strings.FinishBeforeFeedback);return;}
#if ENGLISH
            try { Process.Start(Edition.FeedbackRequest()); }
            catch (Exception) { MessageBox.Show("Open this address in your browser:\n\n" + Edition.FeedbackUrl, Strings.Feedback); }
#else
            WithDialog(delegate{using(var dialog=new FeedbackDialog())dialog.ShowDialog();});
#endif
        }
        private async void CheckUpdate()
        {
            if(disposed||diagnostic||!updateMenu.Enabled)return;
            if(Window.Active||Window.AskingDialog){Notify(Strings.FinishBeforeUpdate);return;}
            updateMenu.Enabled=false;updateMenu.Text=Strings.Checking;
            Window.AskingDialog=true;
            try
            {
                UpdateInfo info=await updates.CheckAsync();if(disposed)return;
                if(!info.IsNewer(Program.Version,Program.BuildNumber))
                {
                    MessageBox.Show(Strings.UpToDate+Program.Version,Strings.CheckUpdates,MessageBoxButtons.OK,MessageBoxIcon.Information);return;
                }
                UpdatePackage package=await Task.Run(delegate{return info.SelectPackage(Program.AppDir);});if(disposed)return;
                if(package==null)throw new UpdateException(Strings.UpdateNotReady);
                string description=Strings.NewVersion+info.Version+Strings.CurrentVersion+Program.Version;
                if(info.Version==Program.Version)description+=Strings.NewBuild;
                if(!String.IsNullOrEmpty(info.Notes))description+="\n\n"+info.Notes;
                description+="\n\n"+(package.Incremental?Strings.PatchUpdate:Strings.FullUpdate)+Strings.DownloadSize+(package.Size<1000000?(package.Size/1000d).ToString("F0")+" KB":(package.Size/1000000d).ToString("F1")+" MB")+Strings.UpdateConfirm;
                if(MessageBox.Show(description,Strings.CheckUpdates,MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
                using(var download=new UpdateDownloadDialog(updates,package))
                {
                    if(download.ShowDialog()!=DialogResult.OK||disposed)return;
                    if(!await Task.Run(delegate{return package.Matches(Program.AppDir);}))throw new UpdateException(Strings.LocalFilesChanged);
                    bool portable=Program.IsPortable();
                    string arguments="/S /INAPPUPDATE="+Process.GetCurrentProcess().Id+(portable?" /PORTABLE":"")+" /D="+Program.AppDir.TrimEnd(Path.DirectorySeparatorChar);
                    Process.Start(new ProcessStartInfo(download.DownloadedPath,arguments){UseShellExecute=false,WorkingDirectory=Program.AppDir});
                    tick.Stop();tray.Visible=false;ExitThread();
                }
            }
            catch(OperationCanceledException){if(!disposed)MessageBox.Show(Strings.UpdateTimeout,Strings.CheckUpdates,MessageBoxButtons.OK,MessageBoxIcon.Information);}
            catch(UpdateException error){if(!disposed)MessageBox.Show(error.Message,Strings.CheckUpdates,MessageBoxButtons.OK,MessageBoxIcon.Information);}
            catch{if(!disposed)MessageBox.Show(Strings.UpdateFailed,Strings.CheckUpdates,MessageBoxButtons.OK,MessageBoxIcon.Information);}
            finally{if(!disposed){Window.AskingDialog=false;updateMenu.Text=Strings.CheckUpdates;updateMenu.Enabled=true;}}
        }
        private void QuitWithPassword()
        {
            WithDialog(delegate
            {
                using (InputDialog dialog = new InputDialog(Strings.QuitTitle,config.PasswordOk("1234") ? Strings.DefaultQuitPassword : Strings.CustomQuitPassword,true))
                {
                    if (dialog.ShowDialog(Window.Active ? Window : null) != DialogResult.OK) return;
                    if (!config.PasswordOk(dialog.Value)) { Notify(Strings.WrongPassword); return; }
                    if (Window.AbortSession()) { tick.Stop(); tray.Visible = false; ExitThread(); }
                }
            });
        }
        private void WithDialog(Action action)
        {
            if (Window.AskingDialog) return;
            Window.AskingDialog = true; try { action(); } finally { Window.AskingDialog = false; }
        }
        private void Notify(string text)
        {
            Program.Log(text); if (!diagnostic) tray.ShowBalloonTip(3000,Strings.ProductName,text,ToolTipIcon.Info);
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing&&!disposed){SystemActivity.Changed-=SystemActivityChanged;if(activityMonitor!=null)activityMonitor.Dispose();}
            if (disposing&&!disposed) { disposed=true;if(updates!=null)updates.Dispose();if(catalog!=null)catalog.Dispose();tick.Stop(); tick.Dispose(); tray.Visible = false; tray.Dispose(); trayIcon.Dispose(); Menu.Dispose(); Window.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
