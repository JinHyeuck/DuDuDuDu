using System;
using System.Collections.Generic;

namespace OJ.EventMinigame
{
    /// <summary>보물찾기 한 단계(3스테이지)의 판 규격.</summary>
    public sealed class TreasureTier
    {
        public readonly int Width;
        public readonly int Height;

        /// <summary>숨겨진 보물들의 모양 번호(<see cref="TreasureHuntRules.ShapeCount"/> 미만).</summary>
        public readonly int[] Shapes;

        /// <summary>칸 하나를 팔 때 드는 곡괭이 수.</summary>
        public readonly int DigCost;

        /// <summary>
        /// 중간 실력 플레이어가 판 하나를 깨는 데 파는 칸 수의 10배. 시뮬레이션(적중하면 주변을 파고, 아니면 바둑판으로 훑기)에서
        /// 쟀다 — 판·보물 세트를 바꾸면 다시 재야 한다. 보상 가치가 이 값에서 나온다.
        /// </summary>
        public readonly int ExpectedDigsTenths;

        public TreasureTier(int width, int height, int[] shapes, int digCost, int expectedDigsTenths)
        {
            Width = width;
            Height = height;
            Shapes = shapes;
            DigCost = digCost;
            ExpectedDigsTenths = expectedDigsTenths;
        }
    }

    /// <summary>숨겨진 보물이 놓인 판. 시드로 다시 만들어지므로 저장은 시드 + 판 번호면 된다.</summary>
    public sealed class TreasureBoard
    {
        public readonly int Width;
        public readonly int Height;

        /// <summary>칸마다 그 칸을 차지한 보물 번호. 빈칸은 -1.</summary>
        public readonly int[] Owner;

        /// <summary>보물 번호 → 모양 번호.</summary>
        public readonly int[] TreasureShapes;

        private readonly int[] cellCounts;

        public TreasureBoard(int width, int height, int[] owner, int[] treasureShapes)
        {
            Width = width;
            Height = height;
            Owner = owner;
            TreasureShapes = treasureShapes;

            cellCounts = new int[treasureShapes.Length];
            for (int i = 0; i < owner.Length; i++)
            {
                if (owner[i] >= 0)
                    cellCounts[owner[i]]++;
            }
        }

        public int TreasureCount
        {
            get { return TreasureShapes.Length; }
        }

        public int CellCount(int treasure)
        {
            return cellCounts[treasure];
        }
    }

    /// <summary>한 칸을 판 결과.</summary>
    public readonly struct DigResult
    {
        /// <summary>false 면 범위 밖이거나 이미 판 칸이라 아무 일도 없었다(곡괭이도 쓰지 않는다).</summary>
        public readonly bool Valid;

        /// <summary>그 칸의 보물 번호. 빈칸이면 -1.</summary>
        public readonly int Treasure;

        /// <summary>이 칸으로 그 보물이 전부 드러났는가.</summary>
        public readonly bool TreasureCompleted;

        /// <summary>이 칸으로 판의 모든 보물을 찾았는가(스테이지 클리어).</summary>
        public readonly bool StageCleared;

        public DigResult(bool valid, int treasure, bool treasureCompleted, bool stageCleared)
        {
            Valid = valid;
            Treasure = treasure;
            TreasureCompleted = treasureCompleted;
            StageCleared = stageCleared;
        }

        public bool IsHit
        {
            get { return Valid && Treasure >= 0; }
        }
    }

    /// <summary>
    /// 보물찾기 규칙. 판 생성(시드), 칸 파기, 스테이지·열쇠·상자, 보상 가치를 전부 static 으로 둔다.
    ///
    /// 설계 기록: <c>plans/3-purrfect-seahorse.md</c> "보물찾기 세부 설계". 남은 보물의 <b>실루엣 목록은 화면에 보여 주고</b>
    /// 확률 표시는 주지 않는다. 보상은 스테이지 클리어당이라 중간 실력 기준 기대 소모량 × 200원에 맞춘다.
    /// </summary>
    public static class TreasureHuntRules
    {
        public const int ShapeCount = 6;
        public const int StagesPerTier = 3;
        public const int StagesPerKey = 3;
        public const int KeysPerChest = 4;
        public const int StagesPerChest = StagesPerKey * KeysPerChest;

        /// <summary>이벤트가 끝나는 순간 버려지는 진행(평균 약 반 스테이지) 보정. 보상에 곱한다(%).</summary>
        public const int LossCompensationPercent = 107;

        /// <summary>보상 가치 중 스테이지 클리어에 주는 몫(%). 나머지는 상자에 몰아 준다.</summary>
        public const int StageSharePercent = 70;

        public const int ChestSharePercent = 30;

        // 보물 모양: 한 장의 그림으로 이어진 직사각형(칸 좌표 x,y 를 이어 붙인 목록).
        // 0 1×1 / 1 2×1 / 2 2×2 / 3 3×1 / 4 3×2 / 5 3×3. 판에는 가로·세로 어느 쪽으로든 놓인다.
        private static readonly int[][] ShapeCells =
        {
            Rectangle(1, 1),
            Rectangle(2, 1),
            Rectangle(2, 2),
            Rectangle(3, 1),
            Rectangle(3, 2),
            Rectangle(3, 3),
        };

        /// <summary>모양 번호 → 방향(회전·뒤집기, 중복 제거) → 칸 좌표 목록.</summary>
        private static readonly int[][][] Orientations = BuildOrientations();

        // 판마다 보물 칸 수 합계를 옛 구성(도미노·트로미노·테트로미노 모양)과 거의 맞춰 두었다. 기대 소모(마지막 값)는
        // 적중하면 주변을 파고 아니면 바둑판으로 훑는 시뮬레이션(판마다 4,000회)에서 새로 쟀다.
        public static readonly TreasureTier[] Tiers =
        {
            new TreasureTier(5, 5, new[] { 1, 3 }, 1, 135),
            new TreasureTier(6, 5, new[] { 1, 3, 3 }, 1, 183),
            new TreasureTier(6, 6, new[] { 1, 3, 3 }, 2, 208),
            new TreasureTier(6, 6, new[] { 1, 3, 3, 2 }, 2, 241),
            new TreasureTier(7, 6, new[] { 3, 2, 4, 1 }, 3, 281),
            new TreasureTier(7, 7, new[] { 0, 3, 2, 2, 4 }, 3, 371),
            new TreasureTier(8, 7, new[] { 3, 3, 2, 2, 4, 1 }, 4, 400),
            new TreasureTier(8, 8, new[] { 5, 4, 2, 3, 1, 0, 0 }, 4, 529),
        };

        // ── 스테이지 · 열쇠 · 상자 ────────────────────────────────────────

        /// <summary>스테이지(1부터)가 속한 단계(1~8). 8단계 뒤로는 8단계가 계속 반복된다.</summary>
        public static int TierOfStage(int stage)
        {
            int s = stage < 1 ? 1 : stage;
            int tier = (s - 1) / StagesPerTier + 1;
            return tier > Tiers.Length ? Tiers.Length : tier;
        }

        public static TreasureTier TierData(int stage)
        {
            return Tiers[TierOfStage(stage) - 1];
        }

        public static int DigCost(int stage)
        {
            return TierData(stage).DigCost;
        }

        /// <summary>이 스테이지를 중간 실력으로 깰 때 쓰는 곡괭이 수의 10배.</summary>
        public static long ExpectedPicksTenths(int stage)
        {
            TreasureTier tier = TierData(stage);
            return (long)tier.ExpectedDigsTenths * tier.DigCost;
        }

        /// <summary>1 스테이지부터 <paramref name="throughStage"/> 까지 기대 곡괭이 소모의 10배.</summary>
        public static long CumulativePicksTenths(int throughStage)
        {
            long sum = 0;
            for (int s = 1; s <= throughStage; s++)
                sum += ExpectedPicksTenths(s);
            return sum;
        }

        /// <summary>이 스테이지를 깨면 열쇠를 얻는가(3스테이지마다).</summary>
        public static bool GrantsKey(int stage)
        {
            return stage >= 1 && stage % StagesPerKey == 0;
        }

        /// <summary>이 스테이지를 깨면 상자가 열리는가(열쇠 4개 = 12스테이지마다).</summary>
        public static bool OpensChest(int stage)
        {
            return stage >= 1 && stage % StagesPerChest == 0;
        }

        /// <summary>그 스테이지에서 열리는 상자 번호(1부터). 상자가 없으면 0.</summary>
        public static int ChestIndexAt(int stage)
        {
            return OpensChest(stage) ? stage / StagesPerChest : 0;
        }

        // ── 보상 ────────────────────────────────────────────────────────

        /// <summary>
        /// 스테이지 클리어 보상(핀볼·골드·일반 뽑기권). 가치 = 기대 곡괭이 소모 × 200원 × 보정 × 스테이지 몫.
        /// </summary>
        public static EventBundle StageReward(int stage)
        {
            long budget = ExpectedPicksTenths(stage) * EventLedger.ReturnPerCurrency
                          * LossCompensationPercent * StageSharePercent / (10L * 100 * 100);

            // 일반 뽑기권을 3스테이지 중 둘째·셋째에 넣는다. 나머지는 핀볼, 골드 약간.
            int mod = stage % 3;
            if (mod == 1)
                return EventBundle.Compose(budget, 0, 0, 0, 0, 0, 0, 300);

            return EventBundle.Compose(budget, 0, 0, 0, 0, 300, 0, 100);
        }

        /// <summary>상자 보상(최고급 뽑기권·카피 중심). 가치 = 그 상자 구간 12스테이지 기대 소모 합 × 200원 × 보정 × 상자 몫.</summary>
        public static EventBundle ChestReward(int chestIndex)
        {
            int index = chestIndex < 1 ? 1 : chestIndex;
            int first = (index - 1) * StagesPerChest + 1;

            long tenths = 0;
            for (int s = first; s < first + StagesPerChest; s++)
                tenths += ExpectedPicksTenths(s);

            long budget = tenths * EventLedger.ReturnPerCurrency
                          * LossCompensationPercent * ChestSharePercent / (10L * 100 * 100);

            return EventBundle.Compose(budget, 300, 0, 250, 250, 0, 0, 0);
        }

        // ── 판 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 시드로 판을 만든다(같은 시드·단계는 늘 같은 판). 보물은 겹치지 않게, 방향과 위치는 시드로 정한다.
        /// </summary>
        public static TreasureBoard Generate(int tier, int seed)
        {
            int index = tier < 1 ? 0 : (tier > Tiers.Length ? Tiers.Length - 1 : tier - 1);
            TreasureTier data = Tiers[index];
            var rng = new EventSeededRandom(seed);

            // 보물이 안 들어가는 배치가 나오면 다시 굴린다. 이 규격에서는 사실상 첫 시도에 끝난다.
            for (int attempt = 0; attempt < 64; attempt++)
            {
                int[] owner;
                if (TryPlace(data, ref rng, false, out owner))
                    return new TreasureBoard(data.Width, data.Height, owner, data.Shapes);
            }

            // 도달하지 않는 경로지만 판이 없으면 이벤트가 멈춘다 — 앞에서부터 채우는 고정 배치로 닫는다.
            int[] fallback;
            TryPlace(data, ref rng, true, out fallback);
            return new TreasureBoard(data.Width, data.Height, fallback, data.Shapes);
        }

        /// <summary>칸을 판다. 이미 판 칸·범위 밖이면 <see cref="DigResult.Valid"/> 가 false 이고 상태는 그대로다.</summary>
        public static DigResult Dig(TreasureBoard board, bool[] revealed, int x, int y)
        {
            if (board == null || revealed == null || revealed.Length != board.Width * board.Height)
                return new DigResult(false, -1, false, false);

            if (x < 0 || y < 0 || x >= board.Width || y >= board.Height)
                return new DigResult(false, -1, false, false);

            int idx = y * board.Width + x;
            if (revealed[idx])
                return new DigResult(false, -1, false, false);

            revealed[idx] = true;
            int treasure = board.Owner[idx];
            if (treasure < 0)
                return new DigResult(true, -1, false, false);

            bool completed = RevealedCount(board, revealed, treasure) >= board.CellCount(treasure);
            bool cleared = completed && AllFound(board, revealed);
            return new DigResult(true, treasure, completed, cleared);
        }

        public static bool IsTreasureFound(TreasureBoard board, bool[] revealed, int treasure)
        {
            return RevealedCount(board, revealed, treasure) >= board.CellCount(treasure);
        }

        public static bool AllFound(TreasureBoard board, bool[] revealed)
        {
            for (int t = 0; t < board.TreasureCount; t++)
            {
                if (!IsTreasureFound(board, revealed, t))
                    return false;
            }

            return true;
        }

        /// <summary>아직 다 찾지 못한 보물의 모양 번호 목록(실루엣 표시용).</summary>
        public static List<int> RemainingShapes(TreasureBoard board, bool[] revealed)
        {
            var list = new List<int>();
            for (int t = 0; t < board.TreasureCount; t++)
            {
                if (!IsTreasureFound(board, revealed, t))
                    list.Add(board.TreasureShapes[t]);
            }

            return list;
        }

        /// <summary>모양의 기본 방향 칸 좌표(x,y 를 이어 붙인 목록). 실루엣을 그릴 때 쓴다.</summary>
        public static int[] ShapeSilhouette(int shape)
        {
            return Orientations[Math.Max(0, Math.Min(ShapeCount - 1, shape))][0];
        }

        /// <summary>
        /// 판에 놓인 보물 하나가 차지한 직사각형(왼쪽 위 칸 좌표와 가로·세로 칸 수). 보물이 한 장의 그림이라
        /// 화면이 그림을 이 자리에 통째로 깐다.
        /// </summary>
        public static void TreasureRect(TreasureBoard board, int treasure, out int x, out int y, out int width, out int height)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = -1;
            int maxY = -1;

            for (int i = 0; i < board.Owner.Length; i++)
            {
                if (board.Owner[i] != treasure)
                    continue;

                int cx = i % board.Width;
                int cy = i / board.Width;
                if (cx < minX) minX = cx;
                if (cy < minY) minY = cy;
                if (cx > maxX) maxX = cx;
                if (cy > maxY) maxY = cy;
            }

            if (maxX < 0)
            {
                x = 0;
                y = 0;
                width = 0;
                height = 0;
                return;
            }

            x = minX;
            y = minY;
            width = maxX - minX + 1;
            height = maxY - minY + 1;
        }

        /// <summary>가로 <paramref name="width"/> × 세로 <paramref name="height"/> 직사각형의 칸 좌표 목록.</summary>
        private static int[] Rectangle(int width, int height)
        {
            var cells = new int[width * height * 2];
            int n = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    cells[n++] = x;
                    cells[n++] = y;
                }
            }

            return cells;
        }

        private static int RevealedCount(TreasureBoard board, bool[] revealed, int treasure)
        {
            int count = 0;
            for (int i = 0; i < board.Owner.Length; i++)
            {
                if (board.Owner[i] == treasure && revealed[i])
                    count++;
            }

            return count;
        }

        private static bool TryPlace(TreasureTier data, ref EventSeededRandom rng, bool firstFit, out int[] owner)
        {
            int w = data.Width;
            int h = data.Height;
            owner = new int[w * h];
            for (int i = 0; i < owner.Length; i++)
                owner[i] = -1;

            for (int treasure = 0; treasure < data.Shapes.Length; treasure++)
            {
                var candidates = new List<int[]>();
                int[][] orientations = Orientations[data.Shapes[treasure]];

                for (int o = 0; o < orientations.Length; o++)
                {
                    int[] cells = orientations[o];
                    int maxX = 0;
                    int maxY = 0;
                    for (int c = 0; c < cells.Length; c += 2)
                    {
                        if (cells[c] > maxX) maxX = cells[c];
                        if (cells[c + 1] > maxY) maxY = cells[c + 1];
                    }

                    for (int ox = 0; ox + maxX < w; ox++)
                    {
                        for (int oy = 0; oy + maxY < h; oy++)
                        {
                            int[] placed = new int[cells.Length / 2];
                            bool free = true;
                            for (int c = 0; c < cells.Length; c += 2)
                            {
                                int cell = (oy + cells[c + 1]) * w + (ox + cells[c]);
                                if (owner[cell] >= 0)
                                {
                                    free = false;
                                    break;
                                }

                                placed[c / 2] = cell;
                            }

                            if (free)
                                candidates.Add(placed);
                        }
                    }
                }

                if (candidates.Count == 0)
                    return false;

                int[] pick = candidates[firstFit ? 0 : rng.NextInt(candidates.Count)];
                for (int i = 0; i < pick.Length; i++)
                    owner[pick[i]] = treasure;
            }

            return true;
        }

        private static int[][][] BuildOrientations()
        {
            var result = new int[ShapeCells.Length][][];
            for (int s = 0; s < ShapeCells.Length; s++)
            {
                var seen = new HashSet<string>();
                var list = new List<int[]>();
                int[] cur = ShapeCells[s];

                for (int rotation = 0; rotation < 4; rotation++)
                {
                    // 90도 회전: (x, y) -> (y, -x)
                    var rotated = new int[cur.Length];
                    for (int i = 0; i < cur.Length; i += 2)
                    {
                        rotated[i] = cur[i + 1];
                        rotated[i + 1] = -cur[i];
                    }

                    cur = rotated;

                    for (int flip = 0; flip < 2; flip++)
                    {
                        var variant = new int[cur.Length];
                        for (int i = 0; i < cur.Length; i += 2)
                        {
                            variant[i] = flip == 1 ? -cur[i] : cur[i];
                            variant[i + 1] = cur[i + 1];
                        }

                        int[] normalized = Normalize(variant);
                        string key = string.Join(",", normalized);
                        if (seen.Add(key))
                            list.Add(normalized);
                    }
                }

                result[s] = list.ToArray();
            }

            return result;
        }

        /// <summary>좌표를 (0,0) 쪽으로 옮기고 x, y 순으로 정렬한다(같은 모양이 같은 키가 되게).</summary>
        private static int[] Normalize(int[] cells)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            for (int i = 0; i < cells.Length; i += 2)
            {
                if (cells[i] < minX) minX = cells[i];
                if (cells[i + 1] < minY) minY = cells[i + 1];
            }

            var points = new List<long>();
            for (int i = 0; i < cells.Length; i += 2)
                points.Add((long)(cells[i] - minX) * 1000 + (cells[i + 1] - minY));

            points.Sort();

            var result = new int[cells.Length];
            for (int i = 0; i < points.Count; i++)
            {
                result[i * 2] = (int)(points[i] / 1000);
                result[i * 2 + 1] = (int)(points[i] % 1000);
            }

            return result;
        }
    }
}
