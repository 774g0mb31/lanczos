using SkiaSharp;
using System.Runtime.InteropServices;

class ImageResizer
{
    static void Main(String[] args)
    {
        var fileName = args[0];
        int dstWidth = Int32.Parse(args[1]);
        int dstHeight = Int32.Parse(args[2]);
        int kernelSize = args.Length < 4 ? 3 : Int32.Parse(args[3]);

        var image = SKImage.FromEncodedData(fileName);
        var srcBmp = SKBitmap.FromImage(image);

        ImageResizer resizer = new ImageResizer();
        SKBitmap dstBmp = resizer.resize(srcBmp, dstWidth, dstHeight, kernelSize);

        using (SKFileWStream stream = new SKFileWStream("resized_" + fileName))
        {
            dstBmp.Encode(stream, SKEncodedImageFormat.Png, 1);
        }
    }

    public SKBitmap resize(SKBitmap srcBmp, int dstWidth, int dstHeight, int kernelSize)
    {
        var dstBmp = new SKBitmap(dstWidth, dstHeight, srcBmp.Info.ColorType, SKAlphaType.Opaque);
        var dstArray = dstBmp.Bytes;

        GCHandle pinnedArray = GCHandle.Alloc(dstArray, GCHandleType.Pinned);
        IntPtr pinnedArrayPtr = pinnedArray.AddrOfPinnedObject();

        Parallel.ForEach(Enumerable.Range(0, dstHeight).ToList(), y =>
        {
            Parallel.ForEach(Enumerable.Range(0, dstWidth).ToList(), x =>
            {
                setTargetPixel(x, y, dstWidth, dstHeight, srcBmp, dstArray, kernelSize);
            });
        });

        dstBmp.SetPixels(pinnedArrayPtr);
        pinnedArray.Free();

        return dstBmp;
    }

    private void setTargetPixel(int distX, int distY, int dstWidth, int dstHeight, SKBitmap srcBmp, byte[] dstArray, int kernelSize)
    {
        byte[] srcArray = srcBmp.Bytes;
        var srcWidth = srcBmp.Width;
        var srcHeight = srcBmp.Height;

        double widthFactor = (double) srcWidth / dstWidth;
        double heightFactor = (double) srcHeight / dstHeight;

        var srcX = distX * widthFactor;
        var srcY = distY * heightFactor;

        double r = .0, g = .0, b = .0;

        for (int y = 1; y < kernelSize * 2; y++)
        {
            for (int x = 1; x < kernelSize * 2; x++)
            {
                int sampleX = (int) Math.Round(srcX) - kernelSize + x;
                int sampleY = (int) Math.Round(srcY) - kernelSize + y;

                var weight = getLanczosWeight(srcX - sampleX, kernelSize) * getLanczosWeight(srcY - sampleY, kernelSize);

                if (weight == 0 || sampleX < 0 || sampleX >= srcWidth || sampleY < 0 || sampleY >= srcHeight)
                {
                    continue;
                }

                var sampleIdx = getPixelIndex(sampleX, sampleY, srcWidth);

                b += srcArray[sampleIdx + 0] * weight;
                g += srcArray[sampleIdx + 1] * weight;
                r += srcArray[sampleIdx + 2] * weight;
            }
        }

        var distIdx = getPixelIndex(distX, distY, dstWidth);

        dstArray[distIdx + 0] = getColorByte(b);
        dstArray[distIdx + 1] = getColorByte(g);
        dstArray[distIdx + 2] = getColorByte(r);
    }

    private double sinc(double x)
    {
        return Math.Sin(x * Math.PI) / (x * Math.PI);
    }

    private double getLanczosWeight(double x, int kernelSize)
    {
        return x == 0 ? 1 : Math.Abs(x) < kernelSize ? sinc(x) * sinc(x / kernelSize) : 0;
    }

    private byte getColorByte(double x)
    {
        return (byte) Math.Min(Math.Max(0, Math.Round(x)), 255);
    }

    private int getPixelIndex(int x, int y , int stride)
    {
        return 4 * (x + stride * y);
    }
}