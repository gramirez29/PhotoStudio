# Makes assets/background.jpg, the picture behind every screen of the app, from assets/background-source.jpg.
#
# The picture is blurred here, once, instead of on every screen: a blur at run time costs memory and looks different on Android
# and on iOS. It is a Gaussian-like blur (three passes of a box blur, with the edges repeated so no frame appears); a bigger
# -Radius blurs more. The wash (white in the light theme, navy in the dark one) is laid over it by the app
# (src/theme/background.ts).
#
# Usage, from the mobile folder:  powershell -ExecutionPolicy Bypass -File scripts/blur-background.ps1 [-Radius 22]
param(
    [int]$Radius = 22
)

Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class BackgroundBlur
{
    // Enlarges the picture to twice its size with smooth resampling and returns its pixels as BGR bytes.
    public static byte[] Load(string path, out int width, out int height)
    {
        using (Image source = Image.FromFile(path))
        using (Bitmap big = new Bitmap(source.Width * 2, source.Height * 2, PixelFormat.Format24bppRgb))
        {
            using (Graphics g = Graphics.FromImage(big))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, 0, 0, big.Width, big.Height);
            }
            width = big.Width;
            height = big.Height;
            BitmapData data = big.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            byte[] pixels = new byte[Math.Abs(data.Stride) * height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            big.UnlockBits(data);
            return pixels;
        }
    }

    // One pass of a box blur along rows (horizontal = true) or columns, repeating the edge pixels.
    static void Pass(byte[] input, byte[] output, int width, int height, int stride, int radius, bool horizontal)
    {
        int length = horizontal ? width : height;
        int lines = horizontal ? height : width;
        int window = 2 * radius + 1;
        for (int line = 0; line < lines; line++)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                long sum = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int p = Math.Min(Math.Max(k, 0), length - 1);
                    sum += input[Index(line, p, channel, stride, horizontal)];
                }
                for (int i = 0; i < length; i++)
                {
                    output[Index(line, i, channel, stride, horizontal)] = (byte)(sum / window);
                    int leaving = Math.Min(Math.Max(i - radius, 0), length - 1);
                    int entering = Math.Min(Math.Max(i + radius + 1, 0), length - 1);
                    sum += input[Index(line, entering, channel, stride, horizontal)] - input[Index(line, leaving, channel, stride, horizontal)];
                }
            }
        }
    }

    static int Index(int line, int position, int channel, int stride, bool horizontal)
    {
        return horizontal ? line * stride + position * 3 + channel : position * stride + line * 3 + channel;
    }

    public static void Run(string source, string target, int radius)
    {
        int width, height;
        byte[] a = Load(source, out width, out height);
        int stride = a.Length / height;
        byte[] b = new byte[a.Length];
        for (int round = 0; round < 3; round++)
        {
            Pass(a, b, width, height, stride, radius, true);
            Pass(b, a, width, height, stride, radius, false);
        }

        using (Bitmap result = new Bitmap(width, height, PixelFormat.Format24bppRgb))
        {
            BitmapData data = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            System.Runtime.InteropServices.Marshal.Copy(a, 0, data.Scan0, a.Length);
            result.UnlockBits(data);
            ImageCodecInfo jpeg = null;
            foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
            {
                if (codec.MimeType == "image/jpeg") jpeg = codec;
            }
            EncoderParameters parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
            result.Save(target, jpeg, parameters);
        }
        Console.WriteLine("background.jpg written, " + width + "x" + height + ", radius " + radius);
    }
}
"@ -ReferencedAssemblies System.Drawing

$source = (Resolve-Path (Join-Path $PSScriptRoot '..\assets\background-source.jpg')).Path
$target = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\assets')).Path 'background.jpg'
[BackgroundBlur]::Run($source, $target, $Radius)
