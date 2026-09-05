using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Genevore.Player;

namespace Genevore.UI
{
    /// <summary>
    /// Lightweight on-screen joystick. Writes Vector2 into MobilePlayerController.
    /// No per-frame Canvas rebuild; only RectTransform.anchoredPosition is mutated.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private float handleRange = 50f;
        [SerializeField] private MobilePlayerController playerController;

        private Vector2 _input = Vector2.zero;
        private Canvas _canvas;
        private Camera _uiCamera;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                _uiCamera = _canvas.worldCamera;
            }

            if (playerController == null)
            {
                playerController = FindObjectOfType<MobilePlayerController>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null) return;

            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, _uiCamera, out pos);

            pos = Vector2.ClampMagnitude(pos, handleRange);
            if (handle != null) handle.anchoredPosition = pos;

            _input = pos / handleRange;
            if (playerController != null)
            {
                playerController.SetJoystickInput(_input);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _input = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            if (playerController != null)
            {
                playerController.SetJoystickInput(Vector2.zero);
            }
        }
    }
}
