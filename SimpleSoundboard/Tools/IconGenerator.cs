using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;

namespace SimpleSoundboard.Tools;

/// <summary>
/// Builds the multi-resolution app icon (.ico) from the source image
/// <c>Assets/logo.png</c> by downscaling it to each icon size.
///
/// Run via: dotnet run -- --genicon SimpleSoundboard/Assets/logo.ico
/// (reads logo.png sitting next to the output path). Requires Avalonia to be
/// initialized first (SetupWithoutStarting) for image decoding/scaling.
///
/// Small sizes are written as classic 32-bit BMP/DIB entries (decoded everywhere —
/// GDI, shell, taskbar); 128/256 use PNG to keep the file small.
/// </summary>
public static class IconGenerator
{
    private static readonly int[] BmpSizes = { 16, 24, 32, 48, 64 };
    private static readonly int[] PngSizes = { 128, 256 };

    public static void Generate(string sourcePngPath, string outputPath)
    {
        using var source = new Bitmap(sourcePngPath);

        var entries = new List<(int size, byte[] data)>();
        foreach (var size in BmpSizes)
        {
            entries.Add((size, RenderDib(source, size)));
        }

        foreach (var size in PngSizes)
        {
            entries.Add((size, RenderPng(source, size)));
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        // ICONDIR header
        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // type: icon
        writer.Write((ushort)entries.Count);

        // ICONDIRENTRY table
        int offset = 6 + 16 * entries.Count;
        foreach (var (size, data) in entries)
        {
            byte dim = (byte)(size >= 256 ? 0 : size); // 0 means 256
            writer.Write(dim);                // width
            writer.Write(dim);                // height
            writer.Write((byte)0);            // palette count
            writer.Write((byte)0);            // reserved
            writer.Write((ushort)1);          // colour planes
            writer.Write((ushort)32);         // bits per pixel
            writer.Write((uint)data.Length);
            writer.Write((uint)offset);
            offset += data.Length;
        }

        foreach (var (_, data) in entries)
        {
            writer.Write(data);
        }
    }

    private static Bitmap Scale(Bitmap source, int size) =>
        source.CreateScaledBitmap(new PixelSize(size, size), BitmapInterpolationMode.HighQuality);

    private static byte[] RenderPng(Bitmap source, int size)
    {
        using var scaled = Scale(source, size);
        using var memory = new MemoryStream();
        scaled.Save(memory);
        return memory.ToArray();
    }

    /// <summary>Builds a 32-bit BGRA DIB icon entry (header + XOR pixels + AND mask).</summary>
    private static byte[] RenderDib(Bitmap source, int size)
    {
        using var scaled = Scale(source, size);

        int stride = size * 4;
        var pixels = new byte[stride * size];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            scaled.CopyPixels(new PixelRect(0, 0, size, size),
                handle.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);

        // BITMAPINFOHEADER — height is doubled to cover XOR colour + AND mask.
        writer.Write(40);              // biSize
        writer.Write(size);            // biWidth
        writer.Write(size * 2);        // biHeight
        writer.Write((ushort)1);       // biPlanes
        writer.Write((ushort)32);      // biBitCount
        writer.Write(0);               // biCompression = BI_RGB
        writer.Write(0);               // biSizeImage
        writer.Write(0);               // biXPelsPerMeter
        writer.Write(0);               // biYPelsPerMeter
        writer.Write(0);               // biClrUsed
        writer.Write(0);               // biClrImportant

        // XOR colour data: bottom-up rows, straight-alpha BGRA.
        for (int y = size - 1; y >= 0; y--)
        {
            int row = y * stride;
            for (int x = 0; x < size; x++)
            {
                int i = row + x * 4;
                byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2], a = pixels[i + 3];
                if (a is not 0 and not 255)
                {
                    // Avalonia gives premultiplied alpha; convert to straight.
                    b = (byte)Math.Min(255, b * 255 / a);
                    g = (byte)Math.Min(255, g * 255 / a);
                    r = (byte)Math.Min(255, r * 255 / a);
                }

                writer.Write(b);
                writer.Write(g);
                writer.Write(r);
                writer.Write(a);
            }
        }

        // AND mask: 1bpp, rows padded to 32 bits. All zero = use the alpha channel.
        int maskStride = (size + 31) / 32 * 4;
        var maskRow = new byte[maskStride];
        for (int y = 0; y < size; y++)
        {
            writer.Write(maskRow);
        }

        return memory.ToArray();
    }
}
