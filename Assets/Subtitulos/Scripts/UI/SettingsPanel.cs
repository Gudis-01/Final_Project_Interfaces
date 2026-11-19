using Subtitulos.UI.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Subtitulos.UI
{
    /// <summary>
    /// Panel de ajustes. Cada opción usa el control que mejor le queda:
    ///   • Interruptor para sí/no (contraste, colores, voces lejanas, palabras dudosas).
    ///   • Selector segmentado para elegir entre pocas opciones visibles (idioma, fuente,
    ///     posición, líneas, tiempo).
    ///   • Botones A− / A+ para el tamaño del texto.
    /// Todo cambio se aplica al instante en los subtítulos (retroalimentación inmediata).
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private AccessibilitySettings settings;
        [SerializeField] private SubtitleFeed feed;
        [SerializeField] private HeadFollowPanel follower;

        [Tooltip("Objeto que se muestra/oculta con el botón de ajustes.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button closeButton;

        [Header("Idioma")]
        [SerializeField] private SegmentedControl languageControl;

        [Header("Texto")]
        [SerializeField] private Button fontSmallerButton;
        [SerializeField] private Button fontBiggerButton;
        [SerializeField] private TextMeshProUGUI fontSizeValue;
        [SerializeField] private SegmentedControl fontControl;
        [SerializeField] private ToggleSwitch contrastToggle;
        [SerializeField] private SegmentedControl positionControl;

        [Header("Subtítulos")]
        [SerializeField] private ToggleSwitch speakerColorsToggle;
        [SerializeField] private SegmentedControl linesControl;
        [SerializeField] private SegmentedControl hideControl;
        [SerializeField] private ToggleSwitch distantVoicesToggle;
        [SerializeField] private ToggleSwitch uncertainWordsToggle;

        [Header("Acciones")]
        [SerializeField] private Button clearButton;
        [SerializeField] private Button recenterButton;

        private bool _initialized;

        private void Awake()
        {
            Bind(toggleButton, TogglePanel);
            Bind(closeButton, () => SetOpen(false));
            Bind(fontSmallerButton, () => settings.DecreaseFont());
            Bind(fontBiggerButton, () => settings.IncreaseFont());
            Bind(clearButton, () => { if (feed != null) feed.Clear(); });
            Bind(recenterButton, () => { if (follower != null) follower.Recenter(); });

            if (languageControl != null) languageControl.Changed += i => settings.SetLanguage(i);
            if (fontControl != null) fontControl.Changed += i => settings.SetFontIndex(i);
            if (positionControl != null) positionControl.Changed += i => settings.SetPosition(i);
            if (linesControl != null) linesControl.Changed += i => settings.SetMaxLines(i + 1);
            if (hideControl != null) hideControl.Changed += i => settings.SetHideIndex(i);

            if (contrastToggle != null) contrastToggle.Changed += on => settings.SetHighContrast(on);
            if (speakerColorsToggle != null) speakerColorsToggle.Changed += on => settings.SetSpeakerColors(on);
            if (distantVoicesToggle != null) distantVoicesToggle.Changed += on => settings.SetIgnoreDistantVoices(on);
            if (uncertainWordsToggle != null) uncertainWordsToggle.Changed += on => settings.SetDimUncertainWords(on);

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

        /// <summary>Pone cada control en el valor actual (sin volver a disparar eventos).</summary>
        private void Refresh(AccessibilityValues v)
        {
            bool instant = !_initialized;
            _initialized = true;

            languageControl?.Select((int)v.language);
            fontControl?.Select(v.fontIndex);
            positionControl?.Select((int)v.position);
            linesControl?.Select(v.maxLines - 1);
            hideControl?.Select(v.hideIndex);

            contrastToggle?.SetValue(v.highContrast, instant: instant);
            speakerColorsToggle?.SetValue(v.showSpeakerColors, instant: instant);
            distantVoicesToggle?.SetValue(v.ignoreDistantVoices, instant: instant);
            uncertainWordsToggle?.SetValue(v.dimUncertainWords, instant: instant);

            if (fontSizeValue != null) fontSizeValue.text = Mathf.RoundToInt(v.fontSize).ToString();
            if (fontSmallerButton != null) fontSmallerButton.interactable = v.fontSize > settings.MinFontSize;
            if (fontBiggerButton != null) fontBiggerButton.interactable = v.fontSize < settings.MaxFontSize;
        }

        private static void Bind(Button button, UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }
    }
}
