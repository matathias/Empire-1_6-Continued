using System;
using UnityEngine;

namespace FactionColonies
{
    /// <summary>
    /// A utility class for agnostic Color transformations.
    /// <para>Differs from <see cref="AccentUtil"/> in that ColorUtil's methods don't care about state outside of this class.</para>
    /// </summary>
    public static class ColorUtil
    {
        public static readonly Color Gray9 = new Color(0.9f, 0.9f, 0.9f);
        public static readonly Color Gray8 = new Color(0.8f, 0.8f, 0.8f);
        public static readonly Color Gray7 = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color Gray6 = new Color(0.6f, 0.6f, 0.6f);
        public static readonly Color Gray5 = new Color(0.5f, 0.5f, 0.5f);
        public static readonly Color Gray4 = new Color(0.4f, 0.4f, 0.4f);
        public static readonly Color Gray3 = new Color(0.3f, 0.3f, 0.3f);
        public static readonly Color Gray2 = new Color(0.2f, 0.2f, 0.2f);
        public static readonly Color Gray1 = new Color(0.1f, 0.1f, 0.1f);

        public static readonly Color Gold = new Color(0.83f, 0.68f, 0.21f);

        /// <summary>
        /// Returns a new color with each of its rgb/a values multiplied by the provided factors. The post-multiplication values are clamped to the [0,1] range.
        /// </summary>
        /// <param name="c">Color to transform</param>
        /// <param name="rmag">Factor to multiply into <paramref name="c"/>'s red element</param>
        /// <param name="gmag">Factor to multiply into <paramref name="c"/>'s green element</param>
        /// <param name="bmag">Factor to multiply into <paramref name="c"/>'s blue element</param>
        /// <param name="amag">Factor to multiply into <paramref name="c"/>'s alpha (transparency) element</param>
        /// <returns></returns>
        public static Color Transform(Color c, float rmag, float gmag, float bmag, float amag) => new Color(Math.Clamp(c.r * rmag, 0f, 1f),
            Math.Clamp(c.g * gmag, 0f, 1f),
            Math.Clamp(c.b * bmag, 0f, 1f),
            Math.Clamp(c.a * amag, 0f, 1f));

        /// <summary>
        /// Returns a new color with its rgb and alpha values multiplied by the provided factors. The post-multiplication values are clamped to the [0,1] range.
        /// </summary>
        /// <param name="c">Color to transform</param>
        /// <param name="cmag">Factor to multiply into <paramref name="c"/>'s rgb elements</param>
        /// <param name="amag">Factor to multiply into <paramref name="c"/>'s alpha (transparency) element</param>
        /// <returns></returns>
        public static Color Transform(Color c, float cmag, float amag) => Transform(c, cmag, cmag, cmag, amag);

        /// <summary>
        /// Returns a new color with its rgb values multiplied by the provided factor. The post-multiplication values are clamped to the [0,1] range.
        /// </summary>
        /// <param name="c">Color to transform</param>
        /// <param name="mag">Factor to multiply into <paramref name="c"/>'s rgb elements</param>
        /// <returns></returns>
        public static Color TransformRGB(Color c, float mag) => Transform(c, mag, 1f);

        /// <summary>
        /// Returns a new color with its alpha value multiplied by the provided factor. The post-multiplication value is clamped to the [0,1] range.
        /// </summary>
        /// <param name="c">Color to transform</param>
        /// <param name="mag">Factor to multiply into <paramref name="c"/>'s alpha element</param>
        /// <returns></returns>
        public static Color TransformA(Color c, float mag) => Transform(c, 1f, mag);
        /// <summary>
        /// Returns a new color with its red value multiplied by the provided factor. The post-multiplication value is clamped to the [0,1] range.
        /// </summary>
        /// <param name="c">Color to transform</param>
        /// <param name="mag">Factor to multiply into <paramref name="c"/>'s red element</param>
        /// <returns></returns>
        public static Color TransformR(Color c, float mag) => Transform(c, mag, 1f, 1f, 1f);
        /// <summary>
        /// Returns a new color with its green value multiplied by the provided factor. The post-multiplication value is clamped to the [0,1] range.
        /// </summary>
        /// <param name="c">Color to transform</param>
        /// <param name="mag">Factor to multiply into <paramref name="c"/>'s green element</param>
        /// <returns></returns>
        public static Color TransformG(Color c, float mag) => Transform(c, 1f, mag, 1f, 1f);
        /// <summary>
        /// Returns a new color with its blue value multiplied by the provided factor. The post-multiplication value is clamped to the [0,1] range.
        /// </summary>
        /// <param name="c">Color to transform</param>
        /// <param name="mag">Factor to multiply into <paramref name="c"/>'s blue element</param>
        /// <returns></returns>
        public static Color TransformB(Color c, float mag) => Transform(c, 1f, 1f, mag, 1f);


        /// <summary>
        /// Returns a new color with its alpha value set to the provided <paramref name="a"/> value.
        /// <para>Differs from <see cref="TransformA"/> in that SetA directly sets the alpha value, while TransformA will multiply the alpha value by a factor.</para>
        /// </summary>
        /// <param name="c">Color to transform</param>
        /// <param name="a">Value to set <paramref name="c"/>'s alpha to</param>
        /// <returns></returns>
        public static Color SetA(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>
        /// Returns a new color with the same rgb values as <paramref name="c"/> and alpha = 1
        /// </summary>
        /// <param name="c">Color to transform</param>
        /// <returns></returns>
        public static Color Opaque(Color c) => SetA(c, 1f);

        /// <summary>
        /// Returns a new color based on the given color with alpha 1, and the rgb values multiplied by mag.
        /// </summary>
        /// <param name="c">Color to transform</param>
        /// <param name="mag">Value to multiple rgb values with</param>
        /// <returns></returns>
        public static Color Bold(Color c, float mag) => Opaque(TransformRGB(c, mag));
    }
}
