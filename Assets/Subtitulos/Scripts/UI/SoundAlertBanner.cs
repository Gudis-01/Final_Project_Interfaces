using Subtitulos.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Subtitulos.UI
{
    /// <summary>
    /// Aviso visual cuando el LoudSoundDetector escucha un sonido fuerte.
    /// Aparece arriba de los subtítulos con un pulso suave (llama la atención
    /// sin ser molesto) y se desvanece solo.
    /// </summary>
    public class SoundAlertBanner : MonoBehaviour
    {
        [SerializeField] private LoudSoundDetector detector;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI label;

        [SerializeField] private float visibleSeconds = 4f;
        [SerializeField] private float fadeSpeed = 8f;

        private float _hideAt = -1f;

        private void OnEnable()
        {
            if (detector != null) detector.SoundDetected += Show;
            if (group != null) group.alpha = 0f;
        }

        private void OnDisable()
        {
            if (detector != null) detector.SoundDetected -= Show;
        }

        private void Show(LoudSoundDetector.SoundKind kind, float levelDb)
        {
            if (label != null)
            {
                label.text = kind == LoudSoundDetector.SoundKind.Sustained
                    ? "Sonido fuerte continuo · alarma, sirena o timbre"
                    : "Golpe o sonido fuerte cerca";
            }
            _hideAt = Time.time + visibleSeconds;
        }

        private void Update()
        {
            if (group == null) return;

            bool visible = _hideAt > 0f && Time.time < _hideAt;
            // Pulso suave entre 75% y 100% mientras está visible.
            float target = visible ? 0.875f + 0.125f * Mathf.Sin(Time.time * 5f) : 0f;
            group.alpha = Mathf.Lerp(group.alpha, target, 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));
        }
    }
}
