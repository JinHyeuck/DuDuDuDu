using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OJ.Utils
{
    [System.Serializable]
    public class CharacterAniData
    {
        public CharacterState AniType = CharacterState.None;

        public float AniDefaultFrameTime = 0.2f;

        public float FrameTime = 0.2f;
        public List<Sprite> AniSprites = new List<Sprite>();

        public bool Loop = false;

        public CharacterState AniAndAction = CharacterState.None;
    }

    public class CharacterAnimation : MonoBehaviour
    {
        public List<CharacterAniData> characterAniDatas = new List<CharacterAniData>();

        public SpriteRenderer spriteRenderer;

        private CharacterAniData currentAniData = null;
        private int aniIndex = 0;
        private float nextFrameTime = 0;

        public CharacterState CurrentAni = CharacterState.None;

        public void PlayAnimation(CharacterState characterState)
        {
            currentAniData = characterAniDatas.Find(x => x.AniType == characterState);

            if (currentAniData != null)
                currentAniData.FrameTime = currentAniData.AniDefaultFrameTime;

            if (CurrentAni != characterState)
            {
                aniIndex = 0;
                nextFrameTime = 0.0f;
            }

            CurrentAni = characterState;

            Update();
        }

        public void PlayAnimation(CharacterState characterState, float frametime)
        {
            currentAniData = characterAniDatas.Find(x => x.AniType == characterState);

            if (currentAniData != null)
            {
                if (frametime < currentAniData.AniDefaultFrameTime)
                    currentAniData.FrameTime = frametime;
                else
                    currentAniData.FrameTime = currentAniData.AniDefaultFrameTime;
            }

            if (CurrentAni != characterState)
            {
                aniIndex = 0;
                nextFrameTime = 0.0f;
            }

            CurrentAni = characterState;

            Update();
        }

        void Update()
        {
            if (currentAniData == null)
                return;

            if (nextFrameTime < Time.time)
            {
                if (currentAniData.AniSprites.Count <= aniIndex)
                {
                    if (currentAniData.Loop == false && currentAniData.AniAndAction != CharacterState.None)
                    {
                        PlayAnimation(currentAniData.AniAndAction);
                        return;
                    }

                    aniIndex = 0;
                }

                spriteRenderer.sprite = currentAniData.AniSprites[aniIndex];
                aniIndex++;

                nextFrameTime = Time.time + currentAniData.FrameTime;
            }
        }
    }

}
