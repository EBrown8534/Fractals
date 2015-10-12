using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fractal_Generator.Mandelbrot
{
    public class Result
    {
        public Chunk Chunk { get; }
        public ushort[] Data { get; }

        public Result(Chunk chunk, ushort[] data)
        {
            Chunk = chunk;
            Data = data;
        }
    }
}
