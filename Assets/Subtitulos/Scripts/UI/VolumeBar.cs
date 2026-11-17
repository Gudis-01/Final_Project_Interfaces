using Subtitulos.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Subtitulos.UI
{
    /// <summary>
    /// Barra vertical que muestra qué tan fuerte es el sonido alrededor, para que la persona
    /// sepa si le hablan bajito, normal o gritando. Funciona igual horizontal o vertical:
    /// la orientación la define el Image "fill" (Fill Method).
    /// </summary>
    public class VolumeBar : MonoBehaviour
    {
        [SerializeField] private MicrophoneCapture microphone;
        [Tooltip("Image con Image Type = Filled.")]
        [SerializeField] private Image fill;
        [Tooltip("Opcional: texto corto debajo de la barra (Bajo / Normal / Alto).")]
        [SerializeField] private TextMeshProUGUI label;

        [Header("Rangos (dBFS)")]
        [Tooltip("Por debajo de esto se considera bajo / silencio.")]
        [SerializeField] private float quietDb = -50f;
        [Tooltip("Por encima de esto se considera fuerte.")]
        [SerializeField] private float loudDb = -20f;

        [Tooltip("Suavizado de la barra. Más alto = reacciona más rápido.")]
        [SerializeField] private float smoothing = 12f;

        private float _displayed;
        private Color _color = SubtitlePalette.VolumeQuiet;

        private void Update()
        {
            if (microphone == null || fill == null) return;

            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            _displayed = Mathf.Lerp(_displayed, microphone.NormalizedLevel, t);
            fill.fillAmount = _displayed;

            float db = microphone.LevelDb;
            Color target;
            string text;
            if (db < quietDb) { target = SubtitlePalette.VolumeQuiet; text = "Bajo"; }
            else if (db < loudDb) { target = SubtitlePalette.VolumeNormal; text = "Normal"; }
            else { target = SubtitlePalette.VolumeLoud; text = "Alto"; }

            _color = Color.Lerp(_color, target, t);
            fill.color = _color;

            // El texto acompaña al color para no depender solo del color.
            if (label != null && label.text != text) label.text = text;
        }
    }
}
