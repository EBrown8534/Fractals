using Evbpc.Framework.Utilities.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fractal_Generator.Mandelbrot
{
    public class Generator
    {
        public const double twoSquared = 2 * 2;

        public ILogger Logger { get; }

        public int NumberOfCores { get; set; }
        public int NumberOfChunks { get; set; }
        public ushort MaxIterations { get; set; }
        public Size ImageSize { get; set; }
        public int NumberOfColors { get; set; }
        public bool BlackFinalColor { get; set; }

        private readonly Func<int, int, int> pointToIndex;

        public Generator(ILogger logger,
                         int numberOfCores,
                         int numberOfChunks,
                         ushort maxIterations,
                         Size imageSize,
                         int numberOfColors,
                         bool blackFinalColor)
        {
            NumberOfChunks = numberOfChunks;
            NumberOfColors = numberOfColors;
            MaxIterations = maxIterations;
            ImageSize = imageSize;
            NumberOfCores = numberOfCores;
            BlackFinalColor = blackFinalColor;
            Logger = logger;

            pointToIndex = delegate (int x, int y) { return y * ImageSize.Width + x; };
        }

        public Color[] BuildColors()
        {
            Logger?.LogInformation($"Building colors...");
            Color[] colors = new Color[NumberOfColors];

            for (int i = 0; i < NumberOfColors - 1; i++)
            {
                colors[i] = Color.FromArgb(255, 0, 0, (i + 1) * (256 / NumberOfColors));
            }

            if (BlackFinalColor)
            {
                colors[NumberOfColors - 1] = Color.FromArgb(255, 0, 0, 0);
            }

            Logger?.LogInformation($"Colors built, returning.");
            return colors;
        }

        public Bitmap BuildImage(ushort[] results)
        {
            Logger?.LogInformation($"Building image...");
            Color[] colors = BuildColors();

            Bitmap image = new Bitmap(ImageSize.Width, ImageSize.Height);

            for (int y = 0; y < ImageSize.Height; y++)
            {
                for (int x = 0; x < ImageSize.Width; x++)
                {
                    image.SetPixel(x, y, colors[results[pointToIndex(x, y)] / (int)(Math.Ceiling(MaxIterations / (double)NumberOfColors))]);
                }
            }

            Logger?.LogInformation($"Image built, returning.");
            return image;
        }

        public ushort[] Generate(Point center, SizeF scaleSize)
        {
            Logger?.LogInformation($"Building {NumberOfChunks} chunks...");
            // Build our chunks.
            List<Chunk> chunks = new List<Chunk>(NumberOfChunks);
            for (int i = 0; i < NumberOfChunks; i++)
            {
                chunks.Add(new Chunk(new Point(0, ImageSize.Height / NumberOfChunks * i), new Point(ImageSize.Width, ImageSize.Height / NumberOfChunks * (i + 1))));
            }

            Logger?.LogInformation($"Build chunks.");
            Logger?.LogInformation($"Creating and assigning chunks, this may take a while...");

            // Create and assign tasks (as we can).
            List<Task<Result>> tasks = new List<Task<Result>>();
            while (chunks.Count > 0)
            {
                if (tasks.Where(x => x.Status != TaskStatus.RanToCompletion).ToList().Count < NumberOfCores)
                {
                    if (chunks.Count > 0)
                    {
                        Logger?.LogInformation($"Assigning chunk...");
                        Task<Result> getSection = GenerateSectionAsync(Logger, chunks[0], center, scaleSize, MaxIterations);
                        chunks.Remove(chunks[0]);
                        tasks.Add(getSection);
                    }
                }

                System.Threading.Thread.Sleep(1);
            }

            Logger?.LogInformation($"Last chunk assigned, waiting for results.");

            // Create the main results
            ushort[] results = new ushort[ImageSize.Width * ImageSize.Height];

            Func<int, int, int> pointToIndex = delegate (int x, int y) { return y * ImageSize.Width + x; };

            // Make sure we finish our tasks and add them to our results.
            while (tasks.Count > 0)
            {
                var finishedTasks = tasks.Where(x => x.Status == TaskStatus.RanToCompletion).ToList();

                foreach (var finishedTask in finishedTasks)
                {
                    Result result = finishedTask.Result;

                    for (int y = result.Chunk.Start.Y; y < result.Chunk.End.Y; y++)
                    {
                        for (int x = 0; x < ImageSize.Width; x++)
                        {
                            results[pointToIndex(x, y)] = result.Data[pointToIndex(x, (y - result.Chunk.Start.Y))];
                        }
                    }

                    tasks.Remove(finishedTask);
                }

                System.Threading.Thread.Sleep(1);
            }
            
            Logger?.LogInformation($"Results computed.");

            return results;
        }

        public static Task<Result> GenerateSectionAsync(ILogger logger, Chunk chunk, Point center, SizeF scaleSize, ushort maxIterations)
        {
            return Task.Run(() =>
            {
                return GenerateSection(logger, chunk, center, scaleSize, maxIterations);
            });
        }

        private static Result GenerateSection(ILogger logger, Chunk chunk, Point center, SizeF scaleSize, ushort maxIterations)
        {
            logger?.LogInformation($"Generating section {chunk.Start.ToString()}:{chunk.End.ToString()}.");
            int startRow = chunk.Start.Y;
            int endRow = chunk.End.Y;

            int startColumn = chunk.Start.X;
            int endColumn = chunk.End.X;

            int height = endRow - startRow;
            int width = endColumn - startColumn;

            ushort[] results = new ushort[height * width];

            // We'll need all of these later.
            int relativeRow = 0;
            ushort iteration = 0;
            double xTemp = 0;
            double x0 = 0f;
            double y0 = 0f;
            double xL = 0f;
            double yL = 0f;
            double xSquared = 0;
            double ySquared = 0;

            double negCenterX = -center.X;
            double negCenterY = -center.Y;
            double reciprocalScaleSizeWidth = 1 / scaleSize.Width;
            double reciprocalScaleSizeHeight = 1 / scaleSize.Height;

            for (int y = startRow; y < endRow; y++)
            {
                for (int x = startColumn; x < endColumn; x++)
                {
                    // The formula for a mandelbrot is z = z^2 + c, basically. We must relate that in code.
                    x0 = (x + negCenterX) * reciprocalScaleSizeWidth;
                    y0 = (y + negCenterY) * reciprocalScaleSizeHeight;
                    xL = 0;
                    yL = 0;

                    iteration = 0;
                    xSquared = xL * xL;
                    ySquared = yL * yL;
                    xTemp = 0;

                    while (xSquared + ySquared < twoSquared && iteration < maxIterations)
                    {
                        xTemp = xSquared - ySquared + x0;
                        yL = 2 * xL * yL + y0;
                        xL = xTemp;
                        iteration++;
                        xSquared = xL * xL;
                        ySquared = yL * yL;
                    }

                    results[relativeRow * width + x] = iteration;
                }

                relativeRow++;
            }

            logger?.LogInformation($"Section {chunk.Start.ToString()}:{chunk.End.ToString()} generated, returning.");
            return new Result(chunk, results);
        }
    }
}
