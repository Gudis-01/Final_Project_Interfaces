using System;
using TMPro;
using UnityEngine;

namespace Subtitulos.UI
{
    public enum SubtitlePosition { Bottom, Middle, Top }

    /// <summary>Valores que el usuario puede ajustar. Se guardan entre sesiones.</summary>
    [Serializable]
    public class AccessibilityValues
    {
        public float fontSize = 34f;
        public int fontIndex = 0;
        public bool highContrast = false;
        public SubtitlePosition position = SubtitlePosition.Bottom;
        public bool showSpeakerColors = true;

        /// <summary>Cuántas burbujas se ven a la vez (además de la que se está diciendo).</summary>
        public int maxLines = 2;

        /// <summary>Segundos sin hablar antes de que el texto se desvanezca. 0 = nunca.</summary>
        public float hideAfterSeconds = 8f;

        /// <summary>Ignorar voces bajas (gente lejos que no te habla a ti).</summary>
        public bool ignoreDistantVoices = false;
    }

    /// <summary>
    /// Fuente única de los ajustes de accesibilidad. La UI lee de aquí y escucha
    /// el evento Changed; los botones del panel de ajustes llaman a los métodos públicos.
    /// </summary>
    public class AccessibilitySettings : MonoBehaviour
    {
        private const string PrefsKey = "Subtitulos.Accessibility";

        [SerializeField] private float minFontSize = 22f;
        [SerializeField] private float maxFontSize = 64f;
        [SerializeField] private float fontStep = 4f;

        [Tooltip("Fuentes que el usuario puede elegir. La primera es la de default.")]
        [SerializeField] private TMP_FontAsset[] fonts = Array.Empty<TMP_FontAsset>();

        [Tooltip("Opciones para 'Líneas en pantalla'.")]
        [SerializeField] private int[] lineOptions = { 1, 2, 3 };

        [Tooltip("Opciones para 'Ocultar después de' (segundos). 0 = nunca.")]
        [SerializeField] private float[] hideOptions = { 5f, 8f, 15f, 0f };

        public event Action<AccessibilityValues> Changed;

        public AccessibilityValues Values { get; private set; } = new AccessibilityValues();

        /// <summary>Fuente seleccionada, o null para usar la que ya tenga el texto.</summary>
        public TMP_FontAsset CurrentFont =>
            fonts.Length == 0 ? null : fonts[Mathf.Clamp(Values.fontIndex, 0, fonts.Length - 1)];

        public string CurrentFontName
        {
            get
            {
                if (CurrentFont == null) return "Default";
                // "Inter-Regular SDF" → "Inter"
                string name = CurrentFont.name.Replace(" SDF", "");
                int dash = name.IndexOf('-');
                return dash > 0 ? name.Substring(0, dash) : name;
            }
        }

        private void Awake() => Load();

        private void Start() => Changed?.Invoke(Values); // Aplicar valores iniciales.

        // ---------- Acciones para botones ----------

        public void IncreaseFont() => SetFontSize(Values.fontSize + fontStep);
        public void DecreaseFont() => SetFontSize(Values.fontSize - fontStep);

        public void SetFontSize(float size)
        {
            Values.fontSize = Mathf.Clamp(size, minFontSize, maxFontSize);
            Commit();
        }

        public void NextFont()
        {
            if (fonts.Length == 0) return;
            Values.fontIndex = (Values.fontIndex + 1) % fonts.Length;
            Commit();
        }

        public void ToggleContrast()
        {
            Values.highContrast = !Values.highContrast;
            Commit();
        }

        public void NextPosition()
        {
            int count = Enum.GetValues(typeof(SubtitlePosition)).Length;
            Values.position = (SubtitlePosition)(((int)Values.position + 1) % count);
            Commit();
        }

        public void ToggleSpeakerColors()
        {
            Values.showSpeakerColors = !Values.showSpeakerColors;
            Commit();
        }

        public void NextMaxLines()
        {
            Values.maxLines = NextInCycle(lineOptions, Values.maxLines);
            Commit();
        }

        public void NextHideAfter()
        {
            Values.hideAfterSeconds = NextInCycle(hideOptions, Values.hideAfterSeconds);
            Commit();
        }

        public void ToggleDistantVoices()
        {
            Values.ignoreDistantVoices = !Values.ignoreDistantVoices;
            Commit();
        }

        public void ResetToDefaults()
        {
            Values = new AccessibilityValues();
            Commit();
        }

        /// <summary>Devuelve el valor que sigue al actual en la lista (y vuelve al inicio).</summary>
        private static T NextInCycle<T>(T[] options, T current)
        {
            if (options.Length == 0) return current;
            int index = Array.IndexOf(options, current);
            return options[(index + 1) % options.Length];
        }

        // ---------- Persistencia ----------

        private void Commit()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Values));
            PlayerPrefs.Save();
            Changed?.Invoke(Values);
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
