using System.Collections.Generic;
using UnityEngine;
using OJ;

namespace OJ
{
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance;

        public CharacterAnimation characterAnimation;
        public CharacterAnimation bowAnimation;
        public Transform bowTransform;


        public Transform firePoint;
        public float fireRate = 0.5f;
        private float timer = 0f;
        private int shotindex = 0;

        private List<DiceType> diceTypes = new List<DiceType>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            characterAnimation.PlayAnimation(CharacterState.Idle);
            bowAnimation.PlayAnimation(CharacterState.Idle);
        }

        void Update()
        {
            if (GameManager.Instance.inGameState != InGameState.Wave)
                return;

            if (UIBoard.Instance == null
                || UIBoard.Instance.diceMap == null
                || UIBoard.Instance.diceMap.Length <= 0)
                return;


            //if (diceTypes.Count <= 0)
            //    return;

            timer += Time.deltaTime;
            if (timer >= fireRate)
            {
                shotindex++;

                if (shotindex >= UIBoard.Instance.diceMap.Length)
                    shotindex = 0;

                List<DiceType> diceType = null;
                int diceStar = 1;

                bool IsFirst = true;

                for (int i = shotindex; i < UIBoard.Instance.diceMap.Length; ++i)
                {
                    if (shotindex == i)
                    {
                        if (IsFirst == false)
                            break;
                        else
                            IsFirst = false;
                    }

                    if (UIBoard.Instance.diceMap[i] == null)
                    {
                        if (UIBoard.Instance.diceMap.Length - 1 <= i)
                            i = -1;
                        continue;
                    }
                    else
                    {
                        UIDice uIDice = UIBoard.Instance.diceMap[i];
                        shotindex = i;
                        diceType = uIDice.Type;
                        diceStar = uIDice.Star;
                        uIDice.PlayLevelUpEffect();
                        break;
                    }
                }

                if (diceType != null)
                    ShootAtClosest(diceType, diceStar);
                timer = 0f;


            }
        }

        public void RefreshDice()
        {
            diceTypes.Clear();

            foreach (var pair in DiceTypeStarManager.Instance.typeCountTotals)
            {
                for (int i = 0; i < pair.Value; ++i)
                {
                    diceTypes.Add(pair.Key);
                }
            }

            diceTypes.Shuffle();

            //fireRate = 1.0f / (float)diceTypes.Count;
            //shotindex = 0;
        }

        void ShootAtClosest(List<DiceType> diceType, int diceStar)
        {
            Monster target = MonsterManager.Instance.GetClosestMonster(firePoint.position);
            if (target == null) return;

            Vector2 dir = (target.transform.position - firePoint.position).normalized;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            bowTransform.rotation = Quaternion.Euler(0, 0, angle);

            Bullet bulletObj = BulletPool.Instance.GetBullet();
            bulletObj.transform.position = firePoint.position;
            bulletObj.transform.rotation = Quaternion.identity;
            bulletObj.SetBulletStat(diceType, diceStar);
            bulletObj.Shoot(dir);
            characterAnimation.PlayAnimation(CharacterState.Attack, fireRate);
            bowAnimation.PlayAnimation(CharacterState.Attack, fireRate);
        }
    }

}
