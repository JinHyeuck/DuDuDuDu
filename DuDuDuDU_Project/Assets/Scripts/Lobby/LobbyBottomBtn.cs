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
            float rotateTime = duration * 0.5f;

            Transform fromObj = isSelected ? _minWidthObj : _maxWidthObj;
            Transform toObj = isSelected ? _maxWidthObj : _minWidthObj;

            if (duration <= 0f)
            {
                if (_rect != null) SetWidth(targetWidth);
                if (fromObj != null) fromObj.gameObject.SetActive(false);
                if (toObj != null)
                {
                    toObj.gameObject.SetActive(true);
                    Vector3 toEuler = toObj.localEulerAngles;
                    toEuler.y = 0f;
                    toObj.localEulerAngles = toEuler;
                }
                _widthRoutine = null;
                yield break;
            }

            float startWidth = _rect != null ? _rect.sizeDelta.x : targetWidth;
            bool swapped = false;

            if (fromObj != null)
            {
                fromObj.gameObject.SetActive(true);
                Vector3 fromStartEuler = fromObj.localEulerAngles;
                fromStartEuler.y = 0f;
                fromObj.localEulerAngles = fromStartEuler;
            }

            if (toObj != null) toObj.gameObject.SetActive(false);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float widthT = Mathf.Clamp01(elapsed / duration);

                if (_rect != null) SetWidth(Mathf.Lerp(startWidth, targetWidth, widthT));

                if (!swapped && elapsed >= rotateTime)
                {
                    swapped = true;
                    if (fromObj != null) fromObj.gameObject.SetActive(false);
                    if (toObj != null)
                    {
                        toObj.gameObject.SetActive(true);
                        Vector3 toStartEuler = toObj.localEulerAngles;
                        toStartEuler.y = 90f;
                        toObj.localEulerAngles = toStartEuler;
                    }
                }

                if (!swapped)
                {
                    if (fromObj != null)
                    {
                        Vector3 euler = fromObj.localEulerAngles;
                        euler.y = Mathf.Lerp(0f, 90f, Mathf.Clamp01(elapsed / rotateTime));
                        fromObj.localEulerAngles = euler;
                    }
                }
                else
                {
                    if (toObj != null)
                    {
                        float secondElapsed = Mathf.Clamp(elapsed - rotateTime, 0f, rotateTime);
                        Vector3 euler = toObj.localEulerAngles;
                        euler.y = Mathf.Lerp(90f, 0f, Mathf.Clamp01(secondElapsed / rotateTime));
                        toObj.localEulerAngles = euler;
                    }
                }

                yield return null;
            }

            if (_rect != null) SetWidth(targetWidth);
            if (fromObj != null) fromObj.gameObject.SetActive(false);
            if (toObj != null)
            {
                toObj.gameObject.SetActive(true);
                Vector3 toEndEuler = toObj.localEulerAngles;
                toEndEuler.y = 0f;
                toObj.localEulerAngles = toEndEuler;
            }

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
