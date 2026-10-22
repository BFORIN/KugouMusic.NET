using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AvaloniaSilkEffects.Gpu.Metal;

/// <summary>
/// The Objective-C runtime calls the Metal backend needs. Every message send is typed by its managed signature;
/// on arm64 and x86-64 none of the selectors used here return a struct, so plain objc_msgSend covers them all.
/// </summary>
[SupportedOSPlatform("macos")]
internal static unsafe partial class ObjC
{
    private const string Runtime = "/usr/lib/libobjc.A.dylib";
    private const string MetalFramework = "/System/Library/Frameworks/Metal.framework/Metal";
    private const string IOSurfaceFramework = "/System/Library/Frameworks/IOSurface.framework/IOSurface";
    private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private static readonly ConcurrentDictionary<string, nint> Selectors = new();
    private static readonly ConcurrentDictionary<string, nint> Classes = new();

    [LibraryImport(Runtime, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint RegisterSelector(string name);

    [LibraryImport(Runtime, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetClassCore(string name);

    [LibraryImport(Runtime, EntryPoint = "objc_autoreleasePoolPush")]
    public static partial nint AutoreleasePoolPush();

    [LibraryImport(Runtime, EntryPoint = "objc_autoreleasePoolPop")]
    public static partial void AutoreleasePoolPop(nint pool);

    [LibraryImport(MetalFramework)]
    public static partial nint MTLCreateSystemDefaultDevice();

    /// <summary>A retained NSArray of every Metal device.</summary>
    [LibraryImport(MetalFramework)]
    public static partial nint MTLCopyAllDevices();

    [LibraryImport(IOSurfaceFramework)]
    public static partial nint IOSurfaceCreate(nint properties);

    [LibraryImport(CoreFoundationFramework)]
    public static partial void CFRelease(nint value);

    public static nint Sel(string name) => Selectors.GetOrAdd(name, RegisterSelector);

    public static nint Class(string name) => Classes.GetOrAdd(name, static n =>
    {
        var cls = GetClassCore(n);
        return cls != 0 ? cls : throw new InvalidOperationException($"Objective-C class {n} is unavailable.");
    });

    // ---- objc_msgSend, by signature ----------------------------------------------------------------------------------

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint a);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint a, nint b);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint a, nint b, nint c);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint a, nint b, nint c, nint d);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint a, nint b, nint c, nint d, nint e);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial void SendVoid(nint receiver, nint selector, nint a, nint b, nint c, nint d, nint e, nint f);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial nint SendDouble(nint receiver, nint selector, double a);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial void SendClearColor(nint receiver, nint selector, MTLClearColor color);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial void SendViewport(nint receiver, nint selector, MTLViewport viewport);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial void SendScissor(nint receiver, nint selector, MTLScissorRect rect);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial void SendRegion(nint receiver, nint selector, MTLRegion region, nint level, nint bytes, nint bytesPerRow);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial void SendCopyToBuffer(nint receiver, nint selector, nint texture, nint slice, nint level,
        MTLOrigin origin, MTLSize size, nint buffer, nint offset, nint bytesPerRow, nint bytesPerImage);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial ulong SendULong(nint receiver, nint selector);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial void SendULongArg(nint receiver, nint selector, ulong value);

    [LibraryImport(Runtime, EntryPoint = "objc_msgSend")]
    private static partial void SendEventValue(nint receiver, nint selector, nint @event, ulong value);

    public static nint Call(nint receiver, string selector) => Send(receiver, Sel(selector));
    public static nint Call(nint receiver, string selector, nint a) => Send(receiver, Sel(selector), a);
    public static nint Call(nint receiver, string selector, nint a, nint b) => Send(receiver, Sel(selector), a, b);
    public static nint Call(nint receiver, string selector, nint a, nint b, nint c) => Send(receiver, Sel(selector), a, b, c);
    public static nint Call(nint receiver, string selector, nint a, nint b, nint c, nint d) => Send(receiver, Sel(selector), a, b, c, d);
    public static nint Call(nint receiver, string selector, nint a, nint b, nint c, nint d, nint e) =>
        Send(receiver, Sel(selector), a, b, c, d, e);
    public static void Call(nint receiver, string selector, nint a, nint b, nint c, nint d, nint e, nint f) =>
        SendVoid(receiver, Sel(selector), a, b, c, d, e, f);
    public static void Call(nint receiver, string selector, double value) => SendDouble(receiver, Sel(selector), value);
    public static void Call(nint receiver, string selector, MTLClearColor color) => SendClearColor(receiver, Sel(selector), color);
    public static void Call(nint receiver, string selector, MTLViewport viewport) => SendViewport(receiver, Sel(selector), viewport);
    public static void Call(nint receiver, string selector, MTLScissorRect rect) => SendScissor(receiver, Sel(selector), rect);
    public static void Call(nint receiver, string selector, MTLRegion region, nint level, nint bytes, nint bytesPerRow) =>
        SendRegion(receiver, Sel(selector), region, level, bytes, bytesPerRow);
    public static void CopyToBuffer(nint encoder, nint texture, MTLSize size, nint buffer, nint bytesPerRow) =>
        SendCopyToBuffer(encoder, Sel("copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toBuffer:destinationOffset:destinationBytesPerRow:destinationBytesPerImage:"),
            texture, 0, 0, default, size, buffer, 0, bytesPerRow, bytesPerRow * (nint)size.Height);
    public static ulong CallULong(nint receiver, string selector) => SendULong(receiver, Sel(selector));
    public static void Call(nint receiver, string selector, ulong value) => SendULongArg(receiver, Sel(selector), value);
    public static void CallEvent(nint receiver, string selector, nint @event, ulong value) =>
        SendEventValue(receiver, Sel(selector), @event, value);

    public static bool CallBool(nint receiver, string selector) => (Send(receiver, Sel(selector)) & 0xFF) != 0;

    // ---- Foundation helpers ------------------------------------------------------------------------------------------

    public static nint New(string className) => Call(Call(Class(className), "alloc"), "init");

    public static void Release(nint value)
    {
        if (value != 0) Call(value, "release");
    }

    public static nint Retain(nint value) => value == 0 ? 0 : Call(value, "retain");

    /// <summary>An autoreleased NSString.</summary>
    public static nint String(string value)
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(value + "\0");
        fixed (byte* text = utf8)
            return Call(Class("NSString"), "stringWithUTF8String:", (nint)text);
    }

    public static string? ToManaged(nint nsString) =>
        nsString == 0 ? null : Marshal.PtrToStringUTF8(Call(nsString, "UTF8String"));

    public static string Describe(nint nsError) =>
        nsError == 0 ? "unknown error" : ToManaged(Call(nsError, "localizedDescription")) ?? "unknown error";

    public static nint Number(long value) => Call(Class("NSNumber"), "numberWithLongLong:", (nint)value);

    /// <summary>Pushes an autorelease pool for the scope; Objective-C objects created inside are drained at Dispose.</summary>
    public readonly struct Pool : IDisposable
    {
        private readonly nint _pool;
        private Pool(nint pool) => _pool = pool;
        public static Pool Push() => new(AutoreleasePoolPush());
        public void Dispose()
        {
            if (_pool != 0) AutoreleasePoolPop(_pool);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal record struct MTLClearColor(double Red, double Green, double Blue, double Alpha);

[StructLayout(LayoutKind.Sequential)]
internal record struct MTLViewport(double OriginX, double OriginY, double Width, double Height, double ZNear, double ZFar);

[StructLayout(LayoutKind.Sequential)]
internal record struct MTLScissorRect(nuint X, nuint Y, nuint Width, nuint Height);

[StructLayout(LayoutKind.Sequential)]
internal record struct MTLOrigin(nuint X, nuint Y, nuint Z);

[StructLayout(LayoutKind.Sequential)]
internal record struct MTLSize(nuint Width, nuint Height, nuint Depth);

[StructLayout(LayoutKind.Sequential)]
internal record struct MTLRegion(MTLOrigin Origin, MTLSize Size);
