using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using MemoryTributaryS;
using EasyCompressor;

if (args.Length < 4)
{
	Console.WriteLine("Usage: <program> <input_directory> <output_file> <frame_width> <frame_height>");
	return;
}

string inputDirectory = Path.GetFullPath(args[0]);
string outputDirectory = Path.GetFullPath(args[1]);
int width = int.Parse(args[2]);
int height = int.Parse(args[3]);

string[] frames = Directory.GetFiles(inputDirectory);

Array.Sort(frames, StringComparer.Ordinal);

string compressedOutputPath = outputDirectory;

List<string> allFramesData = [];

bool dimensionsPrinted = false;
using MemoryTributary binaryMemory = new();
BinaryWriter writer = new(binaryMemory);

void ProcessFrame(string path) 
{
	Console.WriteLine($"Processing frame at {path}");
  byte[] fileBytes = File.ReadAllBytes(path);
  using Image<Rgba32> image = Image.Load<Rgba32>(fileBytes);
  image.Mutate((v) =>
  {
    BlackWhiteProcessor processor = new();
    v.Resize(new Size(width, height));
    v.ApplyProcessor(processor);
  });
	if (!dimensionsPrinted) {
    writer.Write(image.Width);
    writer.Write(image.Height);
  }
  image.ProcessPixelRows(accessor => {
    writer.Write(accessor.Height);
    for (int y = 0; y < accessor.Height; y++) {
      Span<Rgba32> row = accessor.GetRowSpan(y);
      writer.Write(row.Length);
      for (int x = 0; x < row.Length; x++) {
        Rgba32 pixel = row[x];
        byte r = (byte)(pixel.R < 128 ? 0 : 255);
        byte g = (byte)(pixel.G < 128 ? 0 : 255);
        byte b = (byte)(pixel.B < 128 ? 0 : 255);
        int binaryValue = (r == 255 & g == 255 && b == 255) ? 1 : 0;
        writer.Write(binaryValue);
      }
    }
  });
}

writer.Flush();

foreach (string frame in frames) 
	ProcessFrame(frame);

using FileStream stream = File.Open(outputDirectory, FileMode.Create);

binaryMemory.Position = 0;
DeflateCompressor d = DeflateCompressor.Shared;
d.Level = System.IO.Compression.CompressionLevel.SmallestSize;
d.Compress(binaryMemory, stream);

//using IronCompressResult result = iron.Compress(Codec.Brotli, binaryMemory.ToArray(), null, CompressionLevel.SmallestSize);

//File.WriteAllBytes(compressedOutputPath, result.AsSpan().ToArray());