using SkiaSharp;
using System.Runtime.InteropServices;

class ImageResizer
{
    static void Main(String[] args)
    {
        var fileName = args[0];
        int targetWidth = Int32.Parse(args[1]);
        int targetHeight = Int32.Parse(args[2]);
        int kernelSize = args.Length < 4 ? 3 : Int32.Parse(args[3]);

        var image = SKImage.FromEncodedData(fileName);
        var sourceBitmap = SKBitmap.FromImage(image);

        ImageResizer resizer = new ImageResizer();
        SKBitmap targetBitmap = resizer.Resize(sourceBitmap, targetWidth, targetHeight, kernelSize);

        using (SKFileWStream stream = new SKFileWStream("resized_" + fileName))
        {
            targetBitmap.Encode(stream, SKEncodedImageFormat.Png, 1);
        }
    }

    public SKBitmap Resize(SKBitmap sourceBitmap, int targetWidth, int targetHeight, int kernelSize)
    {
        var targetBitmap = new SKBitmap(targetWidth, targetHeight, sourceBitmap.Info.ColorType, SKAlphaType.Opaque);
        var dstArray = targetBitmap.Bytes;

        GCHandle pinnedArray = GCHandle.Alloc(dstArray, GCHandleType.Pinned);
        IntPtr pinnedArrayPtr = pinnedArray.AddrOfPinnedObject();

        Parallel.ForEach(Enumerable.Range(0, targetHeight).ToList(), y =>
        {
            Parallel.ForEach(Enumerable.Range(0, targetWidth).ToList(), x =>
            {
                SetTargetPixel(x, y, targetWidth, targetHeight, sourceBitmap, dstArray, kernelSize);
            });
        });

        targetBitmap.SetPixels(pinnedArrayPtr);
        pinnedArray.Free();

        return targetBitmap;
    }

    private void SetTargetPixel(int targetX, int targetY, int targetWidth, int targetHeight, SKBitmap sourceBitmap, byte[] targetArray, int kernelSize)
    {
        byte[] sourceArray = sourceBitmap.Bytes;
        var sourceWidth = sourceBitmap.Width;
        var sourceHeight = sourceBitmap.Height;

        double widthFactor = (double) sourceWidth / targetWidth;
        double heightFactor = (double) sourceHeight / targetHeight;

        var sourceX = targetX * widthFactor;
        var sourceY = targetY * heightFactor;

        double r = .0, g = .0, b = .0;

        for (int y = 1; y < kernelSize * 2; y++)
        {
            for (int x = 1; x < kernelSize * 2; x++)
            {
                int sampleX = (int) Math.Round(sourceX) - kernelSize + x;
                int sampleY = (int) Math.Round(sourceY) - kernelSize + y;

                var weight = GetLanczosWeight(sourceX - sampleX, kernelSize) * GetLanczosWeight(sourceY - sampleY, kernelSize);

                if (weight == 0 || sampleX < 0 || sampleX >= sourceWidth || sampleY < 0 || sampleY >= sourceHeight)
                {
                    continue;
                }

                var sampleIndex = GetPixelIndex(sampleX, sampleY, sourceWidth);

                b += sourceArray[sampleIndex + 0] * weight;
                g += sourceArray[sampleIndex + 1] * weight;
                r += sourceArray[sampleIndex + 2] * weight;
            }
        }

        var targetIndex = GetPixelIndex(targetX, targetY, targetWidth);

        targetArray[targetIndex + 0] = GetColorByte(b);
        targetArray[targetIndex + 1] = GetColorByte(g);
        targetArray[targetIndex + 2] = GetColorByte(r);
    }

    private double Sinc(double x)
    {
        return Math.Sin(x * Math.PI) / (x * Math.PI);
    }

    private double GetLanczosWeight(double x, int kernelSize)
    {
        return x == 0 ? 1 : Math.Abs(x) < kernelSize ? Sinc(x) * Sinc(x / kernelSize) : 0;
    }

    private byte GetColorByte(double x)
    {
        return (byte) Math.Min(Math.Max(0, Math.Round(x)), 255);
    }

    private int GetPixelIndex(int x, int y , int stride)
    {
        return 4 * (x + stride * y);
    }
}