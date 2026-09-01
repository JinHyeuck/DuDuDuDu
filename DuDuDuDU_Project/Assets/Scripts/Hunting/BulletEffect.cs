using System.Collections;
using System.Collections.Generic;
using OJ.DI;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;

namespace OJ.Hunting
{
    public class BulletEffect : MonoBehaviour
    {
        // 8.3b: 이 컴포넌트는 씬에 놓여 있지 않고 BulletEffectPool 이 런타임에 프리팹으로 찍는다.
        // 배틀 스코프의 씬 루트 순회는 씬 로드 때 한 번뿐이라 그 뒤에 태어나는 이 오브젝트에는
        // 닿지 않는다 — 대신 풀이 resolver.Instantiate 로 찍으면서 그 자리에서 채워 준다.
        // 이 경로는 Awake 에서도 안전하다 — 부모 있는 Instantiate 가 프리팹을 껐다 찍고
        // 주입한 뒤에 켜므로(ObjectResolverUnityExtensions.cs:78-91) 클론의 Awake 는
        // 주입 뒤에 돈다. (씬에 놓인 컴포넌트는 반대로 자기 Awake 뒤에 채워진다.)
        // 유일한 사용처인 ReleaseObj 는 Update 나 ForceRelease 에서만 도달하니
        // 어느 쪽이든 한참 지난 뒤다. 비어 있다면 그건 사고이므로 ?. 로 감싸지 않는다.
        [Inject] private IBattleRefs battle;

        public DiceType myDiceType = DiceType.Max;
        public EffectID myEffectType = EffectID.S;

        [SerializeField]
        private List<Sprite> spriteEffect;

        [SerializeField]
        private bool PlayLoop = false;

        [SerializeField]
        private SpriteRenderer m_spriteRenderer;


        private bool m_isPlay = false;

        public float FrameTime = 0.2f;
        public float Duration => (spriteEffect != null ? spriteEffect.Count : 0) * Mathf.Max(0.0001f, FrameTime);

        private int aniIndex = 0;
        private float nextFrameTime = 0;

        public void PlayEffect()
        {
            aniIndex = 0;
            nextFrameTime = 0.0f;

            m_isPlay = true;
            Update();
        }

        public void PlayLineEffect(Vector3 startPos, Vector3 endPos)
        {
            Vector3 myPos = startPos + endPos;
            myPos *= 0.5f;

            transform.position = myPos;

            Vector3 dirvec = startPos - endPos;

            Vector2 size = m_spriteRenderer.size;

            size.x = dirvec.magnitude;
            m_spriteRenderer.size = size;

            dirvec.Normalize();

            transform.rotation = Quaternion.FromToRotation(Vector3.right, dirvec);

            aniIndex = 0;
            nextFrameTime = 0.0f;

            m_isPlay = true;
            Update();
        }

        //------------------------------------------------------------------------------------
        private void Update()
        {
            if (m_isPlay == false)
                return;

            if (nextFrameTime < Time.time)
            {
                if (spriteEffect.Count <= aniIndex)
                {
                    if(PlayLoop == true)
                    {
                        aniIndex = 0;
                        return;
                    }
                    
                    ReleaseObj();
                    return;
                }

                m_spriteRenderer.sprite = spriteEffect[aniIndex];
                aniIndex++;

                nextFrameTime = Time.time + FrameTime;
            }
        }
        //------------------------------------------------------------------------------------
        public void ForceRelease()
        {
            ReleaseObj();
        }
        //------------------------------------------------------------------------------------
        protected void ReleaseObj()
        {
            m_isPlay = false;
            battle.BulletEffects.PoolBullet(this);
        }
        //------------------------------------------------------------------------------------
    }
}
