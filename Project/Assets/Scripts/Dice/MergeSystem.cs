using UnityEngine;

public class MergeSystem : MonoBehaviour
{
    public static MergeSystem Instance;

    public const int MaxStar = 5;  // 정적 상수로 변경

    private DiceType[] DiceTypeCache = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        DiceTypeCache = (DiceType[])System.Enum.GetValues(typeof(DiceType));
    }


    public bool TryMerge(UIDice from, UIDice to)
    {
        if (from.Type != to.Type || from.Star != to.Star)
            return false;

        if (to.Star >= MaxStar)
        {
            Debug.Log("이미 최대 별");
            return false;
        }

        // 기존 타입에서 별 제거
        DiceTypeStarManager.Instance.OnDiceRemove(from.Type, from.Star);
        DiceTypeStarManager.Instance.OnDiceRemove(to.Type, to.Star);

        // Star 증가
        int newStar = to.Star + 1;

        // 랜덤 타입 선택
        DiceType newType = DiceTypeCache[Random.Range(0, DiceTypeCache.Length)];

        // 타겟 다이스에 적용
        to.Init(newType, newStar, to.SlotIndex);

        // DiceTypeStarManager 갱신
        DiceTypeStarManager.Instance.OnDiceSpawn(newType, newStar);

        // 원본 다이스 제거
        Destroy(from.gameObject);

        return true;
    }
}
