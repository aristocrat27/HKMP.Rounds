using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HKMP.Rounds.Client
{
    internal sealed class RoundsWindowPointerHandle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform _window;
        private RectTransform _canvas;
        private bool _resize;
        private bool _dragging;
        private Vector2 _pointerStart;
        private Vector2 _positionStart;
        private Vector2 _sizeStart;
        private Action _onResize;

        public void Initialize(
            RectTransform window,
            RectTransform canvas,
            bool resize,
            Action onResize = null)
        {
            _window = window;
            _canvas = canvas;
            _resize = resize;
            _onResize = onResize;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_window == null || _canvas == null || eventData == null ||
                eventData.button != PointerEventData.InputButton.Left) return;

            Vector2 point;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas,
                    eventData.position,
                    eventData.pressEventCamera,
                    out point))
            {
                return;
            }

            _pointerStart = point;
            _positionStart = _window.anchoredPosition;
            _sizeStart = _window.sizeDelta;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _window == null || _canvas == null || eventData == null) return;

            Vector2 point;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas,
                    eventData.position,
                    eventData.pressEventCamera,
                    out point))
            {
                return;
            }

            Vector2 delta = point - _pointerStart;
            Vector2 position = _positionStart;
            Vector2 size = _sizeStart;

            if (_resize)
            {
                float maximumWidth = Mathf.Max(1f, _canvas.rect.width - 48f);
                float maximumHeight = Mathf.Max(1f, _canvas.rect.height - 48f);
                float minimumWidth = Mathf.Min(320f, maximumWidth);
                float minimumHeight = Mathf.Min(240f, maximumHeight);
                size.x = Mathf.Clamp(
                    _sizeStart.x - delta.x,
                    minimumWidth,
                    maximumWidth);
                size.y = Mathf.Clamp(
                    _sizeStart.y - delta.y,
                    minimumHeight,
                    maximumHeight);
                position.x += (_sizeStart.x - size.x) * 0.5f;
                position.y += (_sizeStart.y - size.y) * 0.5f;
            }
            else
            {
                position += delta;
            }

            float horizontalLimit = Mathf.Max(0f, _canvas.rect.width * 0.5f - size.x * 0.5f + 80f);
            float verticalLimit = Mathf.Max(0f, _canvas.rect.height * 0.5f - size.y * 0.5f + 60f);
            position.x = Mathf.Clamp(position.x, -horizontalLimit, horizontalLimit);
            position.y = Mathf.Clamp(position.y, -verticalLimit, verticalLimit);

            _window.sizeDelta = size;
            _window.anchoredPosition = position;

            if (_resize)
            {
                _onResize?.Invoke();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _dragging = false;
        }
    }
}
