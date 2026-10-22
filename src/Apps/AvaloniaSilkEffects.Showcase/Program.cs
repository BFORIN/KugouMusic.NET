using Avalonia;
using AvaloniaSilkEffects.Gpu;

namespace AvaloniaSilkEffects.Showcase;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        SilkEffectControl.BackendSelected += (_, e) =>
            Console.WriteLine($"[effects] backend {e.Backend}{(e.FallbackReason is { } why ? $" (fallback: {why})" : "")}");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // The compositor runs on Metal unless the effects are pinned to OpenGL (AVALONIA_SILK_EFFECTS_BACKEND=opengl);
    // SHOWCASE_COMPOSITOR=metal|opengl overrides it to test mixed setups such as OpenGL effects on Metal.
    public static AppBuilder BuildAvaloniaApp()
    {
        var openGl = Environment.GetEnvironmentVariable("SHOWCASE_COMPOSITOR")?.ToLowerInvariant() switch
        {
            "opengl" => true,
            "metal" => false,
            _ => SilkEffectControl.BackendPreference == EffectGpuBackendPreference.OpenGl,
        };
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = openGl
                    ? [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.Software]
                    : [AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software],
            })
            .WithInterFont()
            .LogToTrace();
    }
}
