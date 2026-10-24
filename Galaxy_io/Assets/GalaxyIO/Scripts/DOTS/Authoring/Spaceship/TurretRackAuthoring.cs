using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 조종 포탑의 장전된 투사체를 우주선 위에 그리는 발사대. e.g. Thor 미사일.
    /// 쿨다운이 거의 끝나면 그림이 숨은 자리에서 밀려 나오며 선명해지고, 그 총구가 쏘면 바로 감춤.
    /// </summary>
    public sealed class TurretRackAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct Round
        {
            [Tooltip("투사체 그림. 우주선의 자식. 지금 위치가 다 나온 자리. 나오면서 투명도가 0 에서 1 로 바뀜.")]
            public SpriteRenderer Sprite;

            [Tooltip("이 그림이 나타내는 총구 번호(SpaceshipAuthoring.Muzzles 순서). 이 총구가 쏘면 감춤.")]
            [Min(0)]
            public int Muzzle;

            [Tooltip("장전된 뒤 꺼내기 시작할 때까지 기다리는 시간(초). 그림마다 차례로 나오게 할 때 씀.")]
            [Min(0.0F)]
            public float Delay;
        }

        [SerializeField]
        [Tooltip("첫 총구 쿨다운이 이만큼(초) 남으면 꺼내기 시작함.")]
        [Min(0.0F)]
        public float ShowTime = 0.5F;

        [SerializeField]
        [Tooltip("숨은 자리에서 다 나오기까지 걸리는 시간(초)")]
        [Min(0.01F)]
        public float SlideTime = 0.3F;

        [SerializeField]
        [Tooltip("숨은 자리. 다 나온 자리에서 그림의 -Y 방향으로 이만큼 들어감.")]
        [Min(0.0F)]
        public float Slide = 0.8F;

        [SerializeField]
        public List<Round> Rounds = new List<Round>();
    }

    public sealed class TurretRackBaker : Baker<TurretRackAuthoring>
    {
        public override void Bake(TurretRackAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new TurretRackComponent
            {
                ShowTime  = authoring.ShowTime,
                SlideTime = authoring.SlideTime,
            });

            DynamicBuffer<TurretRackElement> rounds = AddBuffer<TurretRackElement>(entity);

            foreach (TurretRackAuthoring.Round round in authoring.Rounds)
            {
                if (round.Sprite == null)
                {
                    continue;
                }

                Transform sprite = round.Sprite.transform;
                Vector3 shown = sprite.localPosition;
                Vector3 hidden = shown - sprite.localRotation * Vector3.up * authoring.Slide;

                rounds.Add(new TurretRackElement
                {
                    Sprite = GetEntity(round.Sprite, TransformUsageFlags.Dynamic),
                    Muzzle   = round.Muzzle,
                    Shown    = new float3(shown.x, shown.y, shown.z),
                    Hidden   = new float3(hidden.x, hidden.y, hidden.z),
                    Delay    = round.Delay / authoring.SlideTime,
                    Progress = -round.Delay / authoring.SlideTime,
                });
            }
        }
    }
}
