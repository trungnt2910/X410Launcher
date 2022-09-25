using System.Linq;

namespace X410Launcher;

public class SingleInstanceApp : IRunnable
{
    private readonly App _app;
    private readonly bool _isTray;

    public SingleInstanceApp(string[] args)
    {
        _app = new();
        _app.InitializeComponent();
        _isTray = args.Contains("--tray");
    }

    public int Run()
    {
        SingleInstanceHelpers.Register("X410Launcher", _app);
        if (SingleInstanceHelpers.IsSecondaryInstance)
        {
            if (!_isTray)
            {
                SingleInstanceHelpers.ActivateFirstInstanceWindow();
            }
            return 0;
        }
        else
        {
            return _app.Run();
        }
    }
}
