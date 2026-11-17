using UnityEngine;

namespace Subtitulos.UI
{
    /// <summary>
    /// Colores de la app en un solo lugar.
    ///
    /// Estilo: "vidrio oscuro" (fondos casi negros semitransparentes) con texto blanco,
    /// para que se lea bien sobre cualquier fondo del mundo real sin tapar demasiado.
    ///
    /// Los colores de hablantes se basan en la paleta Okabe-Ito (pensada para
    /// daltonismo), aclarados para que resalten sobre fondo oscuro. Además del color,
    /// cada hablante tiene etiqueta (P1, P2...) para no depender solo del color.
    /// </summary>
    public static class SubtitlePalette
    {
        private static readonly Color[] SpeakerColors =
        {
            new Color32(0x5B, 0xB8, 0xF0, 0xFF), // azul      (P1)
            new Color32(0x3C, 0xCB, 0x9A, 0xFF), // verde     (P2)
            new Color32(0xF5, 0xA5, 0x24, 0xFF), // naranja   (P3)
            new Color32(0xE5, 0x8F, 0xC0, 0xFF), // rosa      (P4)
            new Color32(0xF0, 0xE4, 0x42, 0xFF), // amarillo  (P5)
            new Color32(0xB3, 0x9D, 0xF5, 0xFF), // lila      (P6)
        };

        public static readonly Color Neutral = new Color(0.62f, 0.64f, 0.68f, 1f);

        public static readonly Color Text = new Color(0.97f, 0.97f, 0.98f, 1f);
        public static readonly Color TextSecondary = new Color(0.97f, 0.97f, 0.98f, 0.62f);

        /// <summary>Fondo de vidrio oscuro para burbujas y paneles.</summary>
        public static readonly Color Glass = new Color(0.07f, 0.07f, 0.09f, 0.74f);
        public static readonly Color GlassStrong = new Color(0.09f, 0.09f, 0.11f, 0.92f);
        public static readonly Color Row = new Color(1f, 1f, 1f, 0.07f);
        public static readonly Color Separator = new Color(1f, 1f, 1f, 0.10f);

        public static readonly Color Accent = new Color32(0x5B, 0xB8, 0xF0, 0xFF);
        public static readonly Color Alert = new Color32(0xFF, 0xC4, 0x3D, 0xFF);
        public static readonly Color AlertText = new Color(0.10f, 0.08f, 0.02f, 1f);

        public static readonly Color VolumeQuiet = new Color(1f, 1f, 1f, 0.35f);
        public static readonly Color VolumeNormal = new Color32(0x3C, 0xCB, 0x9A, 0xFF);
        public static readonly Color VolumeLoud = new Color32(0xF5, 0x7C, 0x3A, 0xFF);
        public static readonly Color Track = new Color(1f, 1f, 1f, 0.12f);

        public static Color Speaker(int speakerId) =>
            speakerId < 0 ? Neutral : SpeakerColors[speakerId % SpeakerColors.Length];

        public static string SpeakerLabel(int speakerId) => speakerId < 0 ? "" : "P" + (speakerId + 1);

        /// <summary>Fondo de cada burbuja: vidrio oscuro con un toque del color del hablante.</summary>
        public static Color BubbleBackground(int speakerId, bool highContrast, bool speakerColors)
        {
            if (highContrast) return Color.black;
            if (!speakerColors || speakerId < 0) return Glass;
            Color tinted = Color.Lerp(Glass, Speaker(speakerId), 0.16f);
            tinted.a = Glass.a;
            return tinted;
        }

        public static string ToHex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
