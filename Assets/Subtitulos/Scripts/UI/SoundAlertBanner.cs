using Subtitulos.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

namespace Subtitulos.UI
{
    /// <summary>
    /// Aviso de sonido fuerte lo menos invasivo posible: un ícono pequeño en el riel lateral
    /// (fuera del centro de la vista) que pulsa unos segundos, más una vibración corta en los
    /// controles. No lleva texto ni tapa los subtítulos. Un golpe pulsa una vez; un sonido
    /// continuo (alarma, sirena) pulsa más tiempo.
    /// </summary>
    public class SoundAlertBanner : MonoBehaviour
    {
        [SerializeField] private LoudSoundDetector detector;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Opcional. Si existe, se deja vacío (el aviso es solo el ícono).")]
        [SerializeField] private TextMeshProUGUI label;

        [SerializeField] private float impactSeconds = 2f;
        [SerializeField] private float sustainedSeconds = 5f;
        [SerializeField] private float fadeSpeed = 8f;
        [SerializeField] private bool vibrate = true;

        private float _hideAt = -1f;

        private void OnEnable()
        {
            if (detector != null) detector.SoundDetected += Show;
            if (group != null) group.alpha = 0f;
            if (label != null) label.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (detector != null) detector.SoundDetected -= Show;
        }

        private void Show(LoudSoundDetector.SoundKind kind, float levelDb)
        {
            bool sustained = kind == LoudSoundDetector.SoundKind.Sustained;
            _hideAt = Time.time + (sustained ? sustainedSeconds : impactSeconds);
            if (vibrate) Vibrate(sustained ? 0.8f : 0.5f, sustained ? 0.5f : 0.2f);
        }

        private static void Vibrate(float amplitude, float seconds)
        {
            foreach (XRNode node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                InputDevice device = InputDevices.GetDeviceAtXRNode(node);
                if (device.isValid) device.SendHapticImpulse(0, amplitude, seconds);
            }
        }

        private void Update()
        {
            if (group == null) return;

            bool visible = _hideAt > 0f && Time.time < _hideAt;
            float target = visible ? 0.7f + 0.3f * Mathf.Sin(Time.time * 7f) : 0f;
            group.alpha = Mathf.Lerp(group.alpha, target, 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime));
        }
    }
}
