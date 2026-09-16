using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pinball
{
    /// <summary>
    /// 구워진 UI 프리팹의 루트에 붙는다.
    /// 보드 좌표(유닛) -> UI anchoredPosition(px) 변환, 구슬 풀링, 핀 개별 연출을 담당한다.
    ///
    /// 자식 RectTransform 은 모두 anchor (0,0) + pivot (0.5,0.5) 로 만들어진다.
    /// 그래서 anchoredPosition 이 곧 "판 좌하단으로부터의 픽셀 좌표"가 되고,
    /// 변환이 단순 곱셈 하나로 끝난다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PinballBoardView : MonoBehaviour
    {
        [Header("구울 때 자동으로 채워지는 값 — 손으로 건드리지 마세요")]
        public PinballBoard board;
        public float pixelsPerUnit = 100f;

        [Tooltip("board.pegs 와 인덱스가 1:1 대응한다. 시뮬의 HitEvent.pegIndex 로 바로 찾는다.")]
        public RectTransform[] pegs;

        [Tooltip("보상 칸 영역. 아이콘이나 수량 텍스트를 자식으로 붙이면 된다.")]
        public RectTransform[] slots;

        [Tooltip("구슬 '원본'. 실행 시 비활성화되고, 동시에 굴러가는 구슬만큼 복제된다.")]
        public RectTransform ball;

        public RectTransform launchPoint;

        [Header("연출")]
        public bool pegPunchEnabled = true;
        public float pegPunchScale = 1.35f;
        public float pegPunchDuration = 0.18f;

        private float[] _punchTimer;
        private Vector3[] _pegBaseScale;
        private Graphic[] _pegGraphic;
        private Color[] _pegBaseColor;

        private readonly Stack<RectTransform> _ballPool = new Stack<RectTransform>();
        private readonly List<RectTransform> _ballsInUse = new List<RectTransform>();

        private void Awake()
        {
            CachePegs();

            // 원본은 숨겨두고 복제본만 보여준다.
            // 에디터에서는 켜진 채로 두는 편이 배치 확인에 편하므로 런타임에만 끈다.
            if (ball != null) ball.gameObject.SetActive(false);
        }

        private void CachePegs()
        {
            if (pegs == null) return;

            _punchTimer = new float[pegs.Length];
            _pegBaseScale = new Vector3[pegs.Length];
            _pegGraphic = new Graphic[pegs.Length];
            _pegBaseColor = new Color[pegs.Length];

            for (int i = 0; i < pegs.Length; i++)
            {
                if (pegs[i] == null) continue;
                _pegBaseScale[i] = pegs[i].localScale;
                _pegGraphic[i] = pegs[i].GetComponent<Graphic>();
                if (_pegGraphic[i] != null) _pegBaseColor[i] = _pegGraphic[i].color;
                _punchTimer[i] = -1f;
            }
        }

        // ── 좌표 변환

        public Vector2 BoardToAnchored(Vector2 boardPos) => boardPos * pixelsPerUnit;

        // ── 구슬 풀

        public int ActiveBallCount => _ballsInUse.Count;

        /// <summary>구슬 하나를 빌린다. 동시에 여러 개를 굴리기 위한 복제.</summary>
        public RectTransform AcquireBall()
        {
            if (ball == null) return null;

            RectTransform rt = _ballPool.Count > 0 ? _ballPool.Pop() : Instantiate(ball, ball.parent);
            rt.gameObject.SetActive(true);
            rt.SetAsLastSibling();              // 항상 판 위에 보이도록
            _ballsInUse.Add(rt);
            return rt;
        }

        public void ReleaseBall(RectTransform rt)
        {
            if (rt == null) return;
            _ballsInUse.Remove(rt);
            rt.gameObject.SetActive(false);
            _ballPool.Push(rt);
        }

        public void SetBallPosition(RectTransform rt, Vector2 boardPos)
        {
            if (rt != null) rt.anchoredPosition = BoardToAnchored(boardPos);
        }

        /// <summary>굴러가던 구슬을 전부 회수한다. 씬 전환이나 강제 중단 시.</summary>
        public void ReleaseAllBalls()
        {
            for (int i = _ballsInUse.Count - 1; i >= 0; i--)
            {
                var rt = _ballsInUse[i];
                if (rt == null) continue;
                rt.gameObject.SetActive(false);
                _ballPool.Push(rt);
            }
            _ballsInUse.Clear();
        }

        // ── 핀 연출

        /// <param name="strength">0~1. HitEvent.impact 를 정규화해서 넘기면 세기가 반영된다.</param>
        public void PunchPeg(int index, float strength = 1f)
        {
            if (!pegPunchEnabled || _punchTimer == null) return;
            if (index < 0 || index >= _punchTimer.Length) return;

            // 여러 구슬이 같은 핀을 연달아 치면 더 센 쪽이 이긴다
            float t = pegPunchDuration * Mathf.Clamp(strength, 0.4f, 1f);
            if (t > _punchTimer[index]) _punchTimer[index] = t;
        }

        private void Update()
        {
            if (_punchTimer == null) return;

            for (int i = 0; i < _punchTimer.Length; i++)
            {
                if (_punchTimer[i] < 0f) continue;

                _punchTimer[i] -= Time.deltaTime;
                var rt = pegs[i];
                if (rt == null) { _punchTimer[i] = -1f; continue; }

                if (_punchTimer[i] <= 0f)
                {
                    rt.localScale = _pegBaseScale[i];
                    if (_pegGraphic[i] != null) _pegGraphic[i].color = _pegBaseColor[i];
                    _punchTimer[i] = -1f;
                    continue;
                }

                // 튀었다가 원래 크기로 돌아오는 감쇠
                float k01 = _punchTimer[i] / Mathf.Max(0.0001f, pegPunchDuration);
                rt.localScale = _pegBaseScale[i] * Mathf.Lerp(1f, pegPunchScale, k01 * k01);

                if (_pegGraphic[i] != null)
                    _pegGraphic[i].color = Color.Lerp(_pegBaseColor[i], Color.white, k01);
            }
        }

        /// <summary>슬롯 인덱스의 화면 중심(anchoredPosition). 보상 팝업을 띄울 위치로 쓴다.</summary>
        public Vector2 SlotCenter(int slot)
        {
            if (slots != null && slot >= 0 && slot < slots.Length && slots[slot] != null)
                return slots[slot].anchoredPosition;

            if (board == null) return Vector2.zero;

            float left = slot == 0 ? board.slotMinX : board.dividersX[slot - 1];
            float right = slot >= board.dividersX.Length ? board.slotMaxX : board.dividersX[slot];
            return BoardToAnchored(new Vector2((left + right) * 0.5f,
                                               (board.dividerBottomY + board.dividerTopY) * 0.5f));
        }
    }
}
