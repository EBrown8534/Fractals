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

        public int NumberOfCores { get; set; }
        public int NumberOfChunks { get; set; }
        public ushort MaxIterations { get; set; }
        public Size ImageSize { get; set; }
        public int NumberOfColors { get; set; }
        public bool BlackFinalColor { get; set; }

        private readonly Func<int, int, int> pointToIndex;

        public Generator(int numberOfCores,
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

            pointToIndex = delegate (int x, int y) { return y * ImageSize.Width + x; };
        }

        public Color[] BuildColors()
        {
            Color[] colors = new Color[NumberOfColors];

            for (int i = 0; i < NumberOfColors - 1; i++)
            {
                colors[i] = Color.FromArgb(255, 0, 0, (i + 1) * (256 / NumberOfColors));
            }

            if (BlackFinalColor)
            {
                colors[NumberOfColors - 1] = Color.FromArgb(255, 0, 0, 0);
            }

            return colors;
        }

        public Image BuildImage(ushort[] results)
        {
            Color[] colors = BuildColors();

            using (Bitmap image = new Bitmap(ImageSize.Width, ImageSize.Height))
            {
                for (int y = 0; y < ImageSize.Height; y++)
                {
                    for (int x = 0; x < ImageSize.Width; x++)
                    {
                        image.SetPixel(x, y, colors[results[pointToIndex(x, y)] / (int)(Math.Ceiling(MaxIterations / (double)NumberOfColors))]);
                    }
                }

                return image;
            }
        }
    }
}
