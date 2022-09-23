using System;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using X410Launcher.ViewModels;

namespace X410Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Contains("--no-ui"))
            {
                var application = new ConsoleApp(args);
                application.Run();
            }
            else
            {
                var application = new App();
                application.InitializeComponent();
                application.Run();
            }
        }
    }
}
