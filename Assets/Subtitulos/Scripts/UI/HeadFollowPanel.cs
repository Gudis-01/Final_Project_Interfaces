using UnityEngine;

namespace Subtitulos.UI
{
    /// <summary>
    /// Mantiene el panel de subtítulos frente a la vista, como lo haría la pantalla
    /// de unos lentes. Sigue la cabeza "con retraso" (lazy follow): solo se recoloca
    /// cuando te alejas mucho del panel, para que el texto no tiemble con cada
    /// movimiento pequeño (esto ayuda a leer y evita mareo).
    /// </summary>
    public class HeadFollowPanel : MonoBehaviour
    {
        [Tooltip("Cámara del visor. Si está vacío se usa Camera.main.")]
        [SerializeField] private Transform head;
        [SerializeField] private AccessibilitySettings settings;

        [SerializeField] private float distance = 1.5f;

        [Header("Altura según la posición elegida (metros respecto a los ojos)")]
        [SerializeField] private float bottomOffset = -0.36f;
        [SerializeField] private float middleOffset = 0f;
        [SerializeField] private float topOffset = 0.25f;

        [Header("Seguimiento")]
        [Tooltip("Grados que puedes girar antes de que el panel te alcance.")]
        [SerializeField] private float angleThreshold = 25f;
        [SerializeField] private float followSpeed = 4f;

        private bool _repositioning = true;
        private bool _paused;

        /// <summary>
        /// Mientras está en pausa el panel se queda quieto (p. ej. con los ajustes abiertos,
        /// para que no "huya" cuando volteas a verlos). Al reanudar se recentra.
        /// </summary>
        public bool Paused
        {
            get => _paused;
            set
            {
                if (_paused && !value) Recenter();
                _paused = value;
            }
        }

        private void OnEnable()
        {
            if (settings != null) settings.Changed += OnSettingsChanged;
        }

        private void OnDisable()
        {
            if (settings != null) settings.Changed -= OnSettingsChanged;
        }

        // Al cambiar la posición (arriba/centro/abajo) el panel se mueve de inmediato.
        private void OnSettingsChanged(AccessibilityValues _) => Recenter();

        private void LateUpdate()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null || _paused) return;

            Vector3 target = TargetPosition();
            Vector3 toPanel = transform.position - head.position;
            Vector3 toTarget = target - head.position;

            if (Vector3.Angle(toPanel, toTarget) > angleThreshold) _repositioning = true;

            if (_repositioning)
            {
                float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, target, t);
                if (Vector3.Distance(transform.position, target) < 0.01f) _repositioning = false;
            }

            // Siempre mirando al usuario y derecho (sin inclinarse con la cabeza).
            Vector3 look = transform.position - head.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(look, Vector3.up);
        }

        private Vector3 TargetPosition()
        {
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            return head.position + forward * distance + Vector3.up * VerticalOffset();
        }

        private float VerticalOffset()
        {
            if (settings == null) return bottomOffset;
            switch (settings.Values.position)
            {
                case SubtitlePosition.Top: return topOffset;
                case SubtitlePosition.Middle: return middleOffset;
                default: return bottomOffset;
            }
        }

        /// <summary>Recolocar el panel de inmediato frente al usuario.</summary>
        public void Recenter() => _repositioning = true;
    }
}
