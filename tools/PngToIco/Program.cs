using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: PngToIco <input.png> <output.ico>");
    return 1;
}

var input = args[0];
var output = args[1];
var sizes = new[] { 16, 32, 48, 64, 128, 256 };

using var src = Image.FromFile(input);
var bitmaps = new List<Bitmap>();
foreach (var size in sizes)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.Clear(Color.Transparent);
    g.CompositingMode = CompositingMode.SourceCopy;
    g.CompositingQuality = CompositingQuality.HighQuality;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.SmoothingMode = SmoothingMode.HighQuality;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.DrawImage(src, new Rectangle(0, 0, size, size));
    bitmaps.Add(bmp);
}

WriteIco(output, bitmaps);
foreach (var b in bitmaps) b.Dispose();
Console.WriteLine($"Wrote {output}");
return 0;

static void WriteIco(string path, List<Bitmap> images)
{
    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);

    bw.Write((ushort)0); // reserved
    bw.Write((ushort)1); // type icon
    bw.Write((ushort)images.Count);

    var offset = 6 + 16 * images.Count;
    var pngBlobs = new List<byte[]>();

    foreach (var bmp in images)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        var data = ms.ToArray();
        pngBlobs.Add(data);

        var w = bmp.Width >= 256 ? 0 : bmp.Width;
        var h = bmp.Height >= 256 ? 0 : bmp.Height;
        bw.Write((byte)w);
        bw.Write((byte)h);
        bw.Write((byte)0); // colors
        bw.Write((byte)0); // reserved
        bw.Write((ushort)1); // planes
        bw.Write((ushort)32); // bit count
        bw.Write(data.Length);
        bw.Write(offset);
        offset += data.Length;
    }

    foreach (var blob in pngBlobs)
        bw.Write(blob);
}
