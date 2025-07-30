using System.Drawing;
using System.Windows.Forms;

public class OverlayForm : Form
{
    private Image overlayImage;

    public OverlayForm()
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.WindowState = FormWindowState.Maximized;
        //this.TopMost = true;
        this.ShowInTaskbar = false;
        this.StartPosition = FormStartPosition.Manual;
        this.BackColor = Color.Black;
        this.FormBorderStyle = FormBorderStyle.None;
        this.Bounds = Screen.PrimaryScreen.Bounds;
        this.DoubleBuffered = true; // smooth drawing
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            overlayImage?.Dispose();
        }
        base.Dispose(disposing);
    }
}
