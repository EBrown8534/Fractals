using Evbpc.Framework.Utilities.Logging;
using Evbpc.Framework.Utilities.Prompting;
using Fractal_Generator.Mandelbrot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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
            ushort maxIterations = prompt.Prompt<ushort>("Enter the maximum number of iterations",
                                                         PromptOptions.Optional,
                                                         1000);

            // Let us consider a `width` and `height` of the generated image.
            Size imageSize = new Size(4096, 2340);

            int imageWidth = prompt.Prompt("Enter the image width",
                                           PromptOptions.Optional,
                                           imageSize.Width);

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
                double centerX = prompt.Prompt("Center X coordinate from [-2.5, 1.0]",
                                               PromptOptions.Optional,
                                               0.0,
                                               null,
                                               delegate (double val) { return val >= -2.5 && val <= 1.0; },
                                               delegate (double val) { return -val; });

                double centerY = prompt.Prompt("Center Y coordinate from [-1.0, 1.0]",
                                               PromptOptions.Optional,
                                               0.0,
                                               null,
                                               delegate (double val) { return val >= -1.0 && val <= 1.0; });

                scale = prompt.Prompt("Enter the scale to render from [1.0,inf)",
                                      PromptOptions.Optional,
                                      1.0,
                                      null,
                                      delegate (double val) { return val >= 1.0; });

                SizeF oScaleSize = scaleSize;
                scaleSize = new SizeF(scaleSize.Width * (float)scale, scaleSize.Height * (float)scale);
                center = new Point((int)(scaleSize.Width * 2.5f * (centerX / 2.5 + 1 / scale)), (int)(scaleSize.Height * (centerY + 1 / scale)));

                inputCenter = new PointF((float)-centerX, (float)centerY);
            }

            int numberOfCores = prompt.Prompt($"Enter the number of cores to use (1 to {Environment.ProcessorCount})",
                                              PromptOptions.Optional,
                                              Environment.ProcessorCount,
                                              $"The value must be between 1 and {Environment.ProcessorCount}",
                                              delegate (int x) { return x >= 1 && x <= Environment.ProcessorCount; });

            // Setup the number of chunks to break into.
            int numberOfChunks = prompt.Prompt("Enter the number of chunks to break into", PromptOptions.Optional, numberOfCores * 2);

            int numberOfColors = prompt.Prompt("Enter the number of colors to render",
                                               PromptOptions.Optional,
                                               32,
                                               "The value must be between 2 and 256",
                                               delegate (int x) { return x >= 2 && x <= 256; });

            bool blackFinalColor = prompt.Prompt("Use black for max iteration color?",
                                                 PromptOptions.Optional,
                                                 'Y',
                                                 null,
                                                 validateYesNo,
                                                 charToUpper) == 'Y';

            Console.WriteLine("");

            Console.WriteLine($"Creating Mandelbrot image of size ({imageSize.Width},{imageSize.Height}) and max iteration count of {maxIterations}, splitting the image into {numberOfChunks} sections across {numberOfCores} cores.");


            Stopwatch sw = new Stopwatch();
            sw.Start();

            Generator g = new Generator(new ConsoleLogger(LoggingType.All), numberOfCores, numberOfChunks, maxIterations, imageSize, numberOfColors, blackFinalColor);
            ushort[] results = g.Generate(center, scaleSize);

            sw.Stop();
            Console.WriteLine($"Took {sw.ElapsedMilliseconds}ms.");
            Console.WriteLine("Mandelbrot created, building image...");

            string filename = prompt.Prompt("Enter a file name to save as",
                                            PromptOptions.Optional,
                                            $"{inputCenter.ToString()}@{scale}.png");

            using (Bitmap image = g.BuildImage(results))
            {
                image.Save(filename, ImageFormat.Png);
            }

            Console.WriteLine("Image built.");

            if (prompt.Prompt("Open the image with your default image program (Y or N)",
                              PromptOptions.Optional,
                              'N',
                              "Please enter only 'Y' or 'N'",
                              validateYesNo,
                              charToUpper) == 'Y')
            {
                Process.Start(filename);
            }

            Console.WriteLine("Press enter to quit...");
            Console.ReadLine();
        }
    }
}
