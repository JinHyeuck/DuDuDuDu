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

            if (_minWidthObj != null) _minWidthObj.gameObject.SetActive(!isSelected);
            if (_maxWidthObj != null) _maxWidthObj.gameObject.SetActive(isSelected);

            if (duration <= 0f)
            {
                if (_rect != null) SetWidth(targetWidth);
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

                yield return null;
            }

            if (_rect != null) SetWidth(targetWidth);

            _widthRoutine = null;
        }

        private void SetWidth(float width)
        {
            Vector2 size = _rect.sizeDelta;
            size.x = width;
            _rect.sizeDelta = size;
        }

    }
}
