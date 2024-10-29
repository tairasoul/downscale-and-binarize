using ImageMagick;
using System.IO.Compression;
using System.Text;

if (args.Length < 4)
{
	Console.WriteLine("Usage: <program> <input_directory> <output_directory> <frame_width> <frame_height>");
	return;
}

string inputDirectory = Path.GetFullPath(args[0]);
string outputDirectory = Path.GetFullPath(args[1]);
uint width = uint.Parse(args[2]);
uint height = uint.Parse(args[3]);

string[] frames = Directory.GetFiles(inputDirectory);

Array.Sort(frames, StringComparer.Ordinal);

string compressedOutputPath = $"{outputDirectory}/frames.gz";

List<string> allFramesData = [];

bool dimensionsPrinted = false;
string size = "";

void ProcessFrame(string path) 
{
	Console.WriteLine($"Processing frame at {path}");
	MagickImage image = (MagickImage)MagickImage.FromBase64(Convert.ToBase64String(File.ReadAllBytes(path)));
	image.Resize(width, height);
	if (!dimensionsPrinted) 
	{
		size = $"{image.Width}x{image.Height}";
		dimensionsPrinted = true;
	}
	IPixelCollection<byte> pixels = image.GetPixels();
	List<string> pixelData = [];

	for (int y = 0; y < image.Height; y++)
	{
		List<string> rowData = [];
		for (int x = 0; x < image.Width; x++)
		{
			IPixel<byte> pixelValue = pixels[x, y];
			IMagickColor<byte> colour = pixelValue.ToColor();

			byte r = (byte)(colour.R < 128 ? 0 : 255);
			byte g = (byte)(colour.G < 128 ? 0 : 255);
			byte b = (byte)(colour.B < 128 ? 0 : 255);
			int binaryValue = (r == 255 && g == 255 && b == 255) ? 1 : 0;
			rowData.Add(binaryValue.ToString());
		}
		pixelData.Add(string.Join("", rowData) + "&");
	}

	string stringifiedData = string.Join("", pixelData);
	
	allFramesData.Add(stringifiedData);
}

foreach (string frame in frames) 
	ProcessFrame(frame);

string finalData = $"{size}-{string.Join(";", allFramesData)}";

byte[] bytes = Encoding.UTF8.GetBytes(finalData);
using (MemoryStream originalFileStream = new(bytes)) 
{
	using FileStream compressedFileStream = new(compressedOutputPath, FileMode.Create);
	using GZipStream compressionStream = new(compressedFileStream, CompressionLevel.Optimal);
	originalFileStream.CopyTo(compressionStream);
}

Console.WriteLine($"Compressed file saved to {compressedOutputPath}, frame size {size}");