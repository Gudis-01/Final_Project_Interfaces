using System.Collections.Generic;
using Subtitulos.Audio;
using Subtitulos.Speech;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Subtitulos.UI
{
    /// <summary>
    /// Muestra los subtítulos como burbujas apiladas, pensado para NO saturar:
    ///
    ///  • Pocas burbujas: solo las últimas N frases (ajustable: 1, 2 o 3) + la que se está diciendo.
    ///  • Una burbuja por turno: si la misma persona sigue hablando, su texto se agrega a su
    ///    burbuja en lugar de crear otra.
    ///  • Se desvanecen solas tras unos segundos de silencio (ajustable o desactivable).
    ///  • Opcional: ignorar voces lejanas (bajas), para no subtitular conversaciones ajenas.
    ///
    /// Cada burbuja lleva una barrita y una etiqueta (P1, P2...) con el color de quien habla,
    /// el idioma detectado (en modo automático) y las palabras dudosas se ven más tenues
    /// (el sistema muestra cuándo no está seguro, en vez de aparentar certeza).
    /// </summary>
    public class SubtitleFeed : MonoBehaviour
    {
        [SerializeField] private VoskTranscriber transcriber;
        [SerializeField] private AccessibilitySettings settings;
        [Tooltip("Para el filtro de voces lejanas.")]
        [SerializeField] private MicrophoneCapture microphone;

        [Tooltip("Contenedor con VerticalLayoutGroup donde se apilan las burbujas.")]
        [SerializeField] private RectTransform container;

        [Tooltip("Sprite redondeado para el fondo de las burbujas (9-slice).")]
        [SerializeField] private Sprite bubbleSprite;

        [Tooltip("Texto pequeño para estados (cargando, errores). Se oculta mientras todo va bien.")]
        [SerializeField] private TextMeshProUGUI statusLabel;

        [Header("Comportamiento")]
        [Tooltip("Si la misma persona vuelve a hablar antes de estos segundos, se une a su burbuja.")]
        [SerializeField] private float mergeSeconds = 5f;
        [Tooltip("Máximo de caracteres por burbuja; lo más viejo se recorta con '…'.")]
        [SerializeField] private int maxCharsPerBubble = 150;
        [Tooltip("Palabras con confianza menor a esto se muestran más tenues (si está activado).")]
        [SerializeField, Range(0f, 1f)] private float uncertainBelow = 0.6f;
        [Tooltip("Volumen mínimo (dBFS) para aceptar una frase cuando 'Ignorar voces lejanas' está activo.")]
        [SerializeField, Range(-60f, -10f)] private float nearVoiceDb = -38f;
        [SerializeField] private float fadeSpeed = 6f;

        private class Bubble
        {
            public GameObject Root;
            public CanvasGroup Group;
            public Image Background;
            public Image Accent;
            public TextMeshProUGUI Label;
            public LayoutElement TextLayout;
            public int SpeakerId = SpeakerTracker.Unknown;
            public string Language = "";
            public string Text = "";                                   // texto provisional (burbuja en vivo)
            public string Partial = "";                                // texto provisional agregado al final de una burbuja existente
            public readonly List<RecognizedWord> Words = new List<RecognizedWord>(); // frases terminadas
            public float LastUpdate;
            public bool Removing;
        }

        private readonly List<Bubble> _finished = new List<Bubble>();
        private readonly List<Bubble> _removing = new List<Bubble>();
        private Bubble _live;
        private float _peakDb = -80f;

        private AccessibilityValues Values => settings != null ? settings.Values : DefaultValues;
        private static readonly AccessibilityValues DefaultValues = new AccessibilityValues();

        // ---------- Ciclo de vida ----------

        private void OnEnable()
        {
            if (transcriber != null)
            {
                transcriber.PartialResult += OnPartial;
                transcriber.FinalResult += OnFinal;
                transcriber.StatusChanged += OnStatus;
            }
            if (settings != null) settings.Changed += OnSettingsChanged;
        }

        private void OnDisable()
        {
            if (transcriber != null)
            {
                transcriber.PartialResult -= OnPartial;
                transcriber.FinalResult -= OnFinal;
                transcriber.StatusChanged -= OnStatus;
            }
            if (settings != null) settings.Changed -= OnSettingsChanged;
        }

        private void Start()
        {
            _live = CreateBubble();
            Render(_live, isLive: true);
            OnStatus(transcriber != null ? transcriber.Status : "Falta asignar el transcriptor");
        }

        private void Update()
        {
            if (microphone != null) _peakDb = Mathf.Max(_peakDb, microphone.LevelDb);

            float hideAfter = settings != null ? settings.HideAfterSeconds : 8f;
            for (int i = _finished.Count - 1; i >= 0; i--)
            {
                Bubble b = _finished[i];
                if (hideAfter > 0f && Time.time - b.LastUpdate > hideAfter) Remove(b);
            }

            float step = 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);
            foreach (Bubble b in _finished) b.Group.alpha = Mathf.Lerp(b.Group.alpha, 1f, step);
            if (_live != null) _live.Group.alpha = Mathf.Lerp(_live.Group.alpha, _live.Root.activeSelf ? 1f : 0f, step);

            for (int i = _removing.Count - 1; i >= 0; i--)
            {
                Bubble b = _removing[i];
                b.Group.alpha = Mathf.Lerp(b.Group.alpha, 0f, step);
                if (b.Group.alpha < 0.02f)
                {
                    Destroy(b.Root);
                    _removing.RemoveAt(i);
                }
            }
        }

        // ---------- Eventos del transcriptor ----------

        private void OnPartial(string text)
        {
            if (_live == null) return;
            bool tooQuiet = false; // las voces lejanas ya se filtran en MicrophoneCapture
            if (tooQuiet) text = "";

            // Si la misma persona sigue hablando, lo provisional se escribe al final de su burbuja
            // (en vez de abrir otra caja y luego "saltar" a la anterior).
            Bubble host = _finished.Count > 0 ? _finished[_finished.Count - 1] : null;
            if (host != null && (Time.time - host.LastUpdate >= mergeSeconds || host.Removing)) host = null;

            foreach (Bubble b in _finished)
            {
                string wanted = b == host ? text : "";
                if (b.Partial == wanted) continue;
                b.Partial = wanted;
                Render(b, isLive: false);
            }
            if (host != null && text.Length > 0) host.LastUpdate = Time.time; // mientras hablan no se oculta

            _live.Text = host != null ? "" : text;
            Render(_live, isLive: true);
        }

        private void OnFinal(TranscriptSegment segment)
        {
            if (_live == null || segment.Words.Count == 0) return;
            bool tooQuiet = false; // las voces lejanas ya se filtran en MicrophoneCapture
            _peakDb = -80f;
            foreach (Bubble b in _finished) b.Partial = "";
            _live.Text = "";
            Render(_live, isLive: true);
            if (tooQuiet) return;

            Bubble last = _finished.Count > 0 ? _finished[_finished.Count - 1] : null;
            bool sameTurn = last != null
                && last.SpeakerId == segment.SpeakerId
                && last.Language == segment.Language
                && Time.time - last.LastUpdate < mergeSeconds;

            Bubble target = sameTurn ? last : CreateBubble();
            if (!sameTurn)
            {
                target.SpeakerId = segment.SpeakerId;
                target.Language = segment.Language;
                _finished.Add(target);
            }
            else if (target.Words.Count > 0)
            {
                // Al continuar la frase, solo la primera palabra de toda la burbuja lleva mayúscula.
                RecognizedWord first = segment.Words[0];
                var rest = new List<RecognizedWord>(segment.Words);
                rest[0] = new RecognizedWord(Decapitalize(first.Text, segment.Language), first.Confidence);
                target.Words.AddRange(rest);
                Truncate(target.Words);
            }
            if (!sameTurn) { target.Words.AddRange(segment.Words); Truncate(target.Words); }

            target.LastUpdate = Time.time;
            Render(target, isLive: false);

            TrimToMaxLines();
            _live.Root.transform.SetAsLastSibling(); // lo que se está diciendo siempre hasta abajo
        }

        private void OnStatus(string status)
        {
            if (statusLabel == null) return;
            // Mientras todo funciona no se muestra nada: la barra de volumen ya indica que escucha.
            bool ok = transcriber != null && transcriber.IsReady && !transcriber.HasError;
            statusLabel.text = ok ? "" : status;
            statusLabel.gameObject.SetActive(!ok && !string.IsNullOrEmpty(status));
        }

        private void OnSettingsChanged(AccessibilityValues _)
        {
            TrimToMaxLines();
            foreach (Bubble b in _finished) Render(b, isLive: false);
            if (_live != null) Render(_live, isLive: true);
        }

        /// <summary>Borra todo lo mostrado y olvida a los hablantes (botón "Nueva conversación").</summary>
        public void Clear()
        {
            for (int i = _finished.Count - 1; i >= 0; i--) Remove(_finished[i]);
            if (_live != null) { _live.Text = ""; Render(_live, isLive: true); }
            // (los hablantes se olvidan en el transcriptor)
            transcriber?.ResetConversation();
        }

        // ---------- Reglas para no saturar ----------

        private void TrimToMaxLines()
        {
            int max = Mathf.Max(1, Values.maxLines);
            while (_finished.Count > max) Remove(_finished[0]);
        }

        private void Remove(Bubble bubble)
        {
            _finished.Remove(bubble);
            if (bubble.Removing) return;
            bubble.Removing = true;
            _removing.Add(bubble);
        }

        /// <summary>Si la burbuja tiene demasiado texto, quita las palabras más viejas.</summary>
        private void Truncate(List<RecognizedWord> words)
        {
            int chars = 0;
            foreach (RecognizedWord w in words) chars += w.Text.Length + 1;
            while (chars > maxCharsPerBubble && words.Count > 1)
            {
                chars -= words[0].Text.Length + 1;
                words.RemoveAt(0);
            }
        }

        private static string Decapitalize(string word, string language)
        {
            if (string.IsNullOrEmpty(word) || word == "I" || word.StartsWith("I'")) return word;
            return char.ToLower(word[0]) + word.Substring(1);
        }

        /// <summary>Arma el texto con las palabras dudosas más tenues (opacidad baja).</summary>
        private string BuildText(Bubble bubble, bool isLive, AccessibilityValues v)
        {
            if (isLive) return PartialMarkup(bubble.Text, false);

            var sb = new System.Text.StringBuilder();
            bool trimmed = false;
            for (int i = 0; i < bubble.Words.Count; i++)
            {
                RecognizedWord w = bubble.Words[i];
                if (i > 0) sb.Append(' ');
                bool dim = v.dimUncertainWords && !v.highContrast && w.Confidence < uncertainBelow;
                if (dim) sb.Append("<alpha=#80>");
                sb.Append("<noparse>").Append(w.Text).Append("</noparse>");
                if (dim) sb.Append("<alpha=#FF>");
                trimmed |= i == 0 && w.Text.Length > 0 && char.IsLower(w.Text[0]);
            }
            if (bubble.Partial.Length > 0) sb.Append(' ').Append(PartialMarkup(bubble.Partial, true));
            return (trimmed ? "… " : "") + sb;
        }

        /// <summary>
        /// Texto provisional: lo que Vosk ya no suele cambiar (todo menos las últimas 2 palabras)
        /// se ve normal; las últimas palabras, que todavía pueden corregirse, se ven tenues.
        /// </summary>
        private static string PartialMarkup(string text, bool normalFirst)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string[] words = text.Split(' ');
            int stable = Mathf.Max(0, words.Length - 2);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < words.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                bool tail = i >= stable;
                if (tail) sb.Append("<alpha=#99>");
                sb.Append("<noparse>").Append(words[i]).Append("</noparse>");
                if (tail) sb.Append("<alpha=#FF>");
            }
            return sb.ToString();
        }

        // ---------- Construcción y dibujo ----------

        private Bubble CreateBubble()
        {
            var root = new GameObject("Subtitle", typeof(RectTransform), typeof(Image), typeof(CanvasGroup),
                                      typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            root.transform.SetParent(container, false);
            // Sin esto la burbuja se estira a toda la altura disponible (el layout la vuelve "flexible").
            root.GetComponent<LayoutElement>().flexibleHeight = 0f;

            var background = root.GetComponent<Image>();
            ApplySprite(background, 3f);
            background.raycastTarget = false;

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 18, 8, 8);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // Barrita de color a la izquierda.
            var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            accentGo.transform.SetParent(root.transform, false);
            var accent = accentGo.GetComponent<Image>();
            ApplySprite(accent, 8f);
            accent.raycastTarget = false;
            var accentLayout = accentGo.GetComponent<LayoutElement>();
            accentLayout.preferredWidth = accentLayout.minWidth = 4;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textGo.transform.SetParent(root.transform, false);
            var textLayout = textGo.GetComponent<LayoutElement>();
            textLayout.flexibleWidth = 1;
            var label = textGo.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.richText = true;
            label.raycastTarget = false;
            label.lineSpacing = 0;

            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f; // aparece con fade
            group.blocksRaycasts = false;

            return new Bubble { Root = root, Group = group, Background = background, Accent = accent, Label = label, TextLayout = textLayout };
        }

        private void ApplySprite(Image image, float roundness)
        {
            if (bubbleSprite == null) return;
            image.sprite = bubbleSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = roundness; // más alto = esquinas más chicas
        }

        /// <summary>
        /// Calcula la altura del texto con el ancho real disponible. (TextMeshPro dentro de
        /// layouts a veces reporta la altura con un ancho viejo y la burbuja queda demasiado alta.)
        /// </summary>
        private void FitHeight(Bubble bubble)
        {
            const float horizontalChrome = 14 + 18 + 12 + 4; // padding + espacio + barrita de color
            float width = container.rect.width - horizontalChrome;
            if (width < 20f) return;
            Vector2 size = bubble.Label.GetPreferredValues(bubble.Label.text, width, 0f);
            bubble.TextLayout.preferredHeight = Mathf.Ceil(size.y);
        }

        private float _lastWidth;

        private void LateUpdate()
        {
            // Si cambia el ancho del panel (primer frame, cambio de tamaño), recalcular alturas.
            if (Mathf.Approximately(container.rect.width, _lastWidth)) return;
            _lastWidth = container.rect.width;
            foreach (Bubble b in _finished) FitHeight(b);
            if (_live != null) FitHeight(_live);
        }

        private void Render(Bubble bubble, bool isLive)
        {
            AccessibilityValues v = Values;
            bool colors = v.showSpeakerColors && bubble.SpeakerId >= 0;

            bubble.Background.color = SubtitlePalette.BubbleBackground(bubble.SpeakerId, v.highContrast, v.showSpeakerColors);
            bubble.Accent.color = colors ? SubtitlePalette.Speaker(bubble.SpeakerId) : SubtitlePalette.Neutral;
            bubble.Accent.gameObject.SetActive(!isLive || v.highContrast);

            bubble.Label.fontSize = v.fontSize;
            bubble.Label.color = SubtitlePalette.Text;
            if (settings != null && settings.CurrentFont != null) bubble.Label.font = settings.CurrentFont;

            // Etiqueta pequeña: persona (en su color) y, en modo automático, el idioma detectado.
            string tag = "";
            if (colors)
            {
                string hex = SubtitlePalette.ToHex(SubtitlePalette.Speaker(bubble.SpeakerId));
                tag = $"<color=#{hex}><b>{SubtitlePalette.SpeakerLabel(bubble.SpeakerId)}</b></color>";
            }
            bool showLanguage = !isLive && v.language == RecognitionLanguage.Auto && !string.IsNullOrEmpty(bubble.Language);
            if (showLanguage)
                tag += (tag.Length > 0 ? "  " : "") + $"<color=#FFFFFF80>{bubble.Language.ToUpperInvariant()}</color>";
            if (tag.Length > 0) tag = $"<size=62%><cspace=0.05em>{tag}</cspace></size>   ";

            // <noparse> evita que el texto reconocido se interprete como etiquetas de formato.
            bubble.Label.text = tag + BuildText(bubble, isLive, v);

            FitHeight(bubble);

            bool visible = isLive ? !string.IsNullOrEmpty(bubble.Text) : bubble.Words.Count > 0;
            if (isLive && !visible) bubble.Group.alpha = 0f;
            bubble.Root.SetActive(visible);
        }
    }
}
