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
    /// Cada burbuja lleva una barrita y una etiqueta (P1, P2...) con el color de quien habla.
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
            public int SpeakerId = SpeakerTracker.Unknown;
            public string Text = "";
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

            float hideAfter = Values.hideAfterSeconds;
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
            bool tooQuiet = Values.ignoreDistantVoices && _peakDb < nearVoiceDb;
            _live.Text = tooQuiet ? "" : text;
            Render(_live, isLive: true);
        }

        private void OnFinal(TranscriptSegment segment)
        {
            bool tooQuiet = Values.ignoreDistantVoices && _peakDb < nearVoiceDb;
            _peakDb = -80f;
            _live.Text = "";
            Render(_live, isLive: true);
            if (tooQuiet) return;

            Bubble last = _finished.Count > 0 ? _finished[_finished.Count - 1] : null;
            bool sameTurn = last != null
                && last.SpeakerId == segment.SpeakerId
                && Time.time - last.LastUpdate < mergeSeconds;

            if (sameTurn)
            {
                last.Text = Truncate(last.Text + " " + segment.Text);
                last.LastUpdate = Time.time;
                Render(last, isLive: false);
            }
            else
            {
                Bubble bubble = CreateBubble();
                bubble.Text = Truncate(segment.Text);
                bubble.SpeakerId = segment.SpeakerId;
                bubble.LastUpdate = Time.time;
                _finished.Add(bubble);
                Render(bubble, isLive: false);
            }

            TrimToMaxLines();
            _live.Root.transform.SetAsLastSibling(); // lo que se está diciendo siempre hasta abajo
        }

        private void OnStatus(string status)
        {
            if (statusLabel == null) return;
            // Mientras todo funciona no se muestra nada: la barra de volumen ya indica que escucha.
            bool ok = transcriber != null && transcriber.IsReady && !status.StartsWith("Error");
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

        /// <summary>Si el texto es muy largo, conserva lo más reciente y corta en un espacio.</summary>
        private string Truncate(string text)
        {
            text = text.Trim();
            if (text.Length <= maxCharsPerBubble) return text;
            string tail = text.Substring(text.Length - maxCharsPerBubble);
            int space = tail.IndexOf(' ');
            if (space > 0 && space < 20) tail = tail.Substring(space + 1);
            return "… " + tail;
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
            ApplySprite(background, 2f);
            background.raycastTarget = false;

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 24, 14, 14);
            layout.spacing = 16;
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
            accentLayout.preferredWidth = accentLayout.minWidth = 6;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            textGo.transform.SetParent(root.transform, false);
            textGo.GetComponent<LayoutElement>().flexibleWidth = 1;
            var label = textGo.GetComponent<TextMeshProUGUI>();
            label.textWrappingMode = TextWrappingModes.Normal;
            label.richText = true;
            label.raycastTarget = false;
            label.lineSpacing = 4;

            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f; // aparece con fade
            group.blocksRaycasts = false;

            return new Bubble { Root = root, Group = group, Background = background, Accent = accent, Label = label };
        }

        private void ApplySprite(Image image, float roundness)
        {
            if (bubbleSprite == null) return;
            image.sprite = bubbleSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = roundness; // más alto = esquinas más chicas
        }

        private void Render(Bubble bubble, bool isLive)
        {
            AccessibilityValues v = Values;
            bool colors = v.showSpeakerColors && bubble.SpeakerId >= 0;

            bubble.Background.color = SubtitlePalette.BubbleBackground(bubble.SpeakerId, v.highContrast, v.showSpeakerColors);
            bubble.Accent.color = colors ? SubtitlePalette.Speaker(bubble.SpeakerId) : SubtitlePalette.Neutral;
            bubble.Accent.gameObject.SetActive(!isLive || v.highContrast);

            bubble.Label.fontSize = v.fontSize;
            bubble.Label.color = isLive && !v.highContrast ? SubtitlePalette.TextSecondary : SubtitlePalette.Text;
            if (settings != null && settings.CurrentFont != null) bubble.Label.font = settings.CurrentFont;

            string tag = "";
            if (colors)
            {
                string hex = SubtitlePalette.ToHex(SubtitlePalette.Speaker(bubble.SpeakerId));
                tag = $"<size=70%><color=#{hex}><b>{SubtitlePalette.SpeakerLabel(bubble.SpeakerId)}</b></color></size>   ";
            }

            // <noparse> evita que el texto reconocido se interprete como etiquetas de formato.
            bubble.Label.text = tag + "<noparse>" + bubble.Text + "</noparse>";

            bool visible = !string.IsNullOrEmpty(bubble.Text);
            if (isLive && !visible) bubble.Group.alpha = 0f;
            bubble.Root.SetActive(visible);
        }
    }
}
