using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using MemoryTributaryS;
using EasyCompressor;

if (args.Length < 5)
{
	Console.WriteLine("Usage: <program> <input_directory> <output_file> <frame_width> <frame_height> <luminance> [write_intermediate_dir]");
	return;
}

string inputDirectory = Path.GetFullPath(args[0]);
string outputDirectory = Path.GetFullPath(args[1]);
int width = int.Parse(args[2]);
int height = int.Parse(args[3]);
float luminance = float.Parse(args[4]);
string? intermediateDirectory = null;
if (args.Length == 6) {
  intermediateDirectory = Path.GetFullPath(args[5]);
  if (!Directory.Exists(intermediateDirectory)) {
    Directory.CreateDirectory(intermediateDirectory);
  }
}

string[] frames = Directory.GetFiles(inputDirectory);

Array.Sort(frames, StringComparer.Ordinal);

string compressedOutputPath = outputDirectory;

List<string> allFramesData = [];

using MemoryTributary binaryMemory = new();
BinaryWriter writer = new(binaryMemory);

void ProcessFrame(string path) 
{
	Console.WriteLine($"Processing frame at {path}");
  byte[] fileBytes = File.ReadAllBytes(path);
  using Image<Rgba32> image = Image.Load<Rgba32>(fileBytes);
  image.Mutate((v) =>
  {
    v.BinaryThreshold(luminance);
    v.Resize(new Size(width, height));
  });
  if (intermediateDirectory != null) {
    image.Save(Path.Join(intermediateDirectory, Path.GetFileName(path)));
  }
  image.ProcessPixelRows(accessor => {
    writer.Write(accessor.Height);
    writer.Write(accessor.Width);
    for (int y = 0; y < accessor.Height; y++) {
      Span<Rgba32> row = accessor.GetRowSpan(y);
      byte packedByte = 0;
      int bitIndex = 7;
      for (int x = 0; x < row.Length; x++) {
        Rgba32 pixel = row[x];
        byte r = (byte)(pixel.R < 128 ? 0 : 255);
        byte g = (byte)(pixel.G < 128 ? 0 : 255);
        byte b = (byte)(pixel.B < 128 ? 0 : 255);
        bool binaryValue = r == 255 & g == 255 && b == 255;
        if (binaryValue)
          packedByte |= (byte)(1 << bitIndex);
        bitIndex--;
        if (bitIndex < 0 || x == row.Length - 1) {
          writer.Write(packedByte);
          packedByte = 0;
          bitIndex = 7;
        }
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