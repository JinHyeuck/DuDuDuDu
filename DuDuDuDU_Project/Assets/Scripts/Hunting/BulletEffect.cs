using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace OJ
{
    public class BulletEffect : MonoBehaviour
    {
        public DiceType myDiceType = DiceType.Max;
        public EffectID myEffectType = EffectID.S;

        [SerializeField]
        private List<Sprite> spriteEffect;

        [SerializeField]
        private SpriteRenderer m_spriteRenderer;


        private bool m_isPlay = false;

        public float FrameTime = 0.2f;

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
            BulletEffectPool.Instance.PoolBullet(this);
        }
        //------------------------------------------------------------------------------------
    }
}