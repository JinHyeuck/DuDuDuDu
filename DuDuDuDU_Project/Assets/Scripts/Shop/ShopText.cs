using System;
using System.Globalization;
using System.Text;
using OJ.Point;

namespace OJ.Shop
{
    /// <summary>
    /// 상점의 표기 규칙. 숫자·기간·구성품을 <b>어떤 글자로 적을지</b>만 정한다.
    ///
    /// <b>왜 <c>UIShopUIFactory</c> 나 각 뷰가 아니라 별도 파일인가.</b> 헤드리스 테스트
    /// 러너는 <c>UnityEngine.UI</c> 를 못 불러온다. 그래서 <c>Image</c> 필드가 하나라도 있는
    /// 타입은 <b>static 메서드조차 호출할 수 없다</b>(TypeLoadException). 표기 규칙을 뷰 안에
    /// 두면 "유료젬이 맨 앞에 온다" 같은 <b>기획서가 못 박은 규칙</b>이 영영 테스트 밖에 남는다.
    ///
    /// 그래서 UnityEngine.UI 를 참조하지 않는 이 파일에 모았다. 여기 있는 것은 전부
    /// 값에서 값을 만드는 순수 함수다.
    /// </summary>
    public static class ShopText
    {
        /// <summary>"3,000원". 금액은 항상 천 단위를 끊는다 — 자릿수 오독이 곧 결제 사고다.</summary>
        public static string FormatWon(int won)
        {
            return won.ToString("N0", CultureInfo.InvariantCulture) + "원";
        }

        public static string FormatAmount(int amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>"07:12:33". 하루를 안 넘으므로 일 단위는 안 붙인다.</summary>
        public static string FormatTimer(TimeSpan remaining)
        {
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                (int)remaining.TotalHours, remaining.Minutes, remaining.Seconds);
        }

        /// <summary>"2일 4시간". 성장 패키지 배너의 잔여 기간.</summary>
        public static string FormatDuration(int hours)
        {
            if (hours >= 24)
            {
                int days = hours / 24;
                int rest = hours % 24;
                return rest > 0 ? days + "일 " + rest + "시간" : days + "일";
            }

            return hours + "시간";
        }

        /// <summary>
        /// 성장 패키지 구성품 한 줄. <b>유료젬을 맨 앞에 둔다</b> — 기획서 8.1-3 이 요구하는
        /// "10원 = 1젬 즉시 환산"은 젬 수량이 가장 먼저 읽혀야 성립한다. 예시의 스타트
        /// 패키지가 "3,000원 중 3,000원어치가 젬이라 보석은 전액 덤"으로 읽히는 것이 그 효과다.
        /// </summary>
        public static string DescribeContents(ShopDatabase.GrowthPackage package)
        {
            if (package == null || package.contents == null || package.contents.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();

            // 0번 통과에서 유료젬만, 1번 통과에서 나머지를 적는다.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < package.contents.Count; i++)
                {
                    ShopDatabase.Reward reward = package.contents[i];
                    bool isPaidGem = reward.pointType == PointType.PaidGem;

                    if (isPaidGem != (pass == 0))
                        continue;

                    if (builder.Length > 0)
                        builder.Append(" + ");

                    builder.Append(PointRewardUtility.GetPointName(reward.pointType))
                           .Append(' ')
                           .Append(FormatAmount(reward.amount));
                }
            }

            return builder.ToString();
        }
    }
}
