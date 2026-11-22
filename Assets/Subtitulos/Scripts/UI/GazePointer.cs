using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Subtitulos.UI
{
    /// <summary>
    /// Puntero por mirada para el panel de ajustes. No depende de los rayos de los controles
    /// ni de la configuración de XRI, así que funciona igual con controles, con manos o con
    /// lentes sin controles (que es la meta del proyecto).
    ///
    ///  • Un punto sigue hacia donde mira la persona cuando está sobre el panel.
    ///  • Sobre un botón o interruptor aparece un aro que se llena: al completarse (1.2 s) se activa.
    ///  • Para no esperar, se puede apretar el gatillo de cualquier control y se activa al instante.
    ///
    /// Se agrega solo al SubtitleCanvas al arrancar, aunque la escena no se haya reconstruido.
    /// </summary>
    public class GazePointer : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [Tooltip("Segundos mirando un botón para activarlo. 0 = desactivar la selección por mirada.")]
        [SerializeField] private float dwellSeconds = 1.2f;
        [SerializeField] private float maxDistance = 6f;

        private readonly List<Selectable> _selectables = new List<Selectable>();
        private RectTransform _canvasRect;
        private RectTransform _reticle;
        private Image _dot;
        private Image _ring;
        private Selectable _hover;
        private Selectable _clicked;      // ya se activó por mirada: hay que apartar la vista antes de repetir
        private float _dwell;
        private bool _triggerWasDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            GameObject go = GameObject.Find("SubtitleCanvas");
            if (go != null && go.GetComponent<GazePointer>() == null) go.AddComponent<GazePointer>();
        }

        private void Awake()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            _canvasRect = (RectTransform)canvas.transform;
            BuildReticle();
        }

        private void BuildReticle()
        {
            var root = new GameObject("GazeReticle", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            _reticle = (RectTransform)root.transform;
            _reticle.sizeDelta = new Vector2(40f, 40f);

            _ring = MakeImage("Ring", _reticle, MakeCircleSprite(64, ring: true), 40f, new Color(0.36f, 0.72f, 0.94f, 1f));
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _ring.fillAmount = 0f;
            _dot = MakeImage("Dot", _reticle, MakeCircleSprite(32, ring: false), 12f, new Color(1f, 1f, 1f, 0.9f));
            _reticle.gameObject.SetActive(false);
        }

        private static Image MakeImage(string name, Transform parent, Sprite sprite, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite MakeCircleSprite(int size, bool ring)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float r = size / 2f, inner = ring ? r * 0.78f : 0f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d) * (ring ? Mathf.Clamp01(d - inner) : 1f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam == null || canvas == null) return;

            Transform ct = canvas.transform;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var plane = new Plane(ct.forward, ct.position);
            if (!plane.Raycast(ray, out float distance) || distance <= 0f || distance > maxDistance)
            {
                SetHover(null);
                _reticle.gameObject.SetActive(false);
                return;
            }
            Vector3 hit = ray.GetPoint(distance);

            // Qué control está bajo la mirada (el último de la jerarquía es el que se dibuja encima).
            canvas.GetComponentsInChildren(false, _selectables);
            Selectable target = null;
            for (int i = 0; i < _selectables.Count; i++)
            {
                Selectable s = _selectables[i];
                if (s == null || !s.IsInteractable() || !s.gameObject.activeInHierarchy) continue;
                var rect = (RectTransform)s.transform;
                if (rect.rect.Contains(rect.InverseTransformPoint(hit))) target = s;
            }

            bool overCanvas = _canvasRect.rect.Contains(_canvasRect.InverseTransformPoint(hit));
            _reticle.gameObject.SetActive(target != null || overCanvas);
            if (!_reticle.gameObject.activeSelf) { SetHover(null); return; }

            _reticle.position = hit;
            _reticle.rotation = ct.rotation;
            _reticle.SetAsLastSibling();

            SetHover(target);
            if (target == null || target == _clicked)
            {
                if (target == null) _clicked = null;
                _dwell = 0f;
                _ring.fillAmount = 0f;
            }
            else if (dwellSeconds > 0f)
            {
                _dwell += Time.unscaledDeltaTime;
                _ring.fillAmount = Mathf.Clamp01(_dwell / dwellSeconds);
                if (_dwell >= dwellSeconds) Click(target);
            }

            // Gatillo de cualquier control: activa al instante.
            bool down = TriggerDown();
            if (down && !_triggerWasDown && target != null) Click(target);
            _triggerWasDown = down;
        }

        private static bool TriggerDown()
        {
            foreach (XRNode node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                InputDevice device = InputDevices.GetDeviceAtXRNode(node);
                if (device.isValid && device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed)
                    return true;
            }
            return false;
        }

        private void SetHover(Selectable target)
        {
            if (target == _hover) return;
            if (_hover != null) ExecuteEvents.Execute(_hover.gameObject, NewEvent(), ExecuteEvents.pointerExitHandler);
            _hover = target;
            _dwell = 0f;
            if (_ring != null) _ring.fillAmount = 0f;
            if (_hover != null) ExecuteEvents.Execute(_hover.gameObject, NewEvent(), ExecuteEvents.pointerEnterHandler);
        }

        private void Click(Selectable target)
        {
            _clicked = target;
            _dwell = 0f;
            _ring.fillAmount = 0f;
            PointerEventData data = NewEvent();
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        private static PointerEventData NewEvent() =>
            new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
    }
}
