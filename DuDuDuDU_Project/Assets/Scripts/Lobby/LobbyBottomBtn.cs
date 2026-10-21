using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;


namespace OJ.Lobby
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

        /// <summary>해금 가이드가 "눌렀는가" 를 듣는다(<c>LobbyContentGate.ButtonOf</c>).</summary>
        public Button Button => _button;

        /// <summary>잠금 그림. 처음 잠글 때 코드로 만든다 — 프리팹·씬을 고치지 않는다.</summary>
        private GameObject _lockIcon;

        /// <summary>"강화할 수 있다" 레드닷. 처음 켤 때 코드로 만든다.</summary>
        private GameObject _upgradeBadge;

        public void SetUpgradeBadge(bool on)
        {
            OJ.UI.UpgradeRedDot.Set(ref _upgradeBadge, transform, on, 54f, new Vector2(-40f, -26f));
        }

        private const string LockSpritePath = "Art/Upgrade/Icon_lock";
        private const float LockedAlpha = 0.4f;

        /// <summary>
        /// 컨텐츠가 아직 안 열린 탭. <b>숨기지 않는다</b> — 탭이 빠지면 나머지 너비가 바뀌어 하단 바가 출렁인다.
        /// 그림을 흐리게 하고 자물쇠를 얹는다. 누르면 무엇이 일어나는지는 <c>LobbyLayoutController.ShowTab</c> 이 정한다.
        /// </summary>
        public void SetLocked(bool locked)
        {
            SetGroupAlpha(_minWidthObj, locked ? LockedAlpha : 1f);
            SetGroupAlpha(_maxWidthObj, locked ? LockedAlpha : 1f);

            if (locked && _lockIcon == null)
                _lockIcon = CreateLockIcon();

            if (_lockIcon != null)
                _lockIcon.SetActive(locked);
        }

        private static void SetGroupAlpha(Transform target, float alpha)
        {
            if (target == null)
                return;

            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                if (Mathf.Approximately(alpha, 1f))
                    return;

                group = target.gameObject.AddComponent<CanvasGroup>();
            }

            group.alpha = alpha;
        }

        private GameObject CreateLockIcon()
        {
            var go = new GameObject("Lock", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(72f, 72f);
            rect.anchoredPosition = new Vector2(0f, 8f);

            var image = go.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(LockSpritePath);
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (image.sprite == null)
                Debug.LogWarning("[해금] 자물쇠 그림을 못 찾았다: Resources/" + LockSpritePath);

            return go;
        }

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
