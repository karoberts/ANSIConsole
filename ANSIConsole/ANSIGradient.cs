using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ANSIConsole
{
    public enum GradientBlendMode
    {
        Perceptual,
        Linear
    }

    internal static class ANSIGradient
    {
        private struct LinearColor
        {
            public double R, G, B;
            public LinearColor(double r, double g, double b)
            {
                R = r;
                G = g;
                B = b;
            }
        }

        // based on the excellent work shown here: https://stackoverflow.com/questions/22607043/color-gradient-algorithm
        internal static IEnumerable<Color> PerceptualSteps(Color color1, Color color2, int steps)
        {
            // Returns a sRGB value in the range [0,1] for linear input in [0,1].
            static double to_sRGB_f(double x)
                => (x <= 0.0031308d) ? 12.92d*x : 1.055d * Math.Pow(x, 1/2.4) - 0.055d;

            // Returns a sRGB value in the range [0,255] for linear input in [0,1]
            static int to_sRGB(double x)
                => (int)(255.9999 * to_sRGB_f(x));

            // Returns a linear value in the range [0,1] for sRGB input in [0,255].
            static double from_sRGB(int x)
            {
                double xp = x / 255.0d;
                double y;
                if (xp <= 0.04045d)
                    y = xp / 12.92d;
                else
                    y = Math.Pow((xp + 0.055d) / 1.055d, 2.4d);
                return y;
            }

            // linearly interpolate between two values
            static double lerp(double v1, double v2, double frac)
                => v1 * (1.0d - frac) + v2 * frac;

            const double gamma = .43d;
            const double inv_gamma = 1.0d / gamma; 
            var color1_lin = new LinearColor(from_sRGB(color1.R), from_sRGB(color1.G), from_sRGB(color1.B));
            var bright1 = Math.Pow(color1_lin.R + color1_lin.G + color1_lin.B, gamma);
            var color2_lin = new LinearColor(from_sRGB(color2.R), from_sRGB(color2.G), from_sRGB(color2.B));
            var bright2 = Math.Pow(color2_lin.R + color2_lin.G + color2_lin.B, gamma);
            for (int step = 0; step < steps; step++)
            {
                var frac = (double)step / (steps - 1);
                var intensity = Math.Pow(lerp(bright1, bright2, frac), inv_gamma);
                var red = lerp(color1_lin.R, color2_lin.R, frac);
                var green = lerp(color1_lin.G, color2_lin.G, frac);
                var blue = lerp(color1_lin.B, color2_lin.B, frac);
                var sum = red + green + blue;
                if (sum != 0)
                {
                    red = red * intensity / sum;
                    green = green * intensity / sum;
                    blue = blue * intensity / sum;
                }
                yield return Color.FromArgb(to_sRGB(red), to_sRGB(green), to_sRGB(blue));
            }
        }

        internal static IEnumerable<Color> LinearSteps(Color color1, Color color2, int steps)
        {
            // linearly interpolate between two values
            static double lerp(double v1, double v2, double frac)
                => v1 * (1.0d - frac) + v2 * frac;

            for (int step = 0; step < steps; step++)
            {
                var frac = (double)step / (steps - 1);
                var red = lerp(color1.R, color2.R, frac);
                var green = lerp(color1.G, color2.G, frac);
                var blue = lerp(color1.B, color2.B, frac);
                yield return Color.FromArgb((int)red, (int)green, (int)blue);
            }
        }

        internal static IEnumerable<ANSIString> AddGradient(string text, Color color, Color[] colors, bool isBg, Func<Color, Color, int, IEnumerable<Color>> stepsFunc)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield return new ANSIString(string.Empty);
                yield break;
            }
            else if (colors.Length == 0)
            {
                yield return new ANSIString(text);
                yield break;
            }
            else if (colors.Length == 1)
            {
                yield return isBg ? text.Background(colors[0]).Color(color) : text.Color(colors[0]).Background(color);
                yield break;
            }
            if (text.Length < colors.Length)
                throw new ArgumentException("Cannot have more color arguments than characters in the text");

            int colorPrevIndex = 0;
            int colorNextIndex = 1;

            // steps is the amount of characters for the first color pair (colors[0]..colors[1])
            // each subsequent pairing gets one less step (so that we don't have the same unblended color at the end/start of the next section)
            // remainder steps for when text length isn't divisible by colors count, we distribute one by one, starting with color pair 2
             
            int steps = (int)Math.Ceiling((double)text.Length / (colors.Length - 1));
            int stepsLeftOver = text.Length - steps - (steps - 1) * (colors.Length - 2);
            var stepsPerColor = new List<int>(colors.Length) { steps };
            for (int i = 0; i < colors.Length - 1; i++)
            {
                if (stepsLeftOver > 0)
                {
                    stepsPerColor.Add(steps + 1);
                    stepsLeftOver--;
                }
                else
                    stepsPerColor.Add(steps);
            }

            var stepIdx = 0;
            var gradColors = stepsFunc(colors[colorPrevIndex], colors[colorNextIndex], stepsPerColor[stepIdx++]);
            var gradEnum = gradColors.GetEnumerator();

            foreach (var c in text)
            {
                if (!gradEnum.MoveNext())
                {
                    colorPrevIndex++;
                    colorNextIndex++;
                    gradColors = stepsFunc(colors[colorPrevIndex], colors[colorNextIndex], stepsPerColor[stepIdx++]);
                    gradEnum = gradColors.GetEnumerator();
                    gradEnum.MoveNext(); // skip one to avoid the same color shown twice
                    gradEnum.MoveNext();
                }

                if (isBg)
                    yield return c.ToString().Background(gradEnum.Current).Color(color);
                else
                    yield return c.ToString().Color(gradEnum.Current).Background(color);
            }
        }
    }
}
