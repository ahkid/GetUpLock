using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SquatLockTiny
{
    internal sealed class FeedbackDialog : Form
    {
        private readonly Bitmap code;

        internal FeedbackDialog()
        {
            Text = Strings.Feedback;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            BackColor = Color.White;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(380, 420);
            Padding = new Padding(24);
            using (Stream input = typeof(FeedbackDialog).Assembly.GetManifestResourceStream("feedback-wechat.png"))
            using (Image original = Image.FromStream(input))
                code = new Bitmap(original);

            var picture = new PictureBox {
                Image = code, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill,
                AccessibleName = Strings.WeChatCode, BackColor = Color.White
            };
            var caption = new Label {
                Text = Strings.ScanFeedback, Dock = DockStyle.Bottom,
                Height = 46, TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Font(11, false)
            };
            Controls.Add(picture);
            Controls.Add(caption);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && code != null) code.Dispose();
            base.Dispose(disposing);
        }
    }
}
