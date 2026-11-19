using Subtitulos.Audio;
using Subtitulos.Speech;
using TMPro;
using UnityEngine;

namespace Subtitulos.UI
{
    /// <summary>
    /// Ícono de micrófono que dice en qué estado está la app (visibilidad del estado del sistema):
    ///   • gris    → cargando
    ///   • verde   → escuchando (late suavemente cuando detecta voz)
    ///   • rojo    → error (sin micrófono, sin modelo…)
    /// Debajo, una etiqueta corta con el idioma activo (ES / EN / AUTO).
    /// </summary>
    public class ListeningIndicator : MonoBehaviour
    {
        [SerializeField] private VoskTranscriber transcriber;
        [SerializeField] private MicrophoneCapture microphone;
        [SerializeField] private TextMeshProUGUI icon;
        [SerializeField] private TextMeshProUGUI languageLabel;

        private static readonly Color Loading = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color Listening = new Color32(0x3C, 0xCB, 0x9A, 0xFF);
        private static readonly Color Error = new Color32(0xF2, 0x6B, 0x5B, 0xFF);

        private void Update()
        {
            if (icon == null || transcriber == null) return;

            bool error = transcriber.HasError || (microphone != null && !string.IsNullOrEmpty(microphone.Error));
            Color color;
            if (error) { color = Error; icon.text = Icons.MicOff; }
            else if (!transcriber.IsReady) { color = Loading; icon.text = Icons.Mic; }
            else
            {
                icon.text = Icons.Mic;
                bool speaking = Time.time - transcriber.LastSpeechTime < 0.6f;
                float pulse = speaking ? 0.75f + 0.25f * Mathf.Sin(Time.time * 10f) : 1f;
                color = Listening;
                color.a = pulse;
            }
            icon.color = Color.Lerp(icon.color, color, 1f - Mathf.Exp(-10f * Time.deltaTime));

            if (languageLabel == null) return;
            string text;
            switch (transcriber.Language)
            {
                case RecognitionLanguage.Spanish: text = "ES"; break;
                case RecognitionLanguage.English: text = "EN"; break;
                default: text = "AUTO"; break;
            }
            if (languageLabel.text != text) languageLabel.text = text;
        }
    }
}
