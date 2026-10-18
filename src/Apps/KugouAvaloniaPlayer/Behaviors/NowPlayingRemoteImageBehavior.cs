using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KugouAvaloniaPlayer.Services;

namespace KugouAvaloniaPlayer.Behaviors;

public static class NowPlayingRemoteImageBehavior
{
    private static readonly ConditionalWeakTable<Image, ImageState> States = new();

    public static readonly AttachedProperty<string?> SourceProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>(
            "Source",
            typeof(NowPlayingRemoteImageBehavior));

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Image, bool>(
            "IsEnabled",
            typeof(NowPlayingRemoteImageBehavior),
            defaultValue: true);

    /// <summary>
    ///     远程网页图片的解码宽度上限，用于限制整屏背景图的解码内存；0 表示不限制。
    /// </summary>
    public static readonly AttachedProperty<int> DecodePixelWidthProperty =
        AvaloniaProperty.RegisterAttached<Image, int>(
            "DecodePixelWidth",
            typeof(NowPlayingRemoteImageBehavior),
            defaultValue: 0);

    static NowPlayingRemoteImageBehavior()
    {
        SourceProperty.Changed.AddClassHandler<Image>(OnLoadingPropertyChanged);
        IsEnabledProperty.Changed.AddClassHandler<Image>(OnLoadingPropertyChanged);
        DecodePixelWidthProperty.Changed.AddClassHandler<Image>(OnLoadingPropertyChanged);
        Image.SourceProperty.Changed.AddClassHandler<Image>(OnImageSourceChanged);
    }

    public static string? GetSource(AvaloniaObject element) => element.GetValue(SourceProperty);

    public static void SetSource(AvaloniaObject element, string? value) =>
        element.SetValue(SourceProperty, value);

    public static bool GetIsEnabled(AvaloniaObject element) => element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(AvaloniaObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static int GetDecodePixelWidth(AvaloniaObject element) =>
        element.GetValue(DecodePixelWidthProperty);

    public static void SetDecodePixelWidth(AvaloniaObject element, int value) =>
        element.SetValue(DecodePixelWidthProperty, value);

    private static void OnLoadingPropertyChanged(Image image, AvaloniaPropertyChangedEventArgs args)
    {
        States.GetValue(image, static owner => new ImageState(owner)).Reload();
    }

    private static void OnImageSourceChanged(Image image, AvaloniaPropertyChangedEventArgs args)
    {
        if (States.TryGetValue(image, out var state))
            state.TrackAppliedBitmap(args.GetNewValue<Avalonia.Media.IImage?>() as Bitmap);
    }

    private sealed class ImageState
    {
        private readonly Image _image;
        private Bitmap? _ownedBitmap;
        private int _loadVersion;
        private bool _isAttached;

        public ImageState(Image image)
        {
            _image = image;
            _isAttached = image.IsAttachedToVisualTree();
            image.AttachedToVisualTree += OnAttachedToVisualTree;
            image.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        public void Reload()
        {
            unchecked
            {
                _loadVersion++;
            }

            Release();

            if (!_isAttached || !GetIsEnabled(_image))
                return;

            var source = GetSource(_image);
            if (string.IsNullOrWhiteSpace(source))
                return;

            _ = LoadAndApplyAsync(source, GetDecodePixelWidth(_image), _loadVersion);
        }

        public void TrackAppliedBitmap(Bitmap? bitmap)
        {
            if (bitmap == null ||
                LocalArtworkImageBehavior.OwnsBitmap(_image, bitmap) ||
                ReferenceEquals(_ownedBitmap, bitmap))
                return;

            // 位图由本行为之外的写入方应用到控件上，这里接管其生命周期，
            // 确保释放时（Release）一定会被 Dispose，而不是依赖其他组件。
            var previous = _ownedBitmap;
            _ownedBitmap = bitmap;
            previous?.Dispose();
        }

        private async Task LoadAndApplyAsync(string source, int decodePixelWidth, int version)
        {
            Bitmap? bitmap = null;
            try
            {
                var loader = ImageLoader.AsyncImageLoader;
                bitmap = loader is BoundedDiskCachedWebImageLoader boundedLoader
                    ? await boundedLoader.ProvideImageAsync(source, decodePixelWidth).ConfigureAwait(false)
                    : await loader.ProvideImageAsync(source).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // 加载失败时保持上一帧画面，与原 AsyncImageLoader 行为一致。
            }

            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (version != _loadVersion || !_isAttached || !GetIsEnabled(_image))
                    {
                        bitmap?.Dispose();
                        bitmap = null;
                        return;
                    }

                    ApplyOwnedBitmap(bitmap);
                    bitmap = null;
                });
            }
            catch
            {
                bitmap?.Dispose();
            }
        }

        private void ApplyOwnedBitmap(Bitmap? bitmap)
        {
            if (bitmap is null)
                return;

            var previous = _ownedBitmap;
            _ownedBitmap = bitmap;
            _image.Source = bitmap;
            previous?.Dispose();
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
        {
            _isAttached = true;
            Reload();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
        {
            _isAttached = false;
            unchecked
            {
                _loadVersion++;
            }

            Release();
        }

        private void Release()
        {
            var bitmap = _ownedBitmap;
            _ownedBitmap = null;

            if (bitmap == null)
                return;

            if (ReferenceEquals(_image.Source, bitmap))
                _image.Source = null;

            bitmap.Dispose();
        }
    }
}
