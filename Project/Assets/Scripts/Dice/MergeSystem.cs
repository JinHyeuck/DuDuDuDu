using UnityEngine;

public class MergeSystem : MonoBehaviour
{
    public static MergeSystem Instance;

    public const int MaxStar = 5;  // ���� ����� ����

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
            Debug.Log("�̹� �ִ� ��");
            return false;
        }

        // ���� Ÿ�Կ��� �� ����
        DiceTypeStarManager.Instance.OnDiceRemove(from.Type, from.Star);
        DiceTypeStarManager.Instance.OnDiceRemove(to.Type, to.Star);

        // Star ����
        int newStar = to.Star + 1;

        // ���� Ÿ�� ����
        DiceType newType = DiceTypeCache[Random.Range(0, DiceTypeCache.Length)];

        // Ÿ�� ���̽��� ����
        to.Init(newType, newStar, to.SlotIndex);

        // DiceTypeStarManager ����
        DiceTypeStarManager.Instance.OnDiceSpawn(newType, newStar);

        // ���� ���̽� ����
        Destroy(from.gameObject);

        return true;
    }
}
