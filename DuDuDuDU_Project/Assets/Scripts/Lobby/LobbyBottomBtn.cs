using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;


namespace OJ
{
    public class LobbyBottomBtn : MonoBehaviour
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private float _minwidth = 188f;
        [SerializeField] private float _maxwidth = 320f;
        [SerializeField] private Transform _minWidthObj;
        [SerializeField] private Transform _maxWidthObj;
        [SerializeField] private float _maxWidthObj_HideScale = 0.7f;
        [SerializeField] private float _maxWidthObj_HideYpos = -8.5f;
        [SerializeField] private float _maxWidthObj_ShowScale = 1.0f;
        [SerializeField] private float _maxWidthObj_ShowYpos = 38.69f;

        [SerializeField] private Button _button;

        public LobbyTab _tab;

        [SerializeField] private float _widthChangeDuration = 0.2f;

        private Action<LobbyTab> _onClick;
        private Coroutine _widthRoutine;
        private bool _isCurrentStateInitialized;
        private bool _isSelected;

        public void Init(Action<LobbyTab> onClick)
        {
            _onClick = onClick;
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
                _button.onClick.AddListener(OnClick);
            }
        }

        private void OnClick()
        {
            _onClick?.Invoke(_tab);
        }

        public void SetState(bool isSelected)
        {
            if (_isCurrentStateInitialized && _isSelected == isSelected) return;

            _isCurrentStateInitialized = true;
            _isSelected = isSelected;

            if (_widthRoutine != null) StopCoroutine(_widthRoutine);
            _widthRoutine = StartCoroutine(AnimateState(isSelected));
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClick);
        }

        private IEnumerator AnimateState(bool isSelected)
        {
            float targetWidth = isSelected ? _maxwidth : _minwidth;
            float duration = Mathf.Max(0f, _widthChangeDuration);
            float targetScale = isSelected ? _maxWidthObj_ShowScale : _maxWidthObj_HideScale;
            float targetY = isSelected ? _maxWidthObj_ShowYpos : _maxWidthObj_HideYpos;

            if (_minWidthObj != null) _minWidthObj.gameObject.SetActive(!isSelected);

            float startScale = targetScale;
            float startY = targetY;
            if (_maxWidthObj != null)
            {
                if (isSelected) _maxWidthObj.gameObject.SetActive(true);
                startScale = _maxWidthObj.localScale.x;
                startY = _maxWidthObj.localPosition.y;
            }

            if (duration <= 0f)
            {
                if (_rect != null) SetWidth(targetWidth);
                SetMaxWidthObjVisual(targetScale, targetY);
                if (_maxWidthObj != null) _maxWidthObj.gameObject.SetActive(isSelected);
                _widthRoutine = null;
                yield break;
            }

            float startWidth = _rect != null ? _rect.sizeDelta.x : targetWidth;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float widthT = Mathf.Clamp01(elapsed / duration);

                if (_rect != null) SetWidth(Mathf.Lerp(startWidth, targetWidth, widthT));
                SetMaxWidthObjVisual(
                    Mathf.Lerp(startScale, targetScale, widthT),
                    Mathf.Lerp(startY, targetY, widthT));

                yield return null;
            }

            if (_rect != null) SetWidth(targetWidth);
            SetMaxWidthObjVisual(targetScale, targetY);
            if (_maxWidthObj != null) _maxWidthObj.gameObject.SetActive(isSelected);

            _widthRoutine = null;
        }

        private void SetWidth(float width)
        {
            Vector2 size = _rect.sizeDelta;
            size.x = width;
            _rect.sizeDelta = size;
        }

        private void SetMaxWidthObjVisual(float scale, float ypos)
        {
            if (_maxWidthObj == null) return;

            Vector3 localScale = _maxWidthObj.localScale;
            localScale.x = scale;
            localScale.y = scale;
            _maxWidthObj.localScale = localScale;

            Vector3 localPos = _maxWidthObj.localPosition;
            localPos.y = ypos;
            _maxWidthObj.localPosition = localPos;
        }

    }
}
