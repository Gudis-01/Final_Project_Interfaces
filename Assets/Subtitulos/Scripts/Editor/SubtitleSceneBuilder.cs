using System.IO;
using Subtitulos.Audio;
using Subtitulos.Speech;
using Subtitulos.UI;
using Subtitulos.UI.Controls;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Subtitulos.EditorTools
{
    /// <summary>
    /// Menú "Subtítulos > Crear sistema en la escena": arma (o rehace) toda la interfaz
    /// y conecta las referencias entre componentes.
    ///
    ///   ┌────┬─────────────────────────────────────────────┐   ┌──────────────────────┐
    ///   │ 🎙 │        [ 🔔 aviso de sonido fuerte ]         │   │ Ajustes            ✕ │
    ///   │AUTO│                                             │   │ IDIOMA               │
    ///   │ ▮  │ ▌P1  Hola, ¿cómo estás?                     │   │ [Español|English|Auto]│
    ///   │ ▮  │ ▌P2 EN  I'm good, thanks                    │   │ TEXTO …              │
    ///   │ ⚙  │ ▌    (lo que se está diciendo…)             │   │ SUBTÍTULOS …         │
    ///   └────┴─────────────────────────────────────────────┘   └──────────────────────┘
    ///
    /// Sistema visual: vidrio oscuro + texto blanco (legible sobre el mundo real),
    /// tipografía Inter, íconos Material Icons Round, un solo color de acento.
    /// </summary>
    public static class SubtitleSceneBuilder
    {
        private const float CanvasScale = 0.001f; // 1 px = 1 mm
        private const string Root = "Assets/Subtitulos";
        private const string PillPath = Root + "/Art/Pill.png";

        private static Sprite _pill;
        private static TMP_FontAsset _font;       // UI
        private static TMP_FontAsset _iconFont;   // íconos
        private static TMP_FontAsset _readable;   // Atkinson Hyperlegible (opción para subtítulos)

        private static readonly Color Clear = new Color(0, 0, 0, 0);

        [MenuItem("Subtítulos/Crear sistema en la escena")]
        public static void Build()
        {
            RemoveExisting();
            LoadAssets();
            EnsureFolder("Assets", "StreamingAssets");

            // ---------- Lógica ----------
            var system = new GameObject("SubtitleSystem");
            var mic = system.AddComponent<MicrophoneCapture>();
            var transcriber = system.AddComponent<VoskTranscriber>();
            var detector = system.AddComponent<LoudSoundDetector>();
            var settings = system.AddComponent<AccessibilitySettings>();
            Set(detector, "microphone", mic);
            Set(detector, "transcriber", transcriber);
            Set(settings, "transcriber", transcriber);
            SetArray(settings, "fonts", new Object[] { _font, _readable });
            SetStrings(settings, "fontNames", "Inter", "Atkinson");

            // ---------- Canvas en el mundo ----------
            var canvasGo = new GameObject("SubtitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (Camera.main != null) canvas.worldCamera = Camera.main;
            AddXRRaycaster(canvasGo);

            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(820f, 300f);
            canvasRect.localScale = Vector3.one * CanvasScale;
            canvasRect.position = new Vector3(0f, 1.25f, 1.5f);

            var follower = canvasGo.AddComponent<HeadFollowPanel>();
            Set(follower, "settings", settings);

            RectTransform main = Box("Main", canvasRect);
            Stretch(main);
            HLayout(main, spacing: 56, expandHeight: true); // el riel (con la barra de volumen) queda separado de los subtítulos

            Button settingsButton = BuildRail(main, mic, transcriber, detector);
            SubtitleFeed feed = BuildCenter(main, transcriber, settings, mic, detector);
            BuildSettingsPanel(canvasRect, settings, feed, follower, settingsButton);

            Undo.RegisterCreatedObjectUndo(system, "Crear sistema de subtítulos");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Crear sistema de subtítulos");
            Selection.activeGameObject = canvasGo;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(system.scene);
            Debug.Log("[Subtítulos] Interfaz creada.");
        }

        // ---------- Riel izquierdo: estado, volumen y ajustes ----------

        private static Button BuildRail(RectTransform parent, MicrophoneCapture mic, VoskTranscriber transcriber,
                                         LoudSoundDetector detector)
        {
            RectTransform rail = Pill("Rail", parent, SubtitlePalette.Glass, 1f);
            Layout(rail, preferredWidth: 64, minWidth: 64);
            VLayout(rail, spacing: 10, padding: new RectOffset(0, 0, 14, 8), align: TextAnchor.UpperCenter, expandWidth: false);

            TextMeshProUGUI micIcon = Icon("MicIcon", rail, Icons.Mic, 30, SubtitlePalette.TextSecondary);
            Layout((RectTransform)micIcon.transform, preferredWidth: 64, preferredHeight: 32);
            TextMeshProUGUI lang = Label("Language", rail, "AUTO", 13, TextAlignmentOptions.Center, SubtitlePalette.TextSecondary);
            lang.fontStyle = FontStyles.Bold;
            lang.characterSpacing = 8;
            Layout((RectTransform)lang.transform, preferredWidth: 64, preferredHeight: 16);

            var indicator = rail.gameObject.AddComponent<ListeningIndicator>();
            Set(indicator, "transcriber", transcriber);
            Set(indicator, "microphone", mic);
            Set(indicator, "icon", micIcon);
            Set(indicator, "languageLabel", lang);

            // Aviso de sonido fuerte: solo un ícono pequeño que pulsa (no tapa los subtítulos).
            RectTransform alert = Pill("SoundAlert", rail, SubtitlePalette.Alert, 1f);
            Layout(alert, preferredWidth: 40, preferredHeight: 40);
            var alertGroup = alert.gameObject.AddComponent<CanvasGroup>();
            alertGroup.alpha = 0f;
            alertGroup.blocksRaycasts = false;
            TextMeshProUGUI bell = Icon("Icon", alert, Icons.Notifications, 24, SubtitlePalette.AlertText);
            Stretch((RectTransform)bell.transform);
            bell.alignment = TextAlignmentOptions.Center;
            var banner = alert.gameObject.AddComponent<SoundAlertBanner>();
            Set(banner, "detector", detector);
            Set(banner, "group", alertGroup);

            // Volumen: riel redondeado que recorta el relleno (Mask).
            RectTransform spacerTop = Box("Space", rail);
            Layout(spacerTop, flexibleHeight: 1);
            RectTransform track = Pill("VolumeTrack", rail, SubtitlePalette.Track, 1f);
            Layout(track, preferredWidth: 26, preferredHeight: 140);
            // El relleno ocupa todo el ancho de la barra con un margen mínimo (4 px), recortado en forma de píldora.
            RectTransform inner = Pill("FillMask", track, Color.white, 1f);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(4f, 4f);
            inner.offsetMax = new Vector2(-4f, -4f);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(inner, false);
            Stretch((RectTransform)fillGo.transform);
            Image fill = fillGo.GetComponent<Image>();
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0f;
            var volume = track.gameObject.AddComponent<VolumeBar>();
            Set(volume, "microphone", mic);
            Set(volume, "fill", fill);
            RectTransform spacerBottom = Box("Space", rail);
            Layout(spacerBottom, flexibleHeight: 1);

            Button button = IconButton(rail, "SettingsButton", Icons.Tune, 48, 26,
                                       new Color(1f, 1f, 1f, 0.10f), SubtitlePalette.Text);
            return button;
        }

        // ---------- Centro: aviso + subtítulos ----------

        private static SubtitleFeed BuildCenter(RectTransform parent, VoskTranscriber transcriber,
                                                AccessibilitySettings settings, MicrophoneCapture mic,
                                                LoudSoundDetector detector)
        {
            RectTransform column = Box("Center", parent);
            Layout(column, flexibleWidth: 1);
            VLayout(column, spacing: 10, align: TextAnchor.LowerCenter);

            // Zona de subtítulos: lo más nuevo siempre abajo.
            var area = new GameObject("Subtitles", typeof(RectTransform), typeof(RectMask2D));
            var areaRect = (RectTransform)area.transform;
            areaRect.SetParent(column, false);
            Layout(areaRect, flexibleHeight: 1);
            VLayout(areaRect, spacing: 10, align: TextAnchor.LowerCenter);

            TextMeshProUGUI status = Label("Status", column, "Cargando…", 18, TextAlignmentOptions.Left, SubtitlePalette.TextSecondary);
            Layout((RectTransform)status.transform, preferredHeight: 24);

            var feed = area.AddComponent<SubtitleFeed>();
            Set(feed, "transcriber", transcriber);
            Set(feed, "settings", settings);
            Set(feed, "microphone", mic);
            Set(feed, "container", areaRect);
            Set(feed, "bubbleSprite", _pill);
            Set(feed, "statusLabel", status);
            return feed;
        }

        // ---------- Panel de ajustes (lista agrupada) ----------

        private static void BuildSettingsPanel(RectTransform canvas, AccessibilitySettings settings, SubtitleFeed feed,
                                               HeadFollowPanel follower, Button toggle)
        {
            RectTransform panel = Pill("SettingsPanel", canvas, SubtitlePalette.GlassStrong, 2.6f);
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(500f, 0f);
            panel.anchoredPosition = new Vector2(24f, 0f);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            VLayout(panel, spacing: 6, padding: new RectOffset(20, 20, 16, 20));

            // Encabezado
            RectTransform header = Box("Header", panel);
            Layout(header, preferredHeight: 48);
            HLayout(header, spacing: 10, align: TextAnchor.MiddleLeft);
            Icon("Icon", header, Icons.Tune, 26, SubtitlePalette.Accent);
            TextMeshProUGUI title = Label("Title", header, "Ajustes", 26, TextAlignmentOptions.Left, SubtitlePalette.Text);
            title.fontStyle = FontStyles.Bold;
            Layout((RectTransform)title.transform, flexibleWidth: 1);
            Button close = IconButton(header, "Close", Icons.Close, 36, 20, SubtitlePalette.Row, SubtitlePalette.Text);

            // Idioma
            RectTransform languageGroup = Group(panel, "IDIOMA DE RECONOCIMIENTO");
            SegmentedControl language = Segmented(Row(languageGroup, null, true), 440, "Español", "English", "Auto");

            // Texto
            RectTransform textGroup = Group(panel, "TEXTO");
            RectTransform sizeRow = Row(textGroup, "Tamaño", false);
            Button smaller = IconButton(sizeRow, "Smaller", Icons.TextDecrease, 36, 20, SubtitlePalette.Row, SubtitlePalette.Text);
            TextMeshProUGUI sizeValue = Label("Value", sizeRow, "34", 19, TextAlignmentOptions.Center, SubtitlePalette.TextSecondary);
            Layout((RectTransform)sizeValue.transform, preferredWidth: 44);
            Button bigger = IconButton(sizeRow, "Bigger", Icons.TextIncrease, 36, 20, SubtitlePalette.Row, SubtitlePalette.Text);
            Separator(textGroup);
            SegmentedControl font = Segmented(Row(textGroup, "Fuente", false), 220, "Inter", "Atkinson");
            Separator(textGroup);
            SegmentedControl position = Segmented(Row(textGroup, "Posición", false), 260, "Abajo", "Centro", "Arriba");
            Separator(textGroup);
            ToggleSwitch contrast = Toggle(Row(textGroup, "Alto contraste", false));

            // Subtítulos
            RectTransform subsGroup = Group(panel, "SUBTÍTULOS");
            ToggleSwitch colors = Toggle(Row(subsGroup, "Color por persona", false));
            Separator(subsGroup);
            SegmentedControl lines = Segmented(Row(subsGroup, "Líneas visibles", false), 180, "1", "2", "3");
            Separator(subsGroup);
            SegmentedControl hide = Segmented(Row(subsGroup, "Ocultar tras", false), 260, "5 s", "8 s", "15 s", "Nunca");
            Separator(subsGroup);
            ToggleSwitch distant = Toggle(Row(subsGroup, "Ignorar voces lejanas", false));
            Separator(subsGroup);
            ToggleSwitch uncertain = Toggle(Row(subsGroup, "Atenuar palabras dudosas", false));

            // Acciones
            RectTransform actions = Box("Actions", panel);
            Layout(actions, preferredHeight: 50);
            HLayout(actions, spacing: 10, expandWidth: true, expandHeight: true);
            actions.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 8, 0);
            Button clear = ActionButton(actions, "Clear", Icons.Restart, "Nueva conversación");
            Button recenter = ActionButton(actions, "Recenter", Icons.Recenter, "Centrar panel");

            var component = canvas.gameObject.AddComponent<SettingsPanel>();
            Set(component, "settings", settings);
            Set(component, "feed", feed);
            Set(component, "follower", follower);
            Set(component, "panelRoot", panel.gameObject);
            Set(component, "toggleButton", toggle);
            Set(component, "closeButton", close);
            Set(component, "languageControl", language);
            Set(component, "fontSmallerButton", smaller);
            Set(component, "fontBiggerButton", bigger);
            Set(component, "fontSizeValue", sizeValue);
            Set(component, "fontControl", font);
            Set(component, "contrastToggle", contrast);
            Set(component, "positionControl", position);
            Set(component, "speakerColorsToggle", colors);
            Set(component, "linesControl", lines);
            Set(component, "hideControl", hide);
            Set(component, "distantVoicesToggle", distant);
            Set(component, "uncertainWordsToggle", uncertain);
            Set(component, "clearButton", clear);
            Set(component, "recenterButton", recenter);

            panel.gameObject.SetActive(false);
        }

        /// <summary>Título de sección + tarjeta redondeada que agrupa filas (estilo lista de ajustes).</summary>
        private static RectTransform Group(RectTransform panel, string caption)
        {
            TextMeshProUGUI label = Label("Caption", panel, caption, 13, TextAlignmentOptions.BottomLeft, SubtitlePalette.TextSecondary);
            label.characterSpacing = 6;
            label.fontStyle = FontStyles.Bold;
            label.margin = new Vector4(14, 0, 0, 4);
            Layout((RectTransform)label.transform, preferredHeight: 30);

            RectTransform card = Pill("Group", panel, SubtitlePalette.Row, 4.5f);
            VLayout(card, padding: new RectOffset(16, 12, 2, 2));
            return card;
        }

        /// <summary>Fila de 48 px: nombre a la izquierda, control a la derecha.</summary>
        private static RectTransform Row(RectTransform group, string name, bool fullWidth)
        {
            RectTransform row = Box(name ?? "Row", group);
            Layout(row, preferredHeight: 48);
            HLayout(row, spacing: 8, align: fullWidth ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            if (name != null)
            {
                TextMeshProUGUI label = Label("Name", row, name, 19, TextAlignmentOptions.Left, SubtitlePalette.Text);
                Layout((RectTransform)label.transform, flexibleWidth: 1);
            }
            return row;
        }

        private static void Separator(RectTransform group)
        {
            var go = new GameObject("Separator", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(group, false);
            var image = go.GetComponent<Image>();
            image.color = SubtitlePalette.Separator;
            image.raycastTarget = false;
            Layout((RectTransform)go.transform, preferredHeight: 1);
        }

        private static SegmentedControl Segmented(RectTransform row, float width, params string[] options)
        {
            RectTransform root = Pill("Segmented", row, new Color(1f, 1f, 1f, 0.08f), 1f);
            Layout(root, preferredWidth: width, preferredHeight: 36);
            HLayout(root, padding: new RectOffset(3, 3, 3, 3), expandWidth: true, expandHeight: true);

            RectTransform highlight = Pill("Highlight", root, new Color(0.95f, 0.96f, 0.98f, 1f), 1f);
            highlight.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            highlight.anchorMin = highlight.anchorMax = highlight.pivot = new Vector2(0.5f, 0.5f);
            highlight.sizeDelta = Vector2.zero; // se acomoda sola en Play

            var buttons = new Object[options.Length];
            var labels = new Object[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                var go = new GameObject(options[i], typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(root, false);
                go.GetComponent<Image>().color = Clear; // invisible pero recibe el rayo
                Button button = go.GetComponent<Button>();
                button.transition = Selectable.Transition.None;
                buttons[i] = button;
                TextMeshProUGUI label = Label("Text", go.transform, options[i], 16, TextAlignmentOptions.Center, SubtitlePalette.Text);
                Stretch((RectTransform)label.transform);
                labels[i] = label;
            }

            var control = root.gameObject.AddComponent<SegmentedControl>();
            Set(control, "highlight", highlight);
            SetArray(control, "segments", buttons);
            SetArray(control, "labels", labels);
            return control;
        }

        private static ToggleSwitch Toggle(RectTransform row)
        {
            RectTransform root = Pill("Toggle", row, new Color(1f, 1f, 1f, 0.18f), 1f);
            Layout(root, preferredWidth: 54, preferredHeight: 32);
            root.GetComponent<Image>().raycastTarget = true;

            RectTransform knob = Pill("Knob", root, Color.white, 1f);
            knob.anchorMin = knob.anchorMax = new Vector2(0f, 0.5f);
            knob.pivot = new Vector2(0f, 0.5f);
            knob.sizeDelta = new Vector2(24f, 24f);
            knob.anchoredPosition = new Vector2(4f, 0f);

            var toggle = root.gameObject.AddComponent<ToggleSwitch>();
            toggle.targetGraphic = root.GetComponent<Image>();
            toggle.transition = Selectable.Transition.None;
            Set(toggle, "knob", knob);
            Set(toggle, "track", root.GetComponent<Image>());
            SetColor(toggle, "onColor", SubtitlePalette.Accent);
            return toggle;
        }

        private static Button ActionButton(RectTransform parent, string name, string icon, string text)
        {
            Button button = MakeButton(parent, name, SubtitlePalette.Row, 4.5f, out RectTransform rect);
            HLayout(rect, spacing: 8, align: TextAnchor.MiddleCenter);
            Icon("Icon", rect, icon, 22, SubtitlePalette.Accent);
            Label("Text", rect, text, 17, TextAlignmentOptions.Left, SubtitlePalette.Accent);
            return button;
        }

        private static Button IconButton(Transform parent, string name, string icon, float size, float iconSize,
                                         Color background, Color iconColor)
        {
            Button button = MakeButton(parent, name, background, 1f, out RectTransform rect);
            Layout(rect, preferredWidth: size, preferredHeight: size, minWidth: size);
            TextMeshProUGUI glyph = Icon("Icon", rect, icon, iconSize, iconColor);
            Stretch((RectTransform)glyph.transform);
            return button;
        }

        // ---------- Utilidades de UI ----------

        private static RectTransform Box(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <param name="roundness">1 = cápsula completa; más alto = esquinas más chicas.</param>
        private static RectTransform Pill(string name, Transform parent, Color color, float roundness)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.sprite = _pill;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = roundness;
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, string text, float size,
                                             TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static TextMeshProUGUI Icon(string name, Transform parent, string glyph, float size, Color color)
        {
            TextMeshProUGUI icon = Label(name, parent, glyph, size, TextAlignmentOptions.Center, color);
            if (_iconFont != null) icon.font = _iconFont;
            Layout((RectTransform)icon.transform, preferredWidth: size, preferredHeight: size);
            return icon;
        }

        private static Button MakeButton(Transform parent, string name, Color color, float roundness, out RectTransform rect)
        {
            rect = Pill(name, parent, color, roundness);
            rect.GetComponent<Image>().raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            // Al apuntar con el rayo, el fondo se vuelve más opaco (retroalimentación sutil).
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 2.2f);
            colors.pressedColor = new Color(1f, 1f, 1f, 3.2f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
        }

        private static void HLayout(RectTransform rect, float spacing = 0, RectOffset padding = null,
                                    TextAnchor align = TextAnchor.MiddleLeft, bool expandWidth = false, bool expandHeight = false)
        {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = align;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = expandHeight;
        }

        private static void VLayout(RectTransform rect, float spacing = 0, RectOffset padding = null,
                                    TextAnchor align = TextAnchor.UpperLeft, bool expandWidth = true)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = align;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = false;
        }

        private static void Layout(RectTransform rect, float preferredHeight = -1, float preferredWidth = -1,
                                   float flexibleHeight = -1, float flexibleWidth = -1, float minWidth = -1)
        {
            // Ojo: no usar "??" con componentes de Unity (su null no es un null real de C#).
            var element = rect.GetComponent<LayoutElement>();
            if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            element.preferredWidth = preferredWidth;
            element.flexibleHeight = flexibleHeight;
            element.flexibleWidth = flexibleWidth;
            element.minWidth = minWidth;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, 0);
            rect.offsetMax = new Vector2(-padding, 0);
        }

        // ---------- Assets ----------

        private static void LoadAssets()
        {
            _pill = GetOrCreatePillSprite();
            // Inter (incluida en el template de VR) como font asset dinámico propio: así tiene
            // todos los caracteres (á, ñ, ¿, ¡, …) aunque el asset original sea estático.
            _font = FontAssetFactory.GetOrCreate("Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf", 90, 1024,
                                                 Root + "/Fonts/Inter-Regular Dynamic SDF.asset");
            if (_font == null) _font = FontAssetFactory.Find("Inter-Regular SDF");
            if (_font == null) _font = TMP_Settings.defaultFontAsset;
            _iconFont = FontAssetFactory.GetOrCreate(Root + "/Fonts/MaterialIconsRound-Regular.otf", 90, 512);
            _readable = FontAssetFactory.GetOrCreate(Root + "/Fonts/AtkinsonHyperlegible-Regular.ttf");
            if (_readable == null) _readable = _font;
            if (_iconFont == null) Debug.LogWarning("[Subtítulos] Sin fuente de íconos: se verán cuadros vacíos.");
        }

        /// <summary>
        /// Crea (una sola vez) un círculo blanco suavizado de 128 px y lo importa como sprite
        /// 9-slice. Estirado se convierte en cápsula o rectángulo redondeado.
        /// </summary>
        private static Sprite GetOrCreatePillSprite()
        {
            if (!File.Exists(PillPath))
            {
                EnsureFolder(Root, "Art");
                const int size = 128;
                const float radius = size / 2f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius, dy = y + 0.5f - radius;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                File.WriteAllBytes(PillPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(PillPath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(PillPath);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder.x < 1f)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = new Vector4(63, 63, 63, 63);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(PillPath);
        }

        // ---------- Utilidades del editor ----------

        private static void RemoveExisting()
        {
            foreach (string name in new[] { "SubtitleSystem", "SubtitleCanvas" })
            {
                GameObject existing = GameObject.Find(name);
                while (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing);
                    existing = GameObject.Find(name);
                }
            }
        }

        /// <summary>Asigna un campo [SerializeField] privado, como si se arrastrara en el Inspector.</summary>
        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Subtítulos] No existe el campo '{field}' en {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(Object target, string field, Color value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStrings(Object target, string field, params string[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Raycaster de XR Interaction Toolkit para que los rayos de controles/manos presionen
        /// los botones. Se busca por nombre para no romper la compilación sin el paquete.
        /// </summary>
        private static void AddXRRaycaster(GameObject canvas)
        {
            System.Type type = System.Type.GetType(
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
            if (type != null) canvas.AddComponent(type);
            else Debug.LogWarning("[Subtítulos] No se encontró XR Interaction Toolkit; los botones solo funcionarán con mouse.");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
