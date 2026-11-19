using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Subtitulos.UI.Controls
{
    /// <summary>
    /// Interruptor encendido/apagado con perilla animada. Comunica el estado con
    /// posición + color (no solo color), como los interruptores de los sistemas operativos.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ToggleSwitch : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private RectTransform knob;
        [SerializeField] private Image track;
        [SerializeField] private Color onColor = new Color32(0x4C, 0xB0, 0xF2, 0xFF);
        [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private float knobPadding = 4f;
        [SerializeField] private float animationSpeed = 14f;

        public event Action<bool> Changed;

        public bool IsOn { get; private set; }

        private float _t; // 0 = apagado, 1 = encendido (animado)

        public void SetValue(bool on, bool notify = false, bool instant = false)
        {
            bool changed = on != IsOn;
            IsOn = on;
            if (instant) _t = on ? 1f : 0f;
            ApplyVisual();
            if (changed && notify) Changed?.Invoke(on);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            SetValue(!IsOn, notify: true);
        }

        public void OnSubmit(BaseEventData eventData) => OnPointerClick(null);

        private void Update()
        {
            float target = IsOn ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.Lerp(_t, target, 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime));
            if (Mathf.Abs(_t - target) < 0.002f) _t = target;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (track != null) track.color = Color.Lerp(offColor, onColor, _t);
            if (knob == null) return;

            var rect = (RectTransform)transform;
            float travel = rect.rect.width - knob.rect.width - knobPadding * 2f;
            knob.anchoredPosition = new Vector2(knobPadding + travel * _t, 0f);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            ApplyVisual();
        }
    }
}
