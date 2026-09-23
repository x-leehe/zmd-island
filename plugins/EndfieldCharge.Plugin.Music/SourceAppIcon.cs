using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>Best-effort Windows shell icon lookup; an unknown source has no badge.</summary>
public static class SourceAppIcon
{
    private static readonly Guid ImageFactoryId = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    public static Bitmap? TryLoad(string appId)
    {
        try
        {
            string parsingName = "shell:AppsFolder\\" + appId;
            if (File.Exists(appId))
                parsingName = appId;
            else if (appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(appId)))
                {
                    using (process)
                    {
                        try
                        {
                            if (process.MainModule?.FileName is { } path)
                            {
                                parsingName = path;
                                break;
                            }
                        }
                        catch (System.ComponentModel.Win32Exception) { }
                        catch (InvalidOperationException) { }
                    }
                }
            }

            SHCreateItemFromParsingName(parsingName, IntPtr.Zero, in ImageFactoryId, out var factory);
            try
            {
                int result = factory.GetImage(new NativeSize(32, 32), 0x4, out var handle);
                if (result != 0 || handle == IntPtr.Zero)
                    return null;
                try
                {
                    if (GetObject(handle, Marshal.SizeOf<NativeBitmap>(), out var bitmap) == 0)
                        return null;
                    int width = bitmap.Width, height = Math.Abs(bitmap.Height);
                    if (width <= 0 || height <= 0 || width > 256 || height > 256)
                        return null;
                    var pixels = new byte[width * height * 4];
                    var info = new BitmapInfo
                    {
                        Header = new BitmapInfoHeader
                        {
                            Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(), Width = width, Height = -height,
                            Planes = 1, BitCount = 32
                        }
                    };
                    var dc = GetDC(IntPtr.Zero);
                    if (dc == IntPtr.Zero)
                        return null;
                    try
                    {
                        int rows = GetDIBits(dc, handle, 0, (uint)height, pixels, ref info, 0);
                        if (rows != height)
                            return null;
                    }
                    finally { ReleaseDC(IntPtr.Zero, dc); }

                    var pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                    try
                    {
                        return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, pin.AddrOfPinnedObject(),
                            new PixelSize(width, height), new Vector(96, 96), width * 4);
                    }
                    finally { pin.Free(); }
                }
                finally { DeleteObject(handle); }
            }
            finally { Marshal.ReleaseComObject(factory); }
        }
        catch (Exception)
        {
            return null;
        }
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageFactory
    {
        [PreserveSig] int GetImage(NativeSize size, uint flags, out IntPtr bitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeSize(int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBitmap
    {
        public int Type, Width, Height, Stride;
        public ushort Planes, BitsPerPixel;
        public IntPtr Bits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, ImageSize;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ColorsUsed, ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Color;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr context, in Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IImageFactory factory);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static extern int GetObject(IntPtr handle, int size, out NativeBitmap bitmap);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines,
        byte[] pixels, ref BitmapInfo info, uint usage);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);
}
