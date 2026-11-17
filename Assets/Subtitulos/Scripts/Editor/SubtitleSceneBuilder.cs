using System.IO;
using System.Linq;
using Subtitulos.Audio;
using Subtitulos.Speech;
using Subtitulos.UI;
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
    /// Diseño (basado en el boceto del equipo):
    ///   ┌────┬──────────────────────────────────────────┐
    ///   │ ▮  │        [ aviso de sonido fuerte ]        │
    ///   │ ▮  │                                          │
    ///   │ ▮  │ ▌P1  Hola, ¿cómo estás?                  │
    ///   │Bajo│ ▌P2  Bien, ¿y tú?                        │
    ///   │ Aa │ ▌    (lo que se está diciendo…)          │
    ///   └────┴──────────────────────────────────────────┘
    ///   volumen     subtítulos por persona (máx. 1-3)        + panel de ajustes a la derecha
    /// </summary>
    public static class SubtitleSceneBuilder
    {
        private const float CanvasScale = 0.001f; // 1 px = 1 mm
        private const string ArtFolder = "Assets/Subtitulos/Art";
        private const string PillPath = ArtFolder + "/Pill.png";

        private static Sprite _pill;
        private static TMP_FontAsset _font;

        [MenuItem("Subtítulos/Crear sistema en la escena %&s")] // Ctrl+Alt+S
        public static void Build()
        {
            RemoveExisting();

            _pill = GetOrCreatePillSprite();
            _font = FindFonts().FirstOrDefault();
            EnsureFolder("Assets", "StreamingAssets");

            // ---------- Lógica (audio + reconocimiento + ajustes) ----------
            var system = new GameObject("SubtitleSystem");
            var mic = system.AddComponent<MicrophoneCapture>();
            var transcriber = system.AddComponent<VoskTranscriber>();
            var detector = system.AddComponent<LoudSoundDetector>();
            var settings = system.AddComponent<AccessibilitySettings>();
            Set(detector, "microphone", mic);
            Set(detector, "transcriber", transcriber);
            SetArray(settings, "fonts", FindFonts().Cast<Object>().ToArray());

            // ---------- Canvas en el mundo ----------
            var canvasGo = new GameObject("SubtitleCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            if (Camera.main != null) canvas.worldCamera = Camera.main;
            AddXRRaycaster(canvasGo);

            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(880f, 400f);
            canvasRect.localScale = Vector3.one * CanvasScale;
            canvasRect.position = new Vector3(0f, 1.25f, 1.5f);

            var follower = canvasGo.AddComponent<HeadFollowPanel>();
            Set(follower, "settings", settings);

            RectTransform main = Box("Main", canvasRect);
            Stretch(main);
            var mainLayout = main.gameObject.AddComponent<HorizontalLayoutGroup>();
            mainLayout.spacing = 18;
            mainLayout.childControlWidth = mainLayout.childControlHeight = true;
            mainLayout.childForceExpandWidth = false;
            mainLayout.childForceExpandHeight = true;

            Button settingsButton = BuildLeftColumn(main, mic);
            SubtitleFeed feed = BuildCenterColumn(main, transcriber, settings, mic, detector);
            BuildSettingsPanel(canvasRect, settings, feed, follower, settingsButton);

            Undo.RegisterCreatedObjectUndo(system, "Crear sistema de subtítulos");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Crear sistema de subtítulos");
            Selection.activeGameObject = canvasGo;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(system.scene);

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                Debug.LogWarning("[Subtítulos] No hay EventSystem en la escena: los botones no responderán.");

            Debug.Log("[Subtítulos] Interfaz creada.");
        }

        // ---------- Columna izquierda: volumen + botón de ajustes ----------

        private static Button BuildLeftColumn(RectTransform parent, MicrophoneCapture mic)
        {
            RectTransform column = Box("LeftColumn", parent);
            Layout(column, preferredWidth: 64, minWidth: 64);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.spacing = 10;
            layout.padding = new RectOffset(0, 0, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Riel redondeado que recorta el relleno (Mask) para que también se vea redondeado.
            RectTransform track = Pill("VolumeTrack", column, SubtitlePalette.Track, 1f);
            Layout(track, preferredWidth: 14, preferredHeight: 190);
            track.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(track, false);
            Stretch((RectTransform)fillGo.transform);
            Image fill = fillGo.GetComponent<Image>();
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0f;

            TextMeshProUGUI volumeLabel = Text("VolumeLabel", column, "Bajo", 17, TextAlignmentOptions.Center,
                                               SubtitlePalette.TextSecondary);
            Layout((RectTransform)volumeLabel.transform, preferredWidth: 64, preferredHeight: 24);

            var volume = track.gameObject.AddComponent<VolumeBar>();
            Set(volume, "microphone", mic);
            Set(volume, "fill", fill);
            Set(volume, "label", volumeLabel);

            // Botón redondo "Aa" (ícono común de ajustes de texto).
            Button button = MakeButton(column, "SettingsButton", SubtitlePalette.Glass, 1f, out RectTransform rect);
            Layout(rect, preferredWidth: 56, preferredHeight: 56);
            TextMeshProUGUI aa = Text("Icon", rect, "Aa", 24, TextAlignmentOptions.Center, SubtitlePalette.Text);
            aa.fontStyle = FontStyles.Bold;
            Stretch((RectTransform)aa.transform);
            return button;
        }

        // ---------- Columna central: aviso + subtítulos ----------

        private static SubtitleFeed BuildCenterColumn(RectTransform parent, VoskTranscriber transcriber,
                                                     AccessibilitySettings settings, MicrophoneCapture mic,
                                                     LoudSoundDetector detector)
        {
            RectTransform column = Box("CenterColumn", parent);
            Layout(column, flexibleWidth: 1);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Aviso de sonido fuerte (invisible hasta que haya un evento).
            RectTransform alert = Pill("SoundAlert", column, SubtitlePalette.Alert, 1f);
            Layout(alert, preferredHeight: 52);
            var group = alert.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            TextMeshProUGUI alertText = Text("Text", alert, "Sonido fuerte", 22, TextAlignmentOptions.Center,
                                             SubtitlePalette.AlertText);
            alertText.fontStyle = FontStyles.Bold;
            Stretch((RectTransform)alertText.transform, 16);

            var banner = alert.gameObject.AddComponent<SoundAlertBanner>();
            Set(banner, "detector", detector);
            Set(banner, "group", group);
            Set(banner, "label", alertText);

            // Zona de subtítulos: lo más nuevo siempre abajo.
            var area = new GameObject("Subtitles", typeof(RectTransform), typeof(RectMask2D));
            var areaRect = (RectTransform)area.transform;
            areaRect.SetParent(column, false);
            Layout(areaRect, flexibleHeight: 1);
            var areaLayout = area.AddComponent<VerticalLayoutGroup>();
            areaLayout.childAlignment = TextAnchor.LowerCenter;
            areaLayout.spacing = 10;
            areaLayout.childControlWidth = areaLayout.childControlHeight = true;
            areaLayout.childForceExpandWidth = true;
            areaLayout.childForceExpandHeight = false;

            TextMeshProUGUI status = Text("Status", column, "Cargando…", 20, TextAlignmentOptions.Left,
                                          SubtitlePalette.TextSecondary);
            Layout((RectTransform)status.transform, preferredHeight: 28);

            var feed = area.AddComponent<SubtitleFeed>();
            Set(feed, "transcriber", transcriber);
            Set(feed, "settings", settings);
            Set(feed, "microphone", mic);
            Set(feed, "container", areaRect);
            Set(feed, "bubbleSprite", _pill);
            Set(feed, "statusLabel", status);
            return feed;
        }

        // ---------- Panel de ajustes ----------

        private static void BuildSettingsPanel(RectTransform canvas, AccessibilitySettings settings, SubtitleFeed feed,
                                               HeadFollowPanel follower, Button toggle)
        {
            RectTransform panel = Pill("SettingsPanel", canvas, SubtitlePalette.GlassStrong, 3f);
            // A la derecha de los subtítulos, centrado en vertical.
            panel.anchorMin = panel.anchorMax = new Vector2(1f, 0.5f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(460f, 0f);
            panel.anchoredPosition = new Vector2(24f, 0f);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 18);
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Encabezado: "Ajustes" + "Listo"
            RectTransform header = Row(panel, "Header", 52, background: false);
            TextMeshProUGUI title = Text("Title", header, "Ajustes", 30, TextAlignmentOptions.Left, SubtitlePalette.Text);
            title.fontStyle = FontStyles.Bold;
            Layout((RectTransform)title.transform, flexibleWidth: 1);
            Button close = MakeButton(header, "Close", new Color(0, 0, 0, 0), 1f, out RectTransform closeRect);
            Layout(closeRect, preferredWidth: 90);
            Stretch((RectTransform)Text("Text", closeRect, "Listo", 24, TextAlignmentOptions.Right, SubtitlePalette.Accent).transform);

            Section(panel, "TEXTO");
            RectTransform sizeRow = Row(panel, "FontSize", 50, background: true);
            Layout((RectTransform)Text("Name", sizeRow, "Tamaño", 21, TextAlignmentOptions.Left, SubtitlePalette.Text).transform, flexibleWidth: 1);
            Button smaller = SmallButton(sizeRow, "Smaller", "A-");
            TextMeshProUGUI sizeValue = Text("Value", sizeRow, "34", 22, TextAlignmentOptions.Center, SubtitlePalette.TextSecondary);
            Layout((RectTransform)sizeValue.transform, preferredWidth: 46);
            Button bigger = SmallButton(sizeRow, "Bigger", "A+");

            Button font = SettingRow(panel, "Fuente", out TextMeshProUGUI fontValue);
            Button contrast = SettingRow(panel, "Alto contraste", out TextMeshProUGUI contrastValue);
            Button position = SettingRow(panel, "Posición", out TextMeshProUGUI positionValue);

            Section(panel, "SUBTÍTULOS");
            Button colors = SettingRow(panel, "Colores por persona", out TextMeshProUGUI colorsValue);
            Button lines = SettingRow(panel, "Líneas en pantalla", out TextMeshProUGUI linesValue);
            Button hide = SettingRow(panel, "Ocultar después de", out TextMeshProUGUI hideValue);
            Button distant = SettingRow(panel, "Ignorar voces lejanas", out TextMeshProUGUI distantValue);

            Section(panel, "");
            Button clear = ActionRow(panel, "Nueva conversación");
            Button recenter = ActionRow(panel, "Centrar frente a mí");

            var component = canvas.gameObject.AddComponent<SettingsPanel>();
            Set(component, "settings", settings);
            Set(component, "feed", feed);
            Set(component, "follower", follower);
            Set(component, "panelRoot", panel.gameObject);
            Set(component, "toggleButton", toggle);
            Set(component, "closeButton", close);
            Set(component, "fontSmallerButton", smaller);
            Set(component, "fontBiggerButton", bigger);
            Set(component, "fontFaceButton", font);
            Set(component, "contrastButton", contrast);
            Set(component, "positionButton", position);
            Set(component, "speakerColorsButton", colors);
            Set(component, "maxLinesButton", lines);
            Set(component, "hideAfterButton", hide);
            Set(component, "distantVoicesButton", distant);
            Set(component, "clearButton", clear);
            Set(component, "recenterButton", recenter);
            Set(component, "fontSizeValue", sizeValue);
            Set(component, "fontFaceValue", fontValue);
            Set(component, "contrastValue", contrastValue);
            Set(component, "positionValue", positionValue);
            Set(component, "speakerColorsValue", colorsValue);
            Set(component, "maxLinesValue", linesValue);
            Set(component, "hideAfterValue", hideValue);
            Set(component, "distantVoicesValue", distantValue);

            panel.gameObject.SetActive(false);
        }

        /// <summary>Fila tipo lista: nombre a la izquierda, valor a la derecha. Toda la fila es botón.</summary>
        private static Button SettingRow(RectTransform panel, string name, out TextMeshProUGUI value)
        {
            Button button = MakeButton(panel, name, SubtitlePalette.Row, 4f, out RectTransform rect);
            Layout(rect, preferredHeight: 50);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            Layout((RectTransform)Text("Name", rect, name, 21, TextAlignmentOptions.Left, SubtitlePalette.Text).transform, flexibleWidth: 1);
            value = Text("Value", rect, "", 22, TextAlignmentOptions.Right, SubtitlePalette.TextSecondary);
            Layout((RectTransform)value.transform, preferredWidth: 120);
            return button;
        }

        private static Button ActionRow(RectTransform panel, string name)
        {
            Button button = MakeButton(panel, name, SubtitlePalette.Row, 4f, out RectTransform rect);
            Layout(rect, preferredHeight: 50);
            Stretch((RectTransform)Text("Text", rect, name, 21, TextAlignmentOptions.Center, SubtitlePalette.Accent).transform);
            return button;
        }

        private static Button SmallButton(RectTransform parent, string name, string text)
        {
            Button button = MakeButton(parent, name, new Color(1f, 1f, 1f, 0.12f), 2f, out RectTransform rect);
            Layout(rect, preferredWidth: 52, preferredHeight: 38);
            Stretch((RectTransform)Text("Text", rect, text, 20, TextAlignmentOptions.Center, SubtitlePalette.Text).transform);
            return button;
        }

        private static RectTransform Row(RectTransform parent, string name, float height, bool background)
        {
            RectTransform row = background ? Pill(name, parent, SubtitlePalette.Row, 4f) : Box(name, parent);
            Layout(row, preferredHeight: height);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(background ? 18 : 4, background ? 12 : 4, 0, 0);
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        private static void Section(RectTransform parent, string title)
        {
            TextMeshProUGUI label = Text("Section", parent, title, 16, TextAlignmentOptions.BottomLeft, SubtitlePalette.TextSecondary);
            label.characterSpacing = 6;
            label.margin = new Vector4(6, 0, 0, 2);
            Layout((RectTransform)label.transform, preferredHeight: string.IsNullOrEmpty(title) ? 6 : 28);
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

        private static TextMeshProUGUI Text(string name, Transform parent, string text, float size,
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

        private static Button MakeButton(Transform parent, string name, Color color, float roundness, out RectTransform rect)
        {
            rect = Pill(name, parent, color, roundness);
            rect.GetComponent<Image>().raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            // Al apuntar, el fondo se vuelve más opaco (retroalimentación sutil).
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 2.2f);
            colors.pressedColor = new Color(1f, 1f, 1f, 3.2f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            return button;
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

        /// <summary>
        /// Crea (una sola vez) un círculo blanco suavizado de 128 px y lo importa como sprite
        /// 9-slice. Estirado se convierte en cápsula o rectángulo redondeado.
        /// </summary>
        private static Sprite GetOrCreatePillSprite()
        {
            if (!File.Exists(PillPath))
            {
                EnsureFolder("Assets/Subtitulos", "Art");
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

        /// <summary>Inter primero (limpia, tipo sistema), luego LiberationSans como alternativa.</summary>
        private static TMP_FontAsset[] FindFonts()
        {
            return AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(p => !p.Contains("Fallback") && !p.Contains("NoBackface"))
                .OrderBy(p => p.Contains("Inter") ? 0 : 1)
                .Select(p => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p))
                .Where(f => f != null)
                .Take(3)
                .ToArray();
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

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
