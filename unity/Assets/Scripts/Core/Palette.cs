using UnityEngine;

namespace Procon
{
    /// <summary>Paleta do jogo, herdada da versao HTML.</summary>
    public static class Palette
    {
        public static readonly Color Ink = Hex("101733");
        public static readonly Color Ink2 = Hex("26345E");
        public static readonly Color Paper = Hex("FFF8DF");
        public static readonly Color Paper2 = Hex("F2DFAA");
        public static readonly Color Blue = Hex("2463EB");
        public static readonly Color BlueDark = Hex("14378E");
        public static readonly Color Cyan = Hex("55D9E8");
        public static readonly Color Yellow = Hex("FFD84A");
        public static readonly Color Orange = Hex("F28C28");
        public static readonly Color Red = Hex("D83A4E");
        public static readonly Color RedDark = Hex("7F1E36");
        public static readonly Color Green = Hex("31B86B");
        public static readonly Color GreenDark = Hex("17663F");
        public static readonly Color White = Hex("FFFDF2");
        public static readonly Color Shadow = Hex("080D22");

        public static Color Hex(string rgb)
        {
            return ColorUtility.TryParseHtmlString("#" + rgb, out var color) ? color : Color.magenta;
        }

        public static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
