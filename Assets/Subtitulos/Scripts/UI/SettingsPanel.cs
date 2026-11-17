using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Subtitulos.UI
{
    /// <summary>
    /// Panel de ajustes de accesibilidad. Cada fila es un botón: al presionarla cambia
    /// al siguiente valor, y la etiqueta de la derecha siempre muestra el valor actual
    /// (retroalimentación inmediata, sin menús anidados).
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private AccessibilitySettings settings;
        [SerializeField] private SubtitleFeed feed;
        [SerializeField] private HeadFollowPanel follower;

        [Tooltip("Objeto que se muestra/oculta con el botón de ajustes.")]
        [SerializeField] private GameObject panelRoot;

        [Header("Botones")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button fontSmallerButton;
        [SerializeField] private Button fontBiggerButton;
        [SerializeField] private Button fontFaceButton;
        [SerializeField] private Button contrastButton;
        [SerializeField] private Button positionButton;
        [SerializeField] private Button speakerColorsButton;
        [SerializeField] private Button maxLinesButton;
        [SerializeField] private Button hideAfterButton;
        [SerializeField] private Button distantVoicesButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button recenterButton;

        [Header("Etiquetas de valor")]
        [SerializeField] private TextMeshProUGUI fontSizeValue;
        [SerializeField] private TextMeshProUGUI fontFaceValue;
        [SerializeField] private TextMeshProUGUI contrastValue;
        [SerializeField] private TextMeshProUGUI positionValue;
        [SerializeField] private TextMeshProUGUI speakerColorsValue;
        [SerializeField] private TextMeshProUGUI maxLinesValue;
        [SerializeField] private TextMeshProUGUI hideAfterValue;
        [SerializeField] private TextMeshProUGUI distantVoicesValue;

        private void Awake()
        {
            Bind(toggleButton, TogglePanel);
            Bind(closeButton, () => SetOpen(false));
            Bind(fontSmallerButton, () => settings.DecreaseFont());
            Bind(fontBiggerButton, () => settings.IncreaseFont());
            Bind(fontFaceButton, () => settings.NextFont());
            Bind(contrastButton, () => settings.ToggleContrast());
            Bind(positionButton, () => settings.NextPosition());
            Bind(speakerColorsButton, () => settings.ToggleSpeakerColors());
            Bind(maxLinesButton, () => settings.NextMaxLines());
            Bind(hideAfterButton, () => settings.NextHideAfter());
            Bind(distantVoicesButton, () => settings.ToggleDistantVoices());
            Bind(clearButton, () => { if (feed != null) feed.Clear(); });
            Bind(recenterButton, () => { if (follower != null) follower.Recenter(); });

            SetOpen(false);
        }

        private void OnEnable()
        {
            if (settings != null) settings.Changed += Refresh;
        }

        private void OnDisable()
        {
            if (settings != null) settings.Changed -= Refresh;
        }

        private void Start()
        {
            if (settings != null) Refresh(settings.Values);
        }

        public void TogglePanel()
        {
            if (panelRoot != null) SetOpen(!panelRoot.activeSelf);
        }

        public void SetOpen(bool open)
        {
            if (panelRoot != null) panelRoot.SetActive(open);
            if (follower != null) follower.Paused = open;
        }

        private void Refresh(AccessibilityValues v)
        {
            SetText(fontSizeValue, Mathf.RoundToInt(v.fontSize).ToString());
            SetText(fontFaceValue, settings.CurrentFontName);
            SetToggle(contrastValue, v.highContrast);
            SetText(positionValue, PositionName(v.position));
            SetToggle(speakerColorsValue, v.showSpeakerColors);
            SetText(maxLinesValue, v.maxLines.ToString());
            SetText(hideAfterValue, v.hideAfterSeconds <= 0f ? "Nunca" : $"{v.hideAfterSeconds:0} s");
            SetToggle(distantVoicesValue, v.ignoreDistantVoices);
        }

        private static string PositionName(SubtitlePosition p)
        {
            switch (p)
            {
                case SubtitlePosition.Top: return "Arriba";
                case SubtitlePosition.Middle: return "Centro";
                default: return "Abajo";
            }
        }

        private static void Bind(Button button, UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            label.text = text;
            label.color = SubtitlePalette.TextSecondary;
        }

        /// <summary>Los interruptores se muestran como "Sí" (en color) o "No" (gris).</summary>
        private static void SetToggle(TextMeshProUGUI label, bool on)
        {
            if (label == null) return;
            label.text = on ? "Sí" : "No";
            label.color = on ? SubtitlePalette.Accent : SubtitlePalette.TextSecondary;
        }
    }
}
