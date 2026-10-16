using Lastlight.Desktop;
using Lastlight.Core;

try
{
    bool releaseUiChecks=args.Contains("--release-ui-checks");
    bool uiChecks = args.Contains("--ui-checks");
    bool colonyUiChecks = args.Contains("--colony-ui-checks");
    bool dangerUiChecks=args.Contains("--danger-ui-checks");
    bool solarUiChecks=args.Contains("--solar-ui-checks");
    bool institutionUiChecks=args.Contains("--institutions-ui-checks");
    bool regionUiChecks=args.Contains("--region-ui-checks");
    bool physicalUiChecks=args.Contains("--physical-ui-checks");
    bool sliceUiChecks=args.Contains("--slice-ui-checks");
    bool smoke = releaseUiChecks || solarUiChecks || institutionUiChecks || regionUiChecks || physicalUiChecks || sliceUiChecks || args.Contains("--smoke") || uiChecks || colonyUiChecks || dangerUiChecks;
    string? screenshot = null;
    int imageArg = Array.IndexOf(args, "--screenshot");
    if (imageArg >= 0 && imageArg + 1 < args.Length) screenshot = args[imageArg + 1];
    int width = args.Contains("--compact") ? 1366 : 1600;
    string? initialSave = null;
    int saveArg = Array.IndexOf(args, "--load");
    if (saveArg >= 0 && saveArg + 1 < args.Length) initialSave = Path.GetFullPath(args[saveArg + 1]);
    string initialTab = "";
    int tabArg = Array.IndexOf(args, "--tab");
    if (tabArg >= 0 && tabArg + 1 < args.Length) initialTab = args[tabArg + 1];
    float scale = 0; int scaleArg = Array.IndexOf(args, "--scale");
    if (scaleArg >= 0 && scaleArg + 1 < args.Length) scale = float.Parse(args[scaleArg + 1], System.Globalization.CultureInfo.InvariantCulture);
    using var game = new LastlightGame(smoke, screenshot, width, initialSave, initialTab, uiChecks, scale, colonyUiChecks, dangerUiChecks, sliceUiChecks, physicalUiChecks, regionUiChecks, institutionUiChecks, solarUiChecks);
    game.ReleaseUiChecks=releaseUiChecks;
    game.Run();
    return 0;
}
catch (Exception exception)
{
    string message = "LASTLIGHT could not start or continue.\n\n" + exception;
    Console.Error.WriteLine(message);
    try
    {
        Directory.CreateDirectory(SaveStore.DefaultDirectory);
        File.WriteAllText(Path.Combine(SaveStore.DefaultDirectory, "startup-error.txt"), message);
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
    return 1;
}
