using System;
using System.Windows;

namespace ResolutionChanger
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.Run(new MainWindow());
        }
    }
}
