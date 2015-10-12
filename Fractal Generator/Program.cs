using Evbpc.Framework.Utilities.Logging;
using Evbpc.Framework.Utilities.Prompting;
using Fractal_Generator.Mandelbrot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fractal_Generator
{
    class Program
    {
        // To avoid the need for unnecessary multiplication (and the appearance of magic numbers), we'll make a constant `twoSquared`:
        const double twoSquared = 2 * 2;

        static void Main(string[] args)
        {
            string version = $"{typeof(Program).Assembly.GetName().Version}";
            Console.WriteLine($"Mandelbrot Generator version: {version}");
            IPrompt prompt = new ConsolePrompt(new EmptyLogger());
            // Wooo! Mandelbrots! I miss the old programme though. It was probably better.
            // That said, we'll get this thing on CR so they can tell us how badly we fkd up.

            // Currently an arbitrary number.
            ushort maxIterations = 1000;
            maxIterations = prompt.Prompt("Enter the maximum number of iterations", PromptOptions.Optional, maxIterations);

            // Let us consider a `width` and `height` of the generated image.
            Size imageSize = new Size(4096, 2340);
            int imageWidth = prompt.Prompt("Enter the image width", PromptOptions.Optional, imageSize.Width);
            imageSize = new Size(imageWidth, (int)(imageWidth / 3.5 * 2));

            // We need to fit the brot to [-2.5, 1], not sure how to do that yet.

            // Next, consider `xCenter` and `yCenter` which represent what pixel is `(0,0)` in the specified image size.

            SizeF scaleSize = new SizeF(imageSize.Width / 3.5f, imageSize.Height / 2);
            Point center = new Point((int)(scaleSize.Width * 2.5f), (int)scaleSize.Height);
            PointF inputCenter = new PointF(0, 0);
            double scale = 1;

            Func<char, bool> validateYesNo = delegate (char c) { return c == 'Y' || c == 'N' || c == 'y' || c == 'n'; };
            Func<char, char> charToUpper = delegate (char c) { if (c >= 'a' && c <= 'z') { return (char)(c - 'a' + 'A'); } return c; };

            if (prompt.Prompt("Render a specific section", PromptOptions.Optional, 'N', null, validateYesNo, charToUpper) == 'Y')
            {
                double centerX = prompt.Prompt("Center X coordinate from [-2.5, 1.0]", PromptOptions.Optional, 0.0, null, delegate (double val) { return val >= -2.5 && val <= 1.0; }, delegate (double val) { return -val; });
                double centerY = prompt.Prompt("Center Y coordinate from [-1.0, 1.0]", PromptOptions.Optional, 0.0, null, delegate (double val) { return val >= -1.0 && val <= 1.0; });
                scale = prompt.Prompt("Enter the scale to render from [1.0,inf)", PromptOptions.Optional, 1.0, null, delegate (double val) { return val >= 1.0; });

                SizeF oScaleSize = scaleSize;
                scaleSize = new SizeF(scaleSize.Width * (float)scale, scaleSize.Height * (float)scale);
                center = new Point((int)(scaleSize.Width * 2.5f * (centerX / 2.5 + 1 / scale)), (int)(scaleSize.Height * (centerY + 1 / scale)));

                inputCenter = new PointF((float)-centerX, (float)centerY);
            }

            int numberOfCores = Environment.ProcessorCount;
            numberOfCores = prompt.Prompt($"Enter the number of cores to use (1 to {Environment.ProcessorCount})",
                                                 PromptOptions.Optional,
                                                 numberOfCores,
                                                 $"The value must be between 1 and {Environment.ProcessorCount}",
                                                 delegate (int x) { return x >= 1 && x <= Environment.ProcessorCount; });

            // Setup the number of chunks to break into.
            int numberOfChunks = numberOfCores * 2;
            numberOfChunks = prompt.Prompt("Enter the number of chunks to break into", PromptOptions.Optional, numberOfChunks);

            int numberOfColors = 32;
            numberOfColors = prompt.Prompt("Enter the number of colors to render",
                                                  PromptOptions.Optional,
                                                  numberOfColors,
                                                  "The value must be between 2 and 256",
                                                  delegate (int x) { return x >= 2 && x <= 256; });

            Console.WriteLine("");

            Console.WriteLine($"Creating Mandelbrot image of size ({imageSize.Width},{imageSize.Height}) and max iteration count of {maxIterations}, splitting the image into {numberOfChunks} sections across {numberOfCores} cores.");

            Stopwatch sw = new Stopwatch();
            sw.Start();

            // Build our chunks.
            List<Chunk> chunks = new List<Chunk>(numberOfChunks);
            for (int i = 0; i < numberOfChunks; i++)
            {
                chunks.Add(new Chunk(new Point(0, imageSize.Height / numberOfChunks * i), new Point(imageSize.Width, imageSize.Height / numberOfChunks * (i + 1))));
            }

            // Create and assign tasks (as we can).
            List<Task<Result>> tasks = new List<Task<Result>>();
            while (chunks.Count > 0)
            {
                if (tasks.Where(x => x.Status != TaskStatus.RanToCompletion).ToList().Count < numberOfCores)
                {
                    if (chunks.Count > 0)
                    {
                        Task<Result> getSection = GenerateSectionAsync(chunks[0], center, scaleSize, maxIterations);
                        chunks.Remove(chunks[0]);
                        tasks.Add(getSection);
                    }
                }

                System.Threading.Thread.Sleep(1);
            }

            Console.WriteLine("Last chunk assigned, waiting for results.");

            // Create the main results
            ushort[] results = new ushort[imageSize.Width * imageSize.Height];

            Func<int, int, int> pointToIndex = delegate (int x, int y) { return y * imageSize.Width + x; };

            // Make sure we finish our tasks and add them to our results.
            while (tasks.Count > 0)
            {
                var finishedTasks = tasks.Where(x => x.Status == TaskStatus.RanToCompletion).ToList();

                foreach (var finishedTask in finishedTasks)
                {
                    Result result = finishedTask.Result;

                    for (int y = result.Chunk.Start.Y; y < result.Chunk.End.Y; y++)
                    {
                        for (int x = 0; x < imageSize.Width; x++)
                        {
                            results[pointToIndex(x, y)] = result.Data[pointToIndex(x, (y - result.Chunk.Start.Y))];
                        }
                    }

                    tasks.Remove(finishedTask);
                }

                System.Threading.Thread.Sleep(1);
            }

            sw.Stop();
            Console.WriteLine("Took {0}ms.", sw.ElapsedMilliseconds);
            Console.WriteLine("Mandelbrot created, building image...");

            // Create our colours.
            Color[] colors = new Color[numberOfColors];
            for (int i = 0; i < numberOfColors - 1; i++)
            {
                colors[i] = Color.FromArgb(255, 0, 0, (i + 1) * (256 / numberOfColors));
            }

            if (prompt.Prompt("Use black for max iteration color?", PromptOptions.Optional, 'Y', null, validateYesNo, charToUpper) == 'Y')
            {
                colors[numberOfColors - 1] = Color.FromArgb(255, 0, 0, 0);
            }

            string filename = prompt.Prompt("Enter a file name to save as", PromptOptions.Optional, inputCenter.ToString() + "@" + scale + ".png");
            // Create our image.
            using (Bitmap image = new Bitmap(imageSize.Width, imageSize.Height))
            {
                for (int y = 0; y < imageSize.Height; y++)
                {
                    for (int x = 0; x < imageSize.Width; x++)
                    {
                        image.SetPixel(x, y, colors[results[pointToIndex(x, y)] / (int)(Math.Ceiling(maxIterations / (double)numberOfColors))]);
                    }
                }

                image.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
            }

            Console.WriteLine("Image built.");

            if (prompt.Prompt("Open the image with your default image program (Y or N)", PromptOptions.Optional, 'N', "Please enter only 'Y' or 'N'", validateYesNo, charToUpper) == 'Y')
            {
                Process.Start(filename);
            }

            Console.WriteLine("Press enter to quit...");
            Console.ReadLine();
        }

        public static Task<Result> GenerateSectionAsync(Chunk chunk, Point center, SizeF scaleSize, ushort maxIterations)
        {
            return Task.Run(() =>
            {
                return GenerateSection(chunk, center, scaleSize, maxIterations);
            });
        }

        private static Result GenerateSection(Chunk chunk, Point center, SizeF scaleSize, ushort maxIterations)
        {
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

            return new Result(chunk, results);
        }
    }
}
