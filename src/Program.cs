using System;
using System.Threading;
using System.Windows.Forms;

namespace CrispMic;

static class Program
{
    private const string AppGuid = "CrispMic_SingleInstance_Mutex_8F7D6B9A";

    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, AppGuid, out bool isNewInstance);
        if (!isNewInstance)
        {
            // Another instance is already running
            return;
        }

        ApplicationConfiguration.Initialize();

        bool startMinimized = false;
        if (args.Length > 0 && args[0].Equals("--minimized", StringComparison.OrdinalIgnoreCase))
        {
            startMinimized = true;
        }

        Application.Run(new MainForm(startMinimized));
    }
}