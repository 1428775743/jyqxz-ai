using AutoWuxia.Forms;

namespace AutoWuxia.Forms.Controls;

public class TextLog : RichTextBox
{
    public TextLog()
    {
        ReadOnly = true;
        BackColor = WuxiaTheme.Surface;
        ForeColor = WuxiaTheme.Text;
        Font = WuxiaTheme.UiFont(10f);
        BorderStyle = BorderStyle.FixedSingle;
        ScrollBars = RichTextBoxScrollBars.Vertical;
        DetectUrls = false;
        Margin = new Padding(0);
    }

    public void AppendText(string text, Color? color = null)
    {
        var c = color ?? ForeColor;
        SelectionStart = TextLength;
        SelectionLength = 0;
        SelectionColor = c;
        base.AppendText(text + Environment.NewLine);
        ScrollToCaret();
    }

    public void AppendSystem(string text)
    {
        AppendText(text, WuxiaTheme.AccentSoft);
    }

    public void AppendCombat(string text)
    {
        AppendText(text, Color.FromArgb(238, 105, 77));
    }

    public void AppendDialogue(string text)
    {
        AppendText(text, Color.FromArgb(116, 189, 206));
    }

    public void AppendSuccess(string text)
    {
        AppendText(text, Color.FromArgb(157, 202, 112));
    }

    public void AppendWarning(string text)
    {
        AppendText(text, Color.FromArgb(255, 184, 86));
    }

    public void AppendDivider()
    {
        AppendText("────────────────────────────────", WuxiaTheme.Border);
    }
}
