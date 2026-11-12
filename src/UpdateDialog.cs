using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SquatLockTiny
{
    internal sealed class UpdateDownloadDialog : Form
    {
        private readonly CancellationTokenSource cancel=new CancellationTokenSource();
        private readonly Label status;
        private readonly ProgressBar progress;
        private bool finished;
        internal string DownloadedPath;
        internal UpdateDownloadDialog(UpdateService service,UpdatePackage package)
        {
            Text=Strings.DownloadUpdate;Font=Theme.Font(10,false);BackColor=Theme.Background;ForeColor=Theme.Ink;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
            StartPosition=FormStartPosition.CenterScreen;ShowInTaskbar=false;TopMost=true;
            ClientSize=new Size(420,165);AutoScaleMode=AutoScaleMode.Dpi;
            status=new Label{Text=Strings.Downloading,AutoSize=false,Bounds=new Rectangle(24,20,372,32),ForeColor=Theme.Ink};
            progress=new ProgressBar{Bounds=new Rectangle(24,60,372,18),Maximum=100};
            var stop=Theme.Button(Strings.CancelDownload,false);stop.SetBounds(280,103,116,42);
            stop.Click+=delegate{stop.Enabled=false;status.Text=Strings.Cancelling;cancel.Cancel();};
            Controls.AddRange(new Control[]{status,progress,stop});
            Shown+=async delegate
            {
                string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Edition.Id,"updates",Guid.NewGuid().ToString("N"));
                try
                {
                    DownloadedPath=await service.DownloadAsync(package,directory,new Progress<int>(delegate(int value)
                    {if(!IsDisposed&&!finished){progress.Value=value;status.Text=value==100?Strings.Verifying:Strings.DownloadingProgress+value+"%";}}),cancel.Token);
                    finished=true;DialogResult=DialogResult.OK;
                }
                catch(OperationCanceledException){finished=true;DialogResult=DialogResult.Cancel;}
                catch(Exception error)
                {
                    finished=true;MessageBox.Show(error is UpdateException?error.Message:Strings.DownloadFailed,Strings.DownloadUpdate,MessageBoxButtons.OK,MessageBoxIcon.Information);
                    DialogResult=DialogResult.Cancel;
                }
                finally{Close();}
            };
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {if(!finished){e.Cancel=true;cancel.Cancel();}base.OnFormClosing(e);}
        protected override void Dispose(bool disposing){if(disposing&&!IsDisposed){cancel.Cancel();cancel.Dispose();}base.Dispose(disposing);}
    }
}
