using CommandLine.Text;
using CommandLine;

namespace X410Launcher.Tools;

public static class Switches
{
    public const string SwitchPrefix = "--";

    public const string Silent = "silent";
    public const string SilentSwitch = $"{SwitchPrefix}{Silent}";

    public const string Update = "update";
    public const string UpdateSwitch = $"{SwitchPrefix}{Update}";

    public const string Launch = "launch";
    public const string LaunchSwitch = $"{SwitchPrefix}{Launch}";

    public const string Tray = "tray";
    public const string TraySwitch = $"{SwitchPrefix}{Tray}";

    public const string NoUi = "no-ui";
    public const string NoUiSwitch = $"{SwitchPrefix}{NoUi}";
}
