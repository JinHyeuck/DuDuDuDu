namespace OJ.EditorTools
{
    /// <summary>
    /// 로비·옆 메뉴·가방 시안에서 잰 수치. 전부 시안 좌표(1080x1920, 좌상단 원점, 아래가 +)다.
    ///
    /// 굽는 코드에 숫자를 흩어 두면 시안이 바뀔 때 어디를 고칠지 못 찾는다 — 여기서만 고친다.
    /// 출처: Art/Layout/{Main_AddNew_LAyout, Main_Menu_Layout, Item_Layout, Item_Prs_Layout}.png,
    /// Art/0PSD/{로비화면, 가방아이템}.psd.
    /// </summary>
    internal static class LobbyPsdLayout
    {
        /// <summary>옆 메뉴 (Main_Menu_Layout / 로비화면.psd). 머리글 없이 네 줄과 닫기 버튼뿐이다.</summary>
        public static class Menu
        {
            // 왼쪽 어두운 판. 위는 HUD(아바타·재화)가 시안에서도 이 위에 얹히므로 HUD 아래에서 시작한다.
            public const float SlabX = 0f, SlabY = 150f, SlabW = 459f, SlabH = 1559f;

            // 판의 오른쪽 끝선: 밝은 줄 5px 다음 검은 줄 5px.
            public const float EdgeX = 459f, EdgeLightW = 5f, EdgeDarkW = 5f;

            // 줄들이 앉는 안쪽 판 #0d0d19
            public const float PanelX = 23f, PanelY = 203f, PanelW = 413f, PanelH = 1353f;

            // Menu_Btn x4. 줄 하나 398x184, 세로로 네 줄.
            public const float RowX = 29f, RowW = 398f, RowH = 184f;
            public static readonly float[] RowY = { 217f, 402f, 586f, 771f };

            // 줄 왼쪽 어두운 아이콘 칸(138x128 #4e4f6a). 줄 위에서 20px 아래.
            public const float IconBoxX = 46f, IconBoxDown = 20f, IconBoxW = 138f, IconBoxH = 128f;

            // 아이콘은 칸 중심에 놓고 3배(우편만 4배). 글자는 줄 위에서 86px 아래, 가로 중심 317.5.
            public const float LabelCx = 317.5f, LabelDown = 86f, LabelSize = 45f;

            // Icon_NewPoint 42x42. 줄 위에서 3px 위, x=390.
            public const float DotX = 390f, DotUp = 3f, DotSize = 42f;

            // Icon_Cancle 116x116
            public const float CloseX = 164f, CloseY = 1573f, CloseSize = 116f;

            // 판 밖 색
            public const int SlabColor = 0x1d1c30, EdgeLightColor = 0x3e3f68, PanelColor = 0x0d0d19, IconBoxColor = 0x4e4f6a;
        }

        /// <summary>로비 화면 (Main_AddNew_LAyout / 로비화면.psd '로비'). 그림은 보이는 사각형 기준이다.</summary>
        public static class Lobby
        {
            // ── 위쪽 HUD
            // 프로필 틀 Merge_Bg x3 (보이는 138x138), 박쥐는 틀 안에서 살짝 오른쪽 아래.
            public const float ProfileX = 18f, ProfileY = 11f, ProfileSize = 138f;
            public const float BatCx = 89f, BatCy = 81f, BatW = 112f, BatH = 102f;

            // 틀 안쪽은 새까만 칸, 틀 자체는 회보라로 물들인다.
            public const float BatBackCx = 87f, BatBackCy = 80f, BatBackSize = 114f;
            public const int ProfileTint = 0x8c8aa1;

            // 글자는 왼쪽 끝(x)과 세로 중심(cy). 닉네임 35pt, 전투력 줄 30pt.
            public const float NickX = 173f, NickCy = 63f, NickSize = 30f;
            public const float PowerIconCx = 187f, PowerIconCy = 94.5f;
            public const float ApkX = 204f, ApkCy = 96.5f, PowerX = 284f, PowerCy = 97.5f, PowerSize = 28f;

            // 재화 칸 Merge_Bg x2 (보이는 203x56). 아이콘은 칸 왼쪽 끝에서 14px 안, 1.8배. 숫자는 오른쪽 끝에서 32px 안, 30pt.
            public static readonly float[] ChipX = { 412f, 633f, 854f };
            public const float ChipY = 52f, ChipW = 203f, ChipH = 56f;
            public const float ChipIconDx = 14f, ChipIconCy = 78.5f, ChipIconScale = 1.8f;
            public const float ChipTextRightPad = 32f, ChipTextCy = 82.5f, ChipTextSize = 25f;

            // 메뉴 버튼 Btn_Gray x3 (보이는 140x126). 아이콘은 보이는 칸보다 6px 위.
            public const float MenuX = 21f, MenuY = 197f, MenuW = 140f, MenuH = 126f;
            public const float MenuIconCx = 91f, MenuIconCy = 254f;
            public const float MenuDotCx = 153f, MenuDotCy = 205f;

            // ── 왼쪽 열: 아이콘은 2배, 글자는 30pt
            public const float LeftIconScale = 2f, LeftLabelSize = 30f;
            public const float MembershipIconCx = 93f, MembershipIconCy = 412f, MembershipLabelCx = 83.5f, MembershipLabelCy = 472f;
            public const float SevenIconCx = 84f, SevenIconCy = 595f, SevenLabelCx = 86f, SevenLabelCy = 657f;

            // 작은 칸(퀘스트·시너지): 시안에 없는 입구라 메뉴 버튼과 같은 크기로 7일 아래에 쌓는다.
            public const float TileX = 21f, TileW = 140f, TileH = 126f, QuestTileY = 730f, SynergyTileY = 880f;

            // ── 시즌 패스 판 Pass_premium_Btn x3 (보이는 701x102)
            public const float PassX = 201f, PassY = 432f, PassW = 701f, PassH = 102f;
            public const float PassTitleCx = 411.5f, PassTitleCy = 484f, PassTitleSize = 40f;
            public const float PassLevelCx = 275f, PassLevelCy = 484.5f, PassLevelSize = 40f;
            public const float PassRemainX = 644f, PassRemainCy = 483f, PassRemainSize = 35f;
            public const float PassClockCx = 603f, PassClockCy = 479.5f;
            public const float PassDotCx = 882f, PassDotCy = 446f;
            public const int PassLevelColor = 0x00ff5a, PassRemainColor = 0x8f3200;

            // ── 보상 띠 Merge_Bg x3 (보이는 612x97). 아이콘 x3, 글자 25pt
            public const float StripX = 233f, StripY = 1291f, StripW = 612f, StripH = 97f;
            public const float WaveIconCx = 344f, WaveIconCy = 1279f, WaveLabelCx = 340f, WaveLabelCy = 1352f;
            public const float IdleIconCx = 543f, IdleIconCy = 1275f, IdleLabelCx = 544f, IdleLabelCy = 1356.5f;
            public const float StarIconCx = 719f, StarIconCy = 1271.5f, StarLabelCx = 716.5f, StarLabelCy = 1352f;
            public const float StripLabelSize = 25f;

            // ── 아래 한 줄 (y 1454, 높이 145): 뽑기 칸 · 소탕 · 입장 · 무한의 탑 칸. 버튼은 전부 x3.
            public const float RowY = 1454f, RowH = 145f, ButtonW = 290f;
            public const float PinballTileX = -78f, SweepX = 234f, GoX = 553f, TowerTileX = 869f, TowerTileY = 1453f;
            public const float SweepLabelCx = 377f, SweepLabelCy = 1517.5f, SweepLabelSize = 40f;
            public const float SweepStateCx = 379f, SweepStateCy = 1568f, SweepStateSize = 24f;
            public const float GoLabelCx = 698.5f, GoLabelCy = 1496f, GoLabelSize = 35f;
            public const float CostPillX = 573f, CostPillY = 1523f, CostPillW = 251f, CostPillH = 39f, CostPillAlpha = 0.3f;
            // 시안 메모는 "1.8배"지만 잰 값은 고기 몸통 27x30 = 원본(23x25)의 1.2배다. 메모가 아니라 픽셀을 따른다.
            public const float CostIconCx = 673f, CostIconCy = 1544f, CostIconScale = 1.2f;
            public const float CostTextCx = 713f, CostTextCy = 1545.5f, CostTextSize = 35f;

            // 큰 칸 안: 아이콘 x2, 이름 35pt
            public const float PinballIconCx = 92.5f, PinballIconCy = 1472f, PinballTitleCx = 89.5f, TileTitleCy = 1540f;
            public const float TowerIconCx = 984.5f, TowerIconCy = 1471f, TowerTitleCx = 984.5f;
            public const float TileIconScale = 2f, TileTitleSize = 35f;
            public const float TowerBadgeCx = 1045f, TowerBadgeCy = 1470f;
        }

        /// <summary>가방 창 (Item_Layout / 가방아이템.psd).</summary>
        public static class Bag
        {
            // Ui_Popup_Bg x4 의 보이는 사각형
            public const float PanelX = 75f, PanelY = 268f, PanelW = 924f, PanelH = 1339f;

            // Ui_Popup_SmallBox x4 #636481 의 보이는 사각형
            public const float InnerX = 109f, InnerY = 386f, InnerW = 857f, InnerH = 1181f;

            public const float TitleCx = 541f, TitleCy = 333f, TitleSize = 45f;

            // FromPsd_CloseButton 112x111
            public const float CloseCx = 911f, CloseCy = 284.5f, CloseW = 112f, CloseH = 111f;

            // 칸 격자: 칸 188, 4열. 첫 칸 (133, 408), 세로 피치 208, 가로 간격 18.67.
            public const float SlotSize = 188f;
            public const int Columns = 4;
            public const float PadLeft = 24f, PadTop = 16f, PadRight = 25f, PadBottom = 16f;
            public const float SpacingX = 18.67f, SpacingY = 20f;

            // 스크롤 창. 안쪽 판 위아래에서 한 줄 여백씩 뺀 자리.
            public const float ViewportTop = 392f, ViewportBottom = 1561f;

            // 칸 안: 그림은 3배(64px -> 192), 칸 중심보다 22px 위. 수량 글자는 53px 아래.
            public const float IconSize = 192f, IconUp = 22f;
            public const float CountDown = 53f, CountSize = 35f, CountMin = 20f;
        }

        /// <summary>재화를 눌렀을 때 뜨는 작은 창 (Item_Prs_Layout / 가방아이템.psd '재화클릭시').</summary>
        public static class Detail
        {
            // 시안에서 이 창은 캔버스 위쪽에 따로 떠 있다. 화면 중앙으로 옮겨 쓴다.
            public const float PanelX = 122f, PanelY = 12f, PanelW = 774f, PanelH = 388f;

            public const float NameCx = 509f, NameCy = 61.5f, NameSize = 35f;

            public const float InnerX = 157f, InnerY = 93f, InnerW = 704f, InnerH = 266f;

            // 아이콘 칸 129x129, 그림은 2배(64px -> 128).
            public const float SlotX = 445f, SlotY = 106f, SlotSize = 129f;
            public const float IconCx = 509.5f, IconCy = 173f, IconSize = 128f;

            public const float AmountCx = 509f, AmountCy = 258f, AmountSize = 30f;

            // 기본 네모 박스로 만든 줄 669x5 #7b7c96
            public const float DividerX = 175f, DividerY = 288f, DividerW = 669f, DividerH = 5f;

            public const float DescriptionCx = 509f, DescriptionCy = 325f, DescriptionSize = 30f;
        }
    }
}
