using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaSilkEffects;
using AvaloniaSilkEffects.Lumiere;
using AvaloniaSilkEffects.Tempera;
using AvaloniaLyrics;
using KugouAvaloniaPlayer.ViewModels;

namespace KugouAvaloniaPlayer.Views.NowPlayingThemes;

/// <summary>凝彩: Folia's screentone lyric PV (flat tone compositions, hatch, type that inverts against the artwork).</summary>
public partial class TemperaNowPlayingThemeView : UserControl
{
    // Folia's default theme 「午夜墨染」 at its default Tempera weight 600; the ink follows the player's lyric colour.
    private static readonly TemperaTheme PlayerTheme = TemperaTheme.MidnightDefault with { FontFamily = ResolveFontFamily() };

    private static string ResolveFontFamily()
    {
#if KUGOU_WINDOWS
        return "Microsoft YaHei UI";
#elif KUGOU_LINUX
        return "Noto Sans CJK SC";
#elif KUGOU_MACOS
        return "PingFang SC";
#else
        return "Noto Sans CJK SC";
#endif
    }

    // Resync the GL scene clock only when it drifted from the audio position by more than this.
    private const double ClockResyncThresholdSeconds = 0.08;

    private readonly DispatcherTimer _diagnosticTimer;
    private NowPlayingViewModel? _viewModel;
    private PlayerViewModel? _player;
    private TemperaEffectScene? _scene;
    private bool _sceneBuildQueued;
    private double _lastSyncedPositionSeconds = double.NegativeInfinity;
    private long _lastSyncTimestamp;
    private bool _lastSyncPaused = true;

    public TemperaNowPlayingThemeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        EffectSurface.InitializationFailed += OnInitializationFailed;
        _diagnosticTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(400),
            DispatcherPriority.Background,
            (_, _) => RefreshDiagnostic());
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        HookViewModel();
        QueueSceneBuild();
        _diagnosticTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _diagnosticTimer.Stop();
        UnhookViewModel();
        EffectSurface.Scene = null;
        _scene = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnhookViewModel();
        HookViewModel();
        QueueSceneBuild();
    }

    private void HookViewModel()
    {
        var viewModel = DataContext as NowPlayingViewModel;
        if (ReferenceEquals(_viewModel, viewModel))
            return;

        _viewModel = viewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _player = viewModel?.Player;
        if (_player is null)
            return;

        _player.PropertyChanged += OnPlayerPropertyChanged;
        _player.RenderLyricLines.CollectionChanged += OnLyricsCollectionChanged;
        SynchronizePlaybackClock();
    }

    private void UnhookViewModel()
    {
        if (_player is not null)
        {
            _player.PropertyChanged -= OnPlayerPropertyChanged;
            _player.RenderLyricLines.CollectionChanged -= OnLyricsCollectionChanged;
        }

        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _player = null;
        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NowPlayingViewModel.LyricColor))
            QueueSceneBuild();
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.CurrentPlayingSong) or
            nameof(PlayerViewModel.DisplayedPlayingSong) or
            nameof(PlayerViewModel.TotalDurationSeconds))
        {
            QueueSceneBuild();
            return;
        }

        if (e.PropertyName is nameof(PlayerViewModel.CurrentPositionSeconds) or
            nameof(PlayerViewModel.IsPlayingAudio))
            SynchronizePlaybackClock();
    }

    private void OnLyricsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueSceneBuild();

    private void QueueSceneBuild()
    {
        if (_sceneBuildQueued || !this.IsAttachedToVisualTree())
            return;

        _sceneBuildQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _sceneBuildQueued = false;
            if (this.IsAttachedToVisualTree())
                RebuildScene();
        }, DispatcherPriority.Background);
    }

    private void RebuildScene()
    {
        var player = _player;
        if (player is null)
            return;

        var song = player.DisplayedPlayingSong ?? player.CurrentPlayingSong;
        var identity = ResolveTrackIdentity(song);
        var lines = BuildLines(player.RenderLyricLines, player.TotalDurationSeconds);
        // Like Folia, a track without lyrics runs on virtual ♪ lines so the compositions keep cutting.
        var program = TemperaProgramCompiler.Compile(lines.Count > 0 ? lines : TemperaProgramCompiler.InstrumentalLines(), identity);
        var color = _viewModel!.LyricColor;
        var theme = PlayerTheme with { Primary = new TemperaRgb(color.R, color.G, color.B) };
        var context = new TemperaSongContext(identity, program, theme,
            new TemperaSongMetadata(song?.DisplayTitle, song?.Singer, song?.AlbumName));

        if (_scene is null)
        {
            _scene = new TemperaEffectScene(context);
            EffectSurface.Scene = _scene;
        }
        else
            _scene.SetSong(context);

        SynchronizePlaybackClock();
        EffectSurface.RenderOnce();
    }

    private void SynchronizePlaybackClock()
    {
        var player = _player;
        if (player is null)
            return;

        var paused = !player.IsPlayingAudio;
        var position = Math.Max(0, player.CurrentPositionSeconds);

        // While playing, extrapolate where the scene clock should be by now and compare that against the audio position;
        // while paused the scene clock is frozen, so compare directly. First call always syncs.
        var drifted = true;
        if (_lastSyncPaused == paused && !double.IsNegativeInfinity(_lastSyncedPositionSeconds))
        {
            var expected = paused
                ? _lastSyncedPositionSeconds
                : _lastSyncedPositionSeconds + Stopwatch.GetElapsedTime(_lastSyncTimestamp).TotalSeconds;
            drifted = Math.Abs(position - expected) >= ClockResyncThresholdSeconds;
        }

        if (!drifted)
            return;

        _lastSyncPaused = paused;
        _lastSyncedPositionSeconds = position;
        _lastSyncTimestamp = Stopwatch.GetTimestamp();
        EffectSurface.IsPaused = paused;
        EffectSurface.Seek(TimeSpan.FromSeconds(position));
    }

    private void OnInitializationFailed(object? sender, EffectInitializationFailedEventArgs e)
    {
        DiagnosticText.Text = $"凝彩 OpenGL 初始化失败\n{e.Message}";
        DiagnosticOverlay.IsVisible = true;
    }

    private void RefreshDiagnostic()
    {
        if (!string.IsNullOrWhiteSpace(EffectSurface.LastError))
        {
            DiagnosticOverlay.IsVisible = true;
            DiagnosticText.Text = $"凝彩 OpenGL 渲染器不可用\n{EffectSurface.LastError}";
            return;
        }

        if (EffectSurface.FrameStatistics.SubmittedFrames > 0)
        {
            DiagnosticOverlay.IsVisible = false;
            return;
        }

        DiagnosticOverlay.IsVisible = true;
        DiagnosticText.Text =
            "凝彩尚未获得 OpenGL 渲染上下文。\n请完全退出并重新启动播放器；macOS 会按 OpenGL → Metal → Software 顺序选择后端。";
    }

    /// <summary>Player lyrics → Folia lines (word timings kept for the per-glyph entrances).</summary>
    private static IReadOnlyList<LumiereLine> BuildLines(IReadOnlyList<LyricLine> source, double durationSeconds)
    {
        var result = new List<LumiereLine>(source.Count);
        for (var index = 0; index < source.Count; index++)
        {
            var line = source[index];
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            var start = Math.Max(0, line.Start.TotalSeconds);
            var end = line.Duration > TimeSpan.Zero
                ? start + line.Duration.TotalSeconds
                : index + 1 < source.Count
                    ? Math.Max(start + 0.1, source[index + 1].Start.TotalSeconds)
                    : Math.Max(start + 4, durationSeconds);
            var words = line.Words.Select(word => new LumiereWord(
                word.Text,
                Math.Max(start, word.Start.TotalSeconds),
                Math.Max(start, word.Start.TotalSeconds) + Math.Max(0.01, word.Duration.TotalSeconds))).ToArray();
            result.Add(new LumiereLine(line.Text, start, Math.Max(start + 0.1, end), words));
        }

        return result;
    }

    private static string ResolveTrackIdentity(SongItem? song)
    {
        if (song is null)
            return "tempera:empty";
        if (!string.IsNullOrWhiteSpace(song.Hash))
            return $"kugou:{song.Hash}";
        if (!string.IsNullOrWhiteSpace(song.LocalFilePath))
            return $"local:{song.LocalFilePath}";
        if (song.AudioId != 0)
            return $"audio:{song.AudioId}";
        return $"metadata:{song.DisplayTitle}:{song.Singer}:{song.DurationSeconds:F3}";
    }
}
