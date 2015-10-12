using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fractal_Generator.Mandelbrot
{
    public struct Chunk
    {
        public Point Start { get; }
        public Point End { get; }

        public Chunk(Point start, Point end)
        {
            Start = start;
            End = end;
        }
    }
}
