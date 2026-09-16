using System.Collections.Generic;
using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// 충돌이 일어난 스텝과 대상. 프리팹의 개별 핀을 정확히 반짝이게 하려면 이게 필요하다.
    /// pegIndex >= 0 이면 핀, -1 이면 벽.
    /// </summary>
    public struct HitEvent
    {
        public int step;
        public int pegIndex;
        public float impact;   // 충돌 직전 법선 속도 크기. 연출 세기에 쓴다.
    }

    public struct SimResult
    {
        public uint seed;
        public int slot;          // -1 = 무효 (끼임 / 슬롯 밖 착지)
        public int steps;
        public int pegHits;
        public int wallHits;
        public int slotSwitches;  // 바닥 근처에서 칸을 넘나든 횟수 = 니어미스 지표
        public int specialHits;   // specialTag != 0 인 핀에 맞은 총 횟수

        /// <summary>어느 태그를 맞았는지. 비트 i = BoardData.tagByBit[i] 태그에 한 번이라도 맞음.</summary>
        public int specialMask;
        public float fun;

        public bool Valid => slot >= 0;
    }

    /// <summary>
    /// 인스턴스 상태가 없다(_board 는 readonly, Run 은 지역변수만 사용).
    /// 따라서 한 인스턴스를 여러 스레드에서 동시에 Run 해도 안전하다.
    /// </summary>
    public sealed class PinballSimulator
    {
        private readonly BoardData _b;

        public PinballSimulator(BoardData board) { _b = board; }

        public BoardData Board => _b;

        /// <param name="trajectory">null 이 아니면 스텝마다 위치를 기록한다. 베이크 때는 반드시 null.</param>
        /// <param name="hits">null 이 아니면 충돌 이벤트를 기록한다. 베이크 때는 반드시 null.</param>
        public SimResult Run(uint seed, List<Vector2> trajectory = null, List<HitEvent> hits = null)
        {
            var rng = new PinballRng(seed);

            float angRad = (_b.launchAngleDeg + rng.Range(-_b.angleJitterDeg, _b.angleJitterDeg)) * Mathf.Deg2Rad;
            float speed0 = _b.launchSpeed + rng.Range(-_b.speedJitter, _b.speedJitter);

            Vector2 pos = _b.launchPos;
            Vector2 vel = new Vector2(Mathf.Cos(angRad) * speed0, Mathf.Sin(angRad) * speed0);

            var r = new SimResult { seed = seed, slot = -1 };
            int lastSlot = int.MinValue;

            // 발사 지점은 캐치 라인보다 아래에 있다.
            // armHeightY 를 한 번 넘기 전까지는 착지 판정을 하지 않는다.
            bool armed = false;

            trajectory?.Add(pos);

            for (int step = 0; step < _b.maxSteps; step++)
            {
                // 터널링 방지용 서브스텝. 개수가 '현재 속도'로만 결정되므로 여전히 결정론적이다.
                float sp = vel.magnitude;
                int sub = 1 + (int)(sp * _b.dt / (_b.ballRadius * 0.5f));
                if (sub > 8) sub = 8;
                float h = _b.dt / sub;

                for (int s = 0; s < sub; s++)
                {
                    vel += _b.gravity * h;
                    vel *= (1f - _b.linearDrag * h);

                    float m = vel.magnitude;
                    if (m > _b.maxSpeed) vel *= (_b.maxSpeed / m);

                    pos += vel * h;

                    ResolvePegs(ref pos, ref vel, ref r, step, hits);
                    ResolveSegments(ref pos, ref vel, ref r, step, hits);
                }

                r.steps = step + 1;
                trajectory?.Add(pos);

                if (!armed && pos.y > _b.armHeightY) armed = true;
                if (!armed) continue;

                // 바닥 위 1.6 유닛 구간에서 칸을 넘나든 횟수를 센다
                if (pos.y > _b.catchLineY && pos.y < _b.catchLineY + 1.6f)
                {
                    int cur = _b.SlotOf(pos.x);
                    if (lastSlot != int.MinValue && cur != lastSlot) r.slotSwitches++;
                    lastSlot = cur;
                }

                if (pos.y <= _b.catchLineY)
                {
                    if (pos.x < _b.slotMinX || pos.x > _b.slotMaxX)
                        return r;                       // 발사 레인 등으로 빠짐 -> 폐기

                    r.slot = _b.SlotOf(pos.x);
                    r.fun = Fun(r);
                    return r;
                }
            }

            return r;                                   // maxSteps 초과 -> 폐기
        }

        /// <summary>연출 점수. 많이 튀고, 마지막에 칸을 넘나들수록 높다.</summary>
        public static float Fun(in SimResult r)
        {
            float hits = Mathf.Clamp01(r.pegHits / 18f);
            float near = Mathf.Clamp01(r.slotSwitches / 4f);
            float len = Mathf.Clamp01(r.steps / 420f);
            return 0.45f * hits + 0.40f * near + 0.15f * len;
        }

        private void ResolvePegs(ref Vector2 pos, ref Vector2 vel, ref SimResult r, int step, List<HitEvent> hits)
        {
            var pegs = _b.pegs;
            for (int i = 0; i < pegs.Length; i++)
            {
                float rSum = _b.ballRadius + pegs[i].radius;
                Vector2 d = pos - pegs[i].center;
                float sq = d.x * d.x + d.y * d.y;
                if (sq >= rSum * rSum) continue;

                float dist = Mathf.Sqrt(sq);
                Vector2 n = dist > 1e-6f ? d / dist : new Vector2(0f, 1f);

                pos = pegs[i].center + n * rSum;        // 파고든 만큼 밀어냄

                // 핀별 반발계수 override (0 이면 보드 기본값)
                float e = pegs[i].restitution > 0f ? pegs[i].restitution : _b.pegRestitution;

                float vn = vel.x * n.x + vel.y * n.y;
                if (vn < 0f)
                {
                    vel -= (1f + e) * vn * n;
                    r.pegHits++;
                    int bit = _b.pegTagBit[i];
                    if (bit >= 0)
                    {
                        r.specialHits++;
                        r.specialMask |= 1 << bit;
                    }
                    hits?.Add(new HitEvent { step = step, pegIndex = i, impact = -vn });
                }
            }
        }

        private void ResolveSegments(ref Vector2 pos, ref Vector2 vel, ref SimResult r, int step, List<HitEvent> hits)
        {
            var segs = _b.segments;
            for (int i = 0; i < segs.Length; i++)
            {
                Vector2 c = ClosestOnSegment(segs[i].a, segs[i].b, pos);
                Vector2 d = pos - c;
                float sq = d.x * d.x + d.y * d.y;
                if (sq >= _b.ballRadius * _b.ballRadius) continue;

                float dist = Mathf.Sqrt(sq);
                Vector2 n;
                if (dist > 1e-6f)
                {
                    n = d / dist;
                }
                else
                {
                    Vector2 ab = segs[i].b - segs[i].a;
                    n = new Vector2(-ab.y, ab.x).normalized;
                }

                pos = c + n * _b.ballRadius;

                float e = segs[i].restitution > 0f ? segs[i].restitution : _b.wallRestitution;

                float vn = vel.x * n.x + vel.y * n.y;
                if (vn < 0f)
                {
                    vel -= (1f + e) * vn * n;
                    r.wallHits++;
                    hits?.Add(new HitEvent { step = step, pegIndex = -1, impact = -vn });
                }
            }
        }

        public static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float l2 = ab.x * ab.x + ab.y * ab.y;
            if (l2 < 1e-8f) return a;

            float t = ((p.x - a.x) * ab.x + (p.y - a.y) * ab.y) / l2;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;

            return new Vector2(a.x + ab.x * t, a.y + ab.y * t);
        }
    }
}
