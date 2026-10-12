using System.Windows;
using Forms = System.Windows.Forms;

namespace WinSense.App;

/// <summary>The WinSense icon in the notification area ("ícones ocultos", next to the clock).</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private bool _hintShown;

    public TrayIcon(Action open, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir WinSense", null, (_, _) => open());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => exit());

        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/WinSense.ico"))!.Stream;
        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize),
            Text = "WinSense",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                open();
        };
    }

    /// <summary>Once per run, tells the user that closing the window did not quit WinSense.</summary>
    public void ShowStillRunningHint()
    {
        if (_hintShown)
            return;

        _hintShown = true;
        _icon.ShowBalloonTip(5000, "O WinSense continua aberto",
            "Ele fica nos ícones ocultos, perto do relógio. Para encerrar, clique com o botão direito no ícone e escolha Sair.",
            Forms.ToolTipIcon.None);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Icon?.Dispose();
        _icon.Dispose();
    }
}
