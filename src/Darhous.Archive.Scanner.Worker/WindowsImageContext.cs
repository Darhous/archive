using System.Drawing.Imaging;
using NAPS2.Images;
using NAPS2.Images.Bitwise;
using NAPS2.Images.Transforms;
using NAPS2.Util;

namespace Darhous.Archive.Scanner.Worker;

/// <summary>Minimal Windows backend because the brief permits only NAPS2.Sdk.
/// Supports acquisition and PDF export, not general editing/deskew/thumbnail transforms.</summary>
[System.Runtime.Versioning.RequiresPreviewFeatures("NAPS2.Images 1.3.0 marks its API as preview.")]
internal sealed class WindowsImageContext() : ImageContext(typeof(WindowsImage))
{
    protected override bool SupportsTiff => true;

    protected override IMemoryImage LoadCore(Stream stream, ImageFileFormat format)
    {
        using var decoded = new Bitmap(stream);
        return CopyBitmap(decoded);
    }

    internal WindowsImage CopyBitmap(Bitmap source)
    {
        var copy = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        if (source.HorizontalResolution > 0 && source.VerticalResolution > 0)
            copy.SetResolution(source.HorizontalResolution, source.VerticalResolution);
        using (var graphics = Graphics.FromImage(copy))
            graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
        return new WindowsImage(this, copy);
    }

    protected override void LoadFramesCore(Action<IMemoryImage> produceImage, Stream stream,
        ImageFileFormat format, ProgressHandler progress)
    {
        using var decoded = new Bitmap(stream);
        var dimension = new FrameDimension(decoded.FrameDimensionsList[0]);
        for (var i = 0; i < decoded.GetFrameCount(dimension); i++)
        {
            if (progress.IsCancellationRequested) throw new OperationCanceledException();
            decoded.SelectActiveFrame(dimension, i);
            produceImage(CopyBitmap(decoded));
        }
    }

    public override IMemoryImage Create(int width, int height, ImagePixelFormat pixelFormat)
    {
        var format = pixelFormat switch
        {
            ImagePixelFormat.BW1 => System.Drawing.Imaging.PixelFormat.Format1bppIndexed,
            ImagePixelFormat.Gray8 => System.Drawing.Imaging.PixelFormat.Format8bppIndexed,
            ImagePixelFormat.RGB24 => System.Drawing.Imaging.PixelFormat.Format24bppRgb,
            ImagePixelFormat.ARGB32 => System.Drawing.Imaging.PixelFormat.Format32bppArgb,
            _ => throw new NotSupportedException("Unsupported pixel format."),
        };
        var bitmap = new Bitmap(width, height, format);
        if (pixelFormat is ImagePixelFormat.BW1 or ImagePixelFormat.Gray8)
        {
            var palette = bitmap.Palette;
            for (var i = 0; i < palette.Entries.Length; i++)
            {
                var gray = i * 255 / (palette.Entries.Length - 1);
                palette.Entries[i] = Color.FromArgb(gray, gray, gray);
            }
            bitmap.Palette = palette;
        }
        return new WindowsImage(this, bitmap);
    }

    public override IMemoryImage PerformTransform(IMemoryImage image, Transform transform)
    {
        if (transform is ColorBitDepthTransform)
        {
            if (image.PixelFormat is ImagePixelFormat.RGB24 or ImagePixelFormat.ARGB32) return image;
            var color = CopyBitmap(((WindowsImage)image).Bitmap);
            image.Dispose();
            return color;
        }
        if (transform is not GrayscaleTransform && transform is not BlackWhiteTransform)
            throw new NotSupportedException($"Image transform {transform.GetType().Name} is outside this scanner backend's scope.");
        if (image.PixelFormat is ImagePixelFormat.BW1 ||
            (image.PixelFormat is ImagePixelFormat.Gray8 && transform is GrayscaleTransform)) return image;
        if (image.PixelFormat != ImagePixelFormat.RGB24)
        {
            var converted = CopyBitmap(((WindowsImage)image).Bitmap);
            image.Dispose();
            image = converted;
        }
        var bitmap = ((WindowsImage)image).Bitmap;
        // Only fallback color conversion; normal drivers provide the requested bit depth themselves.
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var c = bitmap.GetPixel(x, y);
            var gray = (299 * c.R + 587 * c.G + 114 * c.B) / 1000;
            if (transform is BlackWhiteTransform blackWhite)
                gray = gray >= blackWhite.Threshold * 255 / 1000 ? 255 : 0;
            bitmap.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
        }
        image.LogicalPixelFormat = transform is BlackWhiteTransform ? ImagePixelFormat.BW1 : ImagePixelFormat.Gray8;
        return image;

    }
}

[System.Runtime.Versioning.RequiresPreviewFeatures("NAPS2.Images 1.3.0 marks its API as preview.")]
internal sealed class WindowsImage(WindowsImageContext context, Bitmap bitmap) : IMemoryImage
{
    internal Bitmap Bitmap { get; } = bitmap;
    public ImageContext ImageContext => context;
    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;
    public float HorizontalResolution => Bitmap.HorizontalResolution;
    public float VerticalResolution => Bitmap.VerticalResolution;
    public ImagePixelFormat PixelFormat => Bitmap.PixelFormat switch
    {
        System.Drawing.Imaging.PixelFormat.Format1bppIndexed => ImagePixelFormat.BW1,
        System.Drawing.Imaging.PixelFormat.Format8bppIndexed => ImagePixelFormat.Gray8,
        System.Drawing.Imaging.PixelFormat.Format32bppArgb => ImagePixelFormat.ARGB32,
        _ => ImagePixelFormat.RGB24,
    };
    public ImageFileFormat OriginalFileFormat { get; set; }
    public ImagePixelFormat LogicalPixelFormat { get; set; }
    public void SetResolution(float xDpi, float yDpi)
    {
        if (xDpi > 0 && yDpi > 0) Bitmap.SetResolution(xDpi, yDpi);
    }

    public ImageLockState Lock(LockMode lockMode, out BitwiseImageData imageData)
    {
        var mode = lockMode switch
        {
            LockMode.ReadOnly => ImageLockMode.ReadOnly,
            LockMode.WriteOnly => ImageLockMode.WriteOnly,
            _ => ImageLockMode.ReadWrite,
        };
        if (mode != ImageLockMode.ReadOnly) LogicalPixelFormat = ImagePixelFormat.Unknown;
        var data = Bitmap.LockBits(new Rectangle(0, 0, Width, Height), mode, Bitmap.PixelFormat);
        var subPixel = PixelFormat switch
        {
            ImagePixelFormat.BW1 => SubPixelType.Bit,
            ImagePixelFormat.Gray8 => SubPixelType.Gray,
            ImagePixelFormat.ARGB32 => SubPixelType.Bgra,
            _ => SubPixelType.Bgr,
        };
        imageData = new BitwiseImageData(data.Scan0, new PixelInfo(Width, Height, subPixel, data.Stride));
        return new BitmapLock(Bitmap, data);
    }

    public IMemoryImage Clone() => new WindowsImage(context,
        Bitmap.Clone(new Rectangle(0, 0, Width, Height), Bitmap.PixelFormat))
    {
        OriginalFileFormat = OriginalFileFormat,
        LogicalPixelFormat = LogicalPixelFormat,
    };
    public void Save(string path, ImageFileFormat imageFormat = ImageFileFormat.Unknown, ImageSaveOptions? options = null)
    {
        using var stream = File.Create(path);
        Save(stream, imageFormat == ImageFileFormat.Unknown ? ImageContext.GetFileFormatFromExtension(path) : imageFormat, options);
    }

    public void Save(Stream stream, ImageFileFormat imageFormat, ImageSaveOptions? options = null)
    {
        var format = imageFormat switch
        {
            ImageFileFormat.Png => ImageFormat.Png,
            ImageFileFormat.Jpeg => ImageFormat.Jpeg,
            ImageFileFormat.Bmp => ImageFormat.Bmp,
            ImageFileFormat.Tiff => ImageFormat.Tiff,
            _ => throw new NotSupportedException("Unsupported image output format."),
        };
        Bitmap.Save(stream, format);
    }
    public void Dispose() => Bitmap.Dispose();

    private sealed class BitmapLock(Bitmap bitmap, BitmapData data) : ImageLockState
    {
        private bool _disposed;
        public override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            bitmap.UnlockBits(data);
        }
    }
}
