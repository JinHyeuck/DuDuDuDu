using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pinball.EditorTools
{
    /// <summary>
    /// 핀볼 판 배치의 정본. 아트 PSD(<c>Art/0PSD/핀볼.psd</c>, 1080x1920)의 픽셀 좌표에서
    /// 보드 좌표를 계산해 <c>PinballBoard.asset</c> 에 쓴다. 손으로 고친 배치는 다시 돌리면 이 표로 돌아온다.
    ///
    /// 보드 → 화면 매핑은 <c>x = 175 + 137·u</c>, <c>y = 1324.2 − 92·v</c> (페이지가 판을 (1.37, 0.92) 로 늘려 앉힌다).
    /// 아치형 판 셋(<c>Pinball_Left/Right/Bottom</c>)은 그림 실루엣을 그대로 벽으로 둘러 공이 튕기게 한다.
    /// 물리·핀·벽이 바뀌므로 판 프리팹과 시드표도 같이 다시 굽는다.
    /// </summary>
    public static class PinballPsdLayout
    {
        private const string BoardPath = "Assets/Pinball/PinballBoard.asset";
        private const string SeedTablePath = "Assets/Pinball/PinballSeedTable.asset";
        private const string ViewPrefabPath = "Assets/Pinball/PinballBoard_View.prefab";

        private const int BakeSamples = 200000;
        private const int KeepPerSlot = 2000;

        private const float OriginX = 175f;
        private const float ScaleX = 137f;
        private const float OriginY = 1324.2f;
        private const float ScaleY = 92f;

        private const float StoneRadius = 0.095f;
        private const float RingRadius = 0.2f;
        private const float ChestRadius = 0.3f;

        // 일반 핀(PSD 레이어 1673 외 17개) 중심. 회색 8 + 노랑 9.
        private static readonly Vector2[] Stones =
        {
            new Vector2(399f, 523f), new Vector2(310f, 599f), new Vector2(417f, 657f), new Vector2(529f, 697f),
            new Vector2(293f, 811f), new Vector2(293f, 923f), new Vector2(369f, 1000f), new Vector2(275f, 1072f),
            new Vector2(532f, 475f), new Vector2(532f, 575f), new Vector2(670f, 515f), new Vector2(761f, 599f),
            new Vector2(769f, 923f), new Vector2(708f, 1000f), new Vector2(801f, 1054f), new Vector2(648f, 657f),
            new Vector2(769f, 811f),
        };

        private static readonly Vector2 StarRing = new Vector2(322f, 717f);   // 태그 1 (카피핀)
        private static readonly Vector2 MoonRing = new Vector2(763f, 719f);   // 태그 2 (뽑기권핀)
        private static readonly Vector2 Chest = new Vector2(539f, 990f);      // 태그 3 (대박핀)

        // 아치 판 실루엣(PSD 픽셀). 닫힌 다각형이고 변마다 일반 벽(반발 = 판의 wallRestitution)이 된다.
        // 위 둘이 ⌒, 아래가 ⌣ 로 상자를 둘러싼다. 왼쪽 자리는 Pinball_Right, 오른쪽 자리는 Pinball_Left 그림이고
        // 페이지 굽기(PinballPagePrefabBuilder 의 ChestCage)가 같은 자리에 앉힌다. 그림 알파와의 겹침(IoU)은 0.96.
        private static readonly Vector2[] PlateLeft =
        {
            new Vector2(399.5f, 917f), new Vector2(413.5f, 903f), new Vector2(426.5f, 903f), new Vector2(451.5f, 879f),
            new Vector2(475.5f, 867f), new Vector2(506.5f, 864f), new Vector2(514.5f, 871f), new Vector2(514.5f, 891f),
            new Vector2(506.5f, 899f), new Vector2(493.5f, 901f), new Vector2(461.5f, 919f), new Vector2(430.5f, 955f),
            new Vector2(411.5f, 955f), new Vector2(399.5f, 943f),
        };

        private static readonly Vector2[] PlateRight =
        {
            new Vector2(571.5f, 873f), new Vector2(579.5f, 865f), new Vector2(598.5f, 865f), new Vector2(610.5f, 869f),
            new Vector2(634.5f, 881f), new Vector2(659.5f, 905f), new Vector2(672.5f, 905f), new Vector2(686.5f, 919f),
            new Vector2(686.5f, 945f), new Vector2(674.5f, 957f), new Vector2(655.5f, 957f), new Vector2(624.5f, 921f),
            new Vector2(592.5f, 903f), new Vector2(579.5f, 901f), new Vector2(571.5f, 893f),
        };

        private static readonly Vector2[] PlateBottom =
        {
            new Vector2(431.5f, 1070f), new Vector2(445.5f, 1058f), new Vector2(460.5f, 1058f), new Vector2(485.5f, 1080f),
            new Vector2(517.5f, 1090f), new Vector2(558.5f, 1090f), new Vector2(574.5f, 1086f), new Vector2(592.5f, 1078f),
            new Vector2(615.5f, 1058f), new Vector2(630.5f, 1058f), new Vector2(644.5f, 1070f), new Vector2(644.5f, 1098f),
            new Vector2(632.5f, 1110f), new Vector2(615.5f, 1110f), new Vector2(600.5f, 1120f), new Vector2(578.5f, 1128f),
            new Vector2(562.5f, 1132f), new Vector2(513.5f, 1132f), new Vector2(475.5f, 1120f), new Vector2(460.5f, 1110f),
            new Vector2(443.5f, 1110f), new Vector2(431.5f, 1098f),
        };

        // 위쪽 둥근 천장(14조각). 보드 좌표 그대로다.
        private static readonly Vector2[] Cap =
        {
            new Vector2(0.05f, 8.6f), new Vector2(0.124f, 8.8893f), new Vector2(0.3421f, 9.164f),
            new Vector2(0.6936f, 9.4105f), new Vector2(1.1607f, 9.6164f), new Vector2(1.72f, 9.7713f),
            new Vector2(2.3436f, 9.8674f), new Vector2(3f, 9.9f), new Vector2(3.6564f, 9.8674f),
            new Vector2(4.28f, 9.7713f), new Vector2(4.8393f, 9.6164f), new Vector2(5.3064f, 9.4105f),
            new Vector2(5.6579f, 9.164f), new Vector2(5.876f, 8.8893f), new Vector2(5.95f, 8.6f),
        };

        /// <summary>PSD 픽셀 → 보드 좌표.</summary>
        public static Vector2 ToBoard(Vector2 psd)
        {
            return new Vector2((psd.x - OriginX) / ScaleX, (OriginY - psd.y) / ScaleY);
        }

        [MenuItem("Tools/Pinball/PSD 배치 적용 + 프리팹·시드표 다시 굽기")]
        public static void ApplyAndRebake()
        {
            var board = AssetDatabase.LoadAssetAtPath<PinballBoard>(BoardPath);
            var table = AssetDatabase.LoadAssetAtPath<SeedTable>(SeedTablePath);
            if (board == null || table == null)
            {
                Debug.LogError("[핀볼 배치] 판 또는 시드표를 못 찾았다: " + BoardPath + " / " + SeedTablePath);
                return;
            }

            Apply(board);
            PinballPrefabBaker.Bake(board, ViewPrefabPath);

            BakeReport report = SeedTableBaker.Bake(table, BakeSamples, KeepPerSlot);
            int mismatch = SeedTableBaker.Verify(table, 200, out string message);

            Debug.Log("[핀볼 배치] 적용 완료. 핀 " + board.pegs.Length + " · 벽 " + board.segments.Length +
                      " · 해시 " + board.LayoutHash() + "\n" +
                      "  시드표 " + report.sampled + " 표본 중 폐기 " + report.discarded +
                      " (" + (100.0 * report.discarded / report.sampled).ToString("0.0") + "%), 칸별 풀 " +
                      string.Join("/", report.perSlot) + ", " + report.seconds.ToString("0.0") + "초\n" +
                      "  검증 " + (mismatch == 0 ? "OK" : "불일치 " + mismatch) + " — " + message);
        }

        /// <summary>판 에셋의 핀·벽만 PSD 표로 바꾼다. 물리 상수와 표기 확률은 건드리지 않는다.</summary>
        public static void Apply(PinballBoard board)
        {
            var pegs = new List<Peg>();
            foreach (Vector2 stone in Stones)
                pegs.Add(new Peg { center = ToBoard(stone), radius = StoneRadius, restitution = 0f, specialTag = 0 });

            pegs.Add(new Peg { center = ToBoard(StarRing), radius = RingRadius, restitution = 0f, specialTag = 1 });
            pegs.Add(new Peg { center = ToBoard(MoonRing), radius = RingRadius, restitution = 0f, specialTag = 2 });
            pegs.Add(new Peg { center = ToBoard(Chest), radius = ChestRadius, restitution = 0f, specialTag = 3 });

            var segments = new List<Segment>
            {
                Wall(0.05f, 0.3f, 0.05f, 8.6f),       // 왼쪽 벽
                Wall(5.95f, 0.3f, 5.95f, 8.6f),       // 오른쪽 벽
                Wall(0.05f, 0.3f, 5.95f, 0.3f),       // 바닥
                Wall(5.25f, 0.45f, 5.25f, 7.4f),      // 발사 레인 벽
                Wall(5.95f, 7.4f, 4.7f, 9.0f),        // 디플렉터: 레인 윗끝에서 판 안쪽으로 공을 꺾는다
            };

            for (int i = 0; i < Cap.Length - 1; i++)
                segments.Add(Wall(Cap[i].x, Cap[i].y, Cap[i + 1].x, Cap[i + 1].y));

            AddPlate(segments, PlateLeft);
            AddPlate(segments, PlateRight);
            AddPlate(segments, PlateBottom);

            Undo.RecordObject(board, "PSD 핀볼 배치");
            board.pegs = pegs.ToArray();
            board.segments = segments.ToArray();
            EditorUtility.SetDirty(board);
            AssetDatabase.SaveAssets();
        }

        private static Segment Wall(float ax, float ay, float bx, float by)
        {
            return new Segment { a = new Vector2(ax, ay), b = new Vector2(bx, by), restitution = 0f };
        }

        private static void AddPlate(List<Segment> into, Vector2[] outlinePsd)
        {
            for (int i = 0; i < outlinePsd.Length; i++)
            {
                Vector2 a = ToBoard(outlinePsd[i]);
                Vector2 b = ToBoard(outlinePsd[(i + 1) % outlinePsd.Length]);
                into.Add(Wall(a.x, a.y, b.x, b.y));
            }
        }
    }
}
