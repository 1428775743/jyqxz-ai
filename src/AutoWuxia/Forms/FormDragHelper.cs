using System.Runtime.InteropServices;

namespace AutoWuxia.Forms;

/// <summary>
/// 为无边框窗体/控件提供鼠标拖动移动支持。
/// </summary>
internal static class FormDragHelper
{
    [DllImport("user32.dll")]
    private static extern int ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 2;

    /// <summary>
    /// 让指定控件在按下鼠标左键时拖动其所在顶层窗体。
    /// 适用于无边框窗体的标题栏 Panel 等不会自身处理点击的控件。
    /// 注意:会接管鼠标消息,不要挂在需要响应 Click 的控件上。
    /// </summary>
    public static void EnableDrag(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                var form = control.FindForm();
                if (form != null)
                    SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        };
    }
}
