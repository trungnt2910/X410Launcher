using System.Linq;
using System.Threading;
using X410Launcher;
using X410Launcher.Tools;

// It is unfortunate but we have to set it to Unknown first.
Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

int retVal = 0;

if (args.Contains(Switches.NoUiSwitch))
{
    retVal = new ConsoleApp(args).Run();
}

if (args.Contains(Switches.TraySwitch) || !args.Contains(Switches.NoUiSwitch))
{
    retVal = new SingleInstanceApp(args).Run();
}

return retVal;