using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Subtitulos.UI.Controls
{
    /// <summary>
    /// Selector de opciones en una sola fila (p. ej. "Español | English | Auto").
    /// Todas las opciones están a la vista y una cápsula se desliza a la elegida:
    /// el usuario ve las alternativas sin abrir menús (reconocer en vez de recordar).
    /// </summary>
    public class SegmentedControl : MonoBehaviour
    {
        [SerializeField] private RectTransform highlight;
        [SerializeField] private Button[] segments = Array.Empty<Button>();
        [SerializeField] private TextMeshProUGUI[] labels = Array.Empty<TextMeshProUGUI>();
        [SerializeField] private Color selectedText = new Color(0.06f, 0.06f, 0.08f, 1f);
        [SerializeField] private Color normalText = new Color(1f, 1f, 1f, 0.78f);
        [SerializeField] private float animationSpeed = 16f;

        public event Action<int> Changed;

        public int Index { get; private set; } = -1;

        private bool _snap = true;

        private void Awake()
        {
            for (int i = 0; i < segments.Length; i++)
            {
                int index = i;
                segments[i].onClick.AddListener(() => Select(index, notify: true));
            }
        }

        public void Select(int index, bool notify = false)
        {
            index = Mathf.Clamp(index, 0, segments.Length - 1);
            bool changed = index != Index;
            Index = index;

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                labels[i].color = i == index ? selectedText : normalText;
                labels[i].fontStyle = i == index ? FontStyles.Bold : FontStyles.Normal;
            }

            if (changed && notify) Changed?.Invoke(index);
        }

        private void LateUpdate()
        {
            if (highlight == null || Index < 0 || Index >= segments.Length) return;

            var target = (RectTransform)segments[Index].transform;
            if (target.rect.width <= 0f) return; // el layout todavía no se calcula

            // Mover la cápsula a la posición y tamaño del segmento elegido.
            Vector2 targetPos = (Vector2)highlight.parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Vector2 targetSize = new Vector2(target.rect.width, target.rect.height);

            float t = _snap ? 1f : 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);
            highlight.localPosition = Vector2.Lerp(highlight.localPosition, targetPos, t);
            highlight.sizeDelta = Vector2.Lerp(highlight.sizeDelta, targetSize, t);
            _snap = false;
        }

        private void OnEnable() => _snap = true;
    }
}
