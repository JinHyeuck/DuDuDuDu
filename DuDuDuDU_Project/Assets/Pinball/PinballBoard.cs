using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// 특수 핀 태그 하나에 대한 목표 적중 확률.
    /// "한 번이라도 맞을 확률"을 지정한다. 여러 번 맞는 것까지 구분하지는 않는다.
    /// </summary>
    [Serializable]
    public struct SpecialPegRule
    {
        [Tooltip("Peg.specialTag 와 같은 값.")]
        public int tag;

        [Tooltip("에디터 표시용 이름. 로직에는 쓰이지 않는다.")]
        public string label;

        [Tooltip("이 태그의 핀에 한 번이라도 맞을 목표 확률 (0~1).")]
        [Range(0f, 1f)] public float targetHitProbability;
    }

    public enum SpecialHitPolicy
    {
        /// <summary>물리가 만드는 대로. 적중 횟수를 제어하지 않는다.</summary>
        Natural = 0,

        /// <summary>확률표대로 적중 횟수를 먼저 뽑고, 그 결과가 나온 시드를 고른다.</summary>
        Declared = 1,
    }

    [Serializable]
    public struct Peg
    {
        [Tooltip("핀 중심 좌표 (보드 로컬, 좌하단이 원점).")]
        public Vector2 center;

        [Tooltip("핀 반지름. 작은 핀 0.10~0.13, 범퍼 0.25~0.35 정도가 무난.")]
        public float radius;

        [Tooltip("이 핀만의 반발계수. 0 이면 보드 기본값(pegRestitution)을 쓴다.\n" +
                 "범퍼를 0.95 로 올리면 그 핀에 맞을 때만 크게 튄다.")]
        public float restitution;

        [Tooltip("0 이면 평범한 핀. 0 이 아니면 특수 핀이고, 맞을 때마다 카운트된다.\n" +
                 "같은 값을 여러 핀에 주면 한 그룹으로 묶인다(예: 황금핀 전부 1번).\n" +
                 "★ 물리에는 영향이 없지만, 시드별 적중 횟수가 테이블에 기록되므로\n" +
                 "  값을 바꾸면 시드 테이블을 다시 구워야 한다.")]
        public int specialTag;
    }

    [Serializable]
    public struct Segment
    {
        public Vector2 a;
        public Vector2 b;

        [Tooltip("이 벽만의 반발계수. 0 이면 보드 기본값(wallRestitution).")]
        public float restitution;
    }

    /// <summary>
    /// 워커 스레드에서 읽을 순수 C# 스냅샷.
    /// UnityEngine.Object 필드를 스레드에서 건드리면 Unity 가 alive 체크를 하다 터지므로,
    /// 베이크 직전에 반드시 이걸로 복사해서 넘긴다.
    /// 칸막이 벽은 여기서 dividersX 로부터 자동 생성되어 segments 뒤에 붙는다.
    /// </summary>
    public sealed class BoardData
    {
        public Vector2 gravity;
        public float linearDrag;
        public float ballRadius;
        public float pegRestitution;
        public float wallRestitution;
        public float maxSpeed;
        public float dt;
        public int maxSteps;

        public Vector2 launchPos;
        public float launchSpeed;
        public float launchAngleDeg;
        public float angleJitterDeg;
        public float speedJitter;

        public float catchLineY;
        public float armHeightY;
        public float slotMinX;
        public float slotMaxX;
        public float[] dividersX;

        public Peg[] pegs;
        public Segment[] segments;

        /// <summary>
        /// 핀별 특수 태그의 비트 인덱스. 특수 핀이 아니면 -1.
        /// 시뮬 루프 안에서 태그를 찾아 헤매지 않도록 미리 계산해둔다.
        /// </summary>
        public int[] pegTagBit;

        /// <summary>비트 인덱스 -> 태그 값.</summary>
        public int[] tagByBit;

        public int SlotCount => dividersX.Length + 1;

        public int SlotOf(float x)
        {
            for (int i = 0; i < dividersX.Length; i++)
                if (x < dividersX[i]) return i;
            return dividersX.Length;
        }
    }


    /// <summary>
    /// UI 프리팹을 구울 때 쓰는 설정. 보드 에셋에 함께 저장하므로 다시 구워도 같은 결과가 나온다.
    /// ★ 시뮬 결과에 영향이 없으므로 LayoutHash 에는 포함하지 않는다.
    ///   (프리팹 색만 바꿨다고 시드 테이블이 무효가 되면 안 되니까)
    /// </summary>
    [Serializable]
    public sealed class PrefabBakeSettings
    {
        [Tooltip("보드 1유닛이 UI 픽셀 몇 개인지. 6유닛 폭 × 100 = 600px 캔버스.")]
        public float pixelsPerUnit = 100f;

        [Tooltip("벽과 칸막이의 두께(px).")]
        public float wallThicknessPx = 10f;

        [Tooltip("비워두면 Unity 기본 UI 스프라이트를 쓴다. 아트가 나오면 여기에 꽂고 다시 구우면 된다.")]
        public Sprite pegSprite;
        public Sprite wallSprite;
        public Sprite ballSprite;
        public Sprite slotSprite;
        public Sprite backgroundSprite;

        public Color pegColor = new Color(0.85f, 0.88f, 1f);
        public Color bumperColor = new Color(1f, 0.55f, 0.85f);

        [Tooltip("specialTag 가 0 이 아닌 핀에 적용된다.")]
        public Color specialColor = new Color(1f, 0.85f, 0.25f);
        public Color wallColor = new Color(0.95f, 0.62f, 0.25f);
        public Color dividerColor = new Color(0.55f, 0.85f, 0.55f);
        public Color ballColor = new Color(1f, 0.95f, 0.45f);
        public Color slotColor = new Color(1f, 1f, 1f, 0.10f);
        public Color backgroundColor = new Color(0.13f, 0.11f, 0.20f);

        [Tooltip("보상 칸마다 빈 RectTransform 을 만든다. 아이콘·수량 텍스트를 여기 자식으로 붙이면 된다.")]
        public bool createSlotAreas = true;

        [Tooltip("판 전체 크기의 배경 Image 를 만든다.")]
        public bool createBackground = true;

        [Tooltip("핀에 맞을 때 크기가 튀는 연출을 켠다.")]
        public bool pegPunchEnabled = true;
        public float pegPunchScale = 1.35f;
        public float pegPunchDuration = 0.18f;
    }

    [CreateAssetMenu(fileName = "PinballBoard", menuName = "Pinball/Board")]
    public sealed class PinballBoard : ScriptableObject
    {
        // ────────────────────────────────────────────────────────────────
        // 좌표계: 보드 좌하단이 (0,0), y 가 위쪽. 단위는 "유닛"이며 미터로 생각하면 된다.
        // 구슬 반지름 0.16 을 기준 잣대로 삼으면 다른 값들의 감이 잡힌다.
        // ────────────────────────────────────────────────────────────────

        [Header("─── 보드 크기 ───")]
        [Tooltip("에디터 프리뷰의 표시 범위. 물리에는 영향 없다(벽은 segments 가 만든다).\n" +
                 "실제 화면 비율과 맞춰두면 판을 배치할 때 감이 잡힌다.")]
        public Vector2 size = new Vector2(6f, 10f);

        [Header("─── 물리 ───")]
        [Tooltip("중력. 클수록 구슬이 빨리 떨어져 연출이 짧아진다.\n" +
                 "현실값(-9.8)은 모바일 연출엔 너무 느리다. -20 ~ -30 권장.\n" +
                 "이 값을 키우면 launchSpeed 도 같이 키워야 구슬이 위까지 올라간다.")]
        public Vector2 gravity = new Vector2(0f, -26f);

        [Tooltip("초당 속도 감쇠율. 이게 0이면 구슬이 영원히 튀어서 maxSteps 까지 가고 폐기된다.\n" +
                 "↑ 올리면: 빨리 잠잠해져 연출이 짧아지지만 핀 충돌 수가 줄어 밋밋해진다.\n" +
                 "↓ 내리면: 오래 튀어 화려하지만 3초를 훌쩍 넘긴다.\n" +
                 "0.3 ~ 0.5 권장.")]
        public float linearDrag = 0.45f;

        [Tooltip("구슬 반지름. 모든 충돌 판정의 기준이자 서브스텝 분할의 기준이다.\n" +
                 "핀 간격보다 충분히 작아야 구슬이 핀밭을 통과해 내려갈 수 있다.")]
        public float ballRadius = 0.16f;

        [Tooltip("핀 기본 반발계수. 1이면 에너지 손실 없이 완전탄성, 0이면 달라붙는다.\n" +
                 "0.86 근처가 '통통 튀는' 느낌. 0.95 이상은 구슬이 날뛰어 폐기율이 치솟는다.")]
        [Range(0f, 1f)] public float pegRestitution = 0.86f;

        [Tooltip("벽 기본 반발계수. 핀보다 낮춰야 벽에 갇혀 진동하는 구슬이 줄어든다.")]
        [Range(0f, 1f)] public float wallRestitution = 0.55f;

        [Tooltip("속도 상한. 수치 폭주로 구슬이 판 밖으로 날아가는 걸 막는 안전장치.\n" +
                 "launchSpeed 보다 넉넉히 크게(1.3배 이상) 잡아라.")]
        public float maxSpeed = 36f;

        [Header("─── 발사 ───")]
        [Tooltip("발사 시작 위치. 보통 발사 레인 안쪽 바닥 근처.\n" +
                 "여기가 catchLineY 보다 아래여도 된다 — armHeightY 가 오판정을 막아준다.")]
        public Vector2 launchPos = new Vector2(5.62f, 0.60f);

        [Tooltip("발사 속도의 중앙값. 구슬이 레인을 타고 올라가 상단 디플렉터까지 닿아야 한다.\n" +
                 "너무 낮으면 레인 안에서만 튀다 멈춰 전 시드가 폐기된다(가장 흔한 실패).\n" +
                 "Quick Test 에서 폐기율 90%↑ 가 나오면 제일 먼저 의심할 값.")]
        public float launchSpeed = 27f;

        [Tooltip("발사 각도(도). 90 = 수직 위. 레인이 우측에 있으면 90 이 정석.")]
        public float launchAngleDeg = 90f;

        [Tooltip("★ 궤적 다양성의 핵심. 시드는 이 범위 안에서 각도를 흔드는 데만 쓰인다.\n" +
                 "물리가 카오스적이라 ±2° 만 흔들어도 착지 칸이 완전히 갈린다.\n" +
                 "↑ 올리면: 다양성 ↑ 이지만 레인 벽에 긁혀 폐기되는 시드가 늘어난다.\n" +
                 "특정 슬롯 풀이 비면 제일 먼저 올려볼 값.")]
        public float angleJitterDeg = 2.5f;

        [Tooltip("발사 속도의 흔들림 폭. 각도 지터와 함께 다양성을 만든다.\n" +
                 "너무 키우면 약하게 발사된 구슬이 레인을 못 넘어 폐기된다.")]
        public float speedJitter = 0.85f;

        [Header("─── 시뮬레이션 ───")]
        [Tooltip("고정 스텝 주파수. Time.deltaTime 은 시뮬에 절대 개입하지 않는다.\n" +
                 "↑ 올리면 정확하지만 베이크가 느려진다. 120 이 정확도/속도 균형점.\n" +
                 "★ 이 값을 바꾸면 기존 시드 테이블이 전부 무효가 된다.")]
        public int stepsPerSecond = 120;

        [Tooltip("이 스텝을 넘기면 '끼임'으로 보고 시드를 폐기한다.\n" +
                 "3000 = 25초. 구슬이 구석에 박혀 무한 진동하는 경우를 잘라낸다.\n" +
                 "폐기율이 높은데 원인을 모르겠으면 이 값을 늘려보고, 그래도 폐기되면 진짜 끼임이다.")]
        public int maxSteps = 3000;

        [Header("─── 보상 칸 ───")]
        [Tooltip("구슬 중심이 이 y 아래로 내려오면 착지로 판정한다.\n" +
                 "★ 바닥 위 정지 높이(바닥 y + ballRadius)보다 충분히 위여야 한다.\n" +
                 "겹치면 구슬이 바닥에 앉은 뒤에도 판정이 안 나서 전부 폐기된다.")]
        public float catchLineY = 0.85f;

        [Tooltip("발사 지점이 catchLineY 아래에 있으므로, 이 높이를 한 번 넘어야 착지 판정이 켜진다.\n" +
                 "이게 없으면 발사 첫 스텝에 곧바로 '착지'로 오판정된다.\n" +
                 "핀밭 하단보다 살짝 아래로 잡으면 된다.")]
        public float armHeightY = 3.0f;

        [Tooltip("보상 칸 영역의 좌/우 경계. 이 밖에서 캐치 라인을 지나면 무효 처리한다.\n" +
                 "발사 레인으로 되돌아간 구슬을 걸러내는 용도다.")]
        public float slotMinX = 0.12f;
        public float slotMaxX = 5.18f;

        [Tooltip("칸 경계 x 좌표(오름차순). n 개면 슬롯은 n+1 개.\n" +
                 "★ 이 값에서 칸막이 벽이 자동 생성되므로 segments 에 따로 넣지 마라.")]
        public float[] dividersX = { 1.13f, 2.14f, 3.16f, 4.17f };

        [Tooltip("자동 생성되는 칸막이 벽의 아래/위 y.\n" +
                 "위쪽을 높일수록 칸이 깊어져 한번 들어간 구슬이 옆칸으로 못 넘어간다.\n" +
                 "낮추면 칸을 넘나드는 니어미스 연출이 늘어난다.")]
        public float dividerBottomY = 0.30f;
        public float dividerTopY = 1.35f;

        [Header("─── 표기 확률 (실제 지급) ───")]
        [Tooltip("★ 실제 지급은 이 확률표를 따른다. 물리 판의 자연 분포와 일치할 필요가 전혀 없다.\n" +
                 "시드 풀은 '그 칸으로 가는 예쁜 궤적'을 공급하는 역할만 한다.\n" +
                 "합이 1.0 이어야 한다. 슬롯 수와 길이가 같아야 한다.")]
        public float[] declaredProbability = { 0.20f, 0.22f, 0.16f, 0.22f, 0.20f };

        [Header("─── 레이아웃 (Sim Lab 의 Edit 탭에서 마우스로 편집) ───")]
        [Tooltip("핀 목록. Sim Lab > Edit 탭에서 드래그로 옮기고 휠로 크기를 조절할 수 있다.")]
        public Peg[] pegs = Array.Empty<Peg>();

        [Tooltip("벽 목록(외벽, 상단 아치, 발사 레인, 디플렉터 등).\n" +
                 "칸막이는 여기 넣지 않는다 — dividersX 에서 자동 생성된다.")]
        public Segment[] segments = Array.Empty<Segment>();

        [Header("─── 특수 핀 ───")]
        [Tooltip("Natural: 물리가 만드는 대로 둔다.\n" +
                 "Declared: 아래 규칙의 목표 확률에 맞춰 시드를 고른다.")]
        public SpecialHitPolicy specialHitPolicy = SpecialHitPolicy.Natural;

        [Tooltip("태그별 목표 적중 확률. '한 번이라도 맞을 확률' 기준이다.\n" +
                 "서로 합이 1이 될 필요가 없다. 각각 독립적인 목표값이다.")]
        public SpecialPegRule[] specialRules = Array.Empty<SpecialPegRule>();

        [Header("─── UI 프리팹 굽기 ───")]
        public PrefabBakeSettings prefabSettings = new PrefabBakeSettings();

        public int SlotCount => dividersX.Length + 1;

        /// <summary>칸막이 벽을 dividersX 로부터 생성한다. 물리와 판정의 desync 를 원천 차단.</summary>
        /// <summary>판에 실제로 쓰인 특수 태그들. 오름차순, 중복 제거. 최대 8개.</summary>
        public List<int> SpecialTags()
        {
            var tags = new List<int>();
            foreach (var p in pegs)
                if (p.specialTag != 0 && !tags.Contains(p.specialTag)) tags.Add(p.specialTag);
            tags.Sort();
            if (tags.Count > MaxSpecialTags) tags.RemoveRange(MaxSpecialTags, tags.Count - MaxSpecialTags);
            return tags;
        }

        /// <summary>비트마스크로 다루므로 태그 개수에 상한이 있다.</summary>
        public const int MaxSpecialTags = 8;

        public IEnumerable<Segment> GenerateDividerSegments()
        {
            foreach (var dx in dividersX)
                yield return new Segment
                {
                    a = new Vector2(dx, dividerBottomY),
                    b = new Vector2(dx, dividerTopY),
                    restitution = 0f
                };
        }

        public BoardData CreateSnapshot()
        {
            var segs = new List<Segment>(segments);
            segs.AddRange(GenerateDividerSegments());

            var tags = SpecialTags();
            var bits = new int[pegs.Length];
            for (int i = 0; i < pegs.Length; i++)
                bits[i] = pegs[i].specialTag == 0 ? -1 : tags.IndexOf(pegs[i].specialTag);

            return new BoardData
            {
                gravity = gravity,
                linearDrag = linearDrag,
                ballRadius = ballRadius,
                pegRestitution = pegRestitution,
                wallRestitution = wallRestitution,
                maxSpeed = maxSpeed,
                dt = 1f / Mathf.Max(1, stepsPerSecond),
                maxSteps = maxSteps,
                launchPos = launchPos,
                launchSpeed = launchSpeed,
                launchAngleDeg = launchAngleDeg,
                angleJitterDeg = angleJitterDeg,
                speedJitter = speedJitter,
                catchLineY = catchLineY,
                armHeightY = armHeightY,
                slotMinX = slotMinX,
                slotMaxX = slotMaxX,
                dividersX = (float[])dividersX.Clone(),
                pegs = (Peg[])pegs.Clone(),
                segments = segs.ToArray(),
                pegTagBit = bits,
                tagByBit = tags.ToArray()
            };
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float f;
            [FieldOffset(0)] public uint u;
        }

        /// <summary>
        /// 레이아웃 지문. 핀 하나를 0.001 옮겨도 값이 바뀐다.
        /// 시드 테이블이 현재 판과 맞는지 검사하는 유일한 근거이므로,
        /// 시뮬 결과에 영향을 주는 필드는 빠짐없이 들어가야 한다.
        /// (size 와 declaredProbability 는 시뮬에 영향이 없으므로 제외)
        /// </summary>
        public string LayoutHash()
        {
            unchecked
            {
                uint h = 2166136261u;

                void F(float v)
                {
                    var fb = new FloatBits { f = v };
                    h = (h ^ fb.u) * 16777619u;
                }

                F(gravity.x); F(gravity.y);
                F(linearDrag); F(ballRadius);
                F(pegRestitution); F(wallRestitution); F(maxSpeed);
                F(stepsPerSecond); F(maxSteps);
                F(launchPos.x); F(launchPos.y);
                F(launchSpeed); F(launchAngleDeg);
                F(angleJitterDeg); F(speedJitter);
                F(catchLineY); F(armHeightY); F(slotMinX); F(slotMaxX);
                F(dividerBottomY); F(dividerTopY);

                foreach (var d in dividersX) F(d);
                foreach (var p in pegs) { F(p.center.x); F(p.center.y); F(p.radius); F(p.restitution); F(p.specialTag); }
                foreach (var s in segments) { F(s.a.x); F(s.a.y); F(s.b.x); F(s.b.y); F(s.restitution); }

                return h.ToString("X8");
            }
        }
    }
}
