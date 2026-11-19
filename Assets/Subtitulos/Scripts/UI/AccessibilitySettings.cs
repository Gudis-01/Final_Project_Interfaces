using System;
using Subtitulos.Speech;
using TMPro;
using UnityEngine;

namespace Subtitulos.UI
{
    public enum SubtitlePosition { Bottom, Middle, Top }

    /// <summary>Valores que el usuario puede ajustar. Se guardan entre sesiones.</summary>
    [Serializable]
    public class AccessibilityValues
    {
        public RecognitionLanguage language = RecognitionLanguage.Auto;
        public float fontSize = 34f;
        public int fontIndex = 0;
        public bool highContrast = false;
        public SubtitlePosition position = SubtitlePosition.Bottom;
        public bool showSpeakerColors = true;

        /// <summary>Cuántas burbujas se ven a la vez (además de la que se está diciendo).</summary>
        public int maxLines = 2;

        /// <summary>Índice en las opciones de "Ocultar después de".</summary>
        public int hideIndex = 1;

        /// <summary>Ignorar voces bajas (gente lejos que no te habla a ti).</summary>
        public bool ignoreDistantVoices = false;

        /// <summary>Mostrar más tenues las palabras de las que el reconocedor no está seguro.</summary>
        public bool dimUncertainWords = true;
    }

    /// <summary>
    /// Fuente única de los ajustes. La UI lee de aquí y escucha el evento Changed;
    /// los controles del panel de ajustes llaman a los métodos Set*.
    /// También le pasa el idioma elegido al transcriptor.
    /// </summary>
    public class AccessibilitySettings : MonoBehaviour
    {
        private const string PrefsKey = "Subtitulos.Accessibility.v2";

        [SerializeField] private VoskTranscriber transcriber;

        [SerializeField] private float minFontSize = 24f;
        [SerializeField] private float maxFontSize = 60f;
        [SerializeField] private float fontStep = 4f;

        [Tooltip("Fuentes que el usuario puede elegir. La primera es la de default.")]
        [SerializeField] private TMP_FontAsset[] fonts = Array.Empty<TMP_FontAsset>();
        [Tooltip("Nombre que se muestra para cada fuente (mismo orden).")]
        [SerializeField] private string[] fontNames = Array.Empty<string>();

        [Tooltip("Opciones para 'Ocultar después de' (segundos). 0 = nunca.")]
        [SerializeField] private float[] hideOptions = { 5f, 8f, 15f, 0f };

        public event Action<AccessibilityValues> Changed;

        public AccessibilityValues Values { get; private set; } = new AccessibilityValues();

        public float MinFontSize => minFontSize;
        public float MaxFontSize => maxFontSize;
        public string[] FontNames => fontNames;
        public float[] HideOptions => hideOptions;

        /// <summary>Segundos antes de desvanecer el texto (0 = nunca).</summary>
        public float HideAfterSeconds =>
            hideOptions.Length == 0 ? 0f : hideOptions[Mathf.Clamp(Values.hideIndex, 0, hideOptions.Length - 1)];

        /// <summary>Fuente seleccionada, o null para usar la que ya tenga el texto.</summary>
        public TMP_FontAsset CurrentFont =>
            fonts.Length == 0 ? null : fonts[Mathf.Clamp(Values.fontIndex, 0, fonts.Length - 1)];

        private void Awake() => Load();

        private void Start()
        {
            ApplyToTranscriber();
            Changed?.Invoke(Values); // Aplicar valores iniciales.
        }

        // ---------- Acciones para los controles ----------

        public void SetLanguage(int index) => Apply(v => v.language = (RecognitionLanguage)Mathf.Clamp(index, 0, 2));
        public void IncreaseFont() => SetFontSize(Values.fontSize + fontStep);
        public void DecreaseFont() => SetFontSize(Values.fontSize - fontStep);
        public void SetFontSize(float size) => Apply(v => v.fontSize = Mathf.Clamp(size, minFontSize, maxFontSize));
        public void SetFontIndex(int index) => Apply(v => v.fontIndex = Mathf.Clamp(index, 0, Mathf.Max(0, fonts.Length - 1)));
        public void SetHighContrast(bool on) => Apply(v => v.highContrast = on);
        public void SetPosition(int index) => Apply(v => v.position = (SubtitlePosition)Mathf.Clamp(index, 0, 2));
        public void SetSpeakerColors(bool on) => Apply(v => v.showSpeakerColors = on);
        public void SetMaxLines(int lines) => Apply(v => v.maxLines = Mathf.Clamp(lines, 1, 3));
        public void SetHideIndex(int index) => Apply(v => v.hideIndex = Mathf.Clamp(index, 0, hideOptions.Length - 1));
        public void SetIgnoreDistantVoices(bool on) => Apply(v => v.ignoreDistantVoices = on);
        public void SetDimUncertainWords(bool on) => Apply(v => v.dimUncertainWords = on);

        public void ResetToDefaults()
        {
            Values = new AccessibilityValues();
            Commit();
        }

        private void Apply(Action<AccessibilityValues> change)
        {
            change(Values);
            Commit();
        }

        // ---------- Persistencia ----------

        private void Commit()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Values));
            PlayerPrefs.Save();
            ApplyToTranscriber();
            Changed?.Invoke(Values);
        }

        private void ApplyToTranscriber()
        {
            if (transcriber != null) transcriber.SetLanguage(Values.language);
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try { Values = JsonUtility.FromJson<AccessibilityValues>(json) ?? new AccessibilityValues(); }
            catch (ArgumentException) { Values = new AccessibilityValues(); }
            Values.maxLines = Mathf.Clamp(Values.maxLines, 1, 3);
        }
    }
}
