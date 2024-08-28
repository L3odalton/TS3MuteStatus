using System;
using System.Drawing;
using System.Windows.Forms;

namespace TS3MuteStatus
{
    public partial class Form1 : Form
    {
        private NotifyIcon trayIcon;

        public Form1()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;

            // Initialize Tray Icon
            trayIcon = new NotifyIcon
            {
                Icon = new Icon(@"C:\Scripts\TS3MuteStatus\Resources\mic-mute.ico"), // Pfad zu deinem .ico-Icon
                Visible = true,
                Text = "TS3MuteStatus"
            };

            // Add a context menu to the tray icon
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Exit", null, OnExit);
            trayIcon.ContextMenuStrip = trayMenu;

            trayIcon.Visible = true;
        }

        private void OnExit(object? sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            base.OnLoad(e);
        }
    }
}
