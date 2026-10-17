using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 우주선 하나를 그리는 스프라이트(선체, 자동 포탑) 모음.
    /// </summary>
    public static class SpaceshipSprites
    {
        /// <summary>
        /// 선체와 자동 포탑의 Companion 스프라이트를 sprites 에 채움.
        /// </summary>
        public static void Collect(EntityManager entityManager, Entity spaceship, List<SpriteRenderer> sprites)
        {
            sprites.Clear();
            sprites.Add(entityManager.GetComponentObject<SpriteRenderer>(spaceship));

            if (!entityManager.HasBuffer<AutoTurretMountElement>(spaceship))
            {
                return;
            }

            foreach (AutoTurretMountElement mount in entityManager.GetBuffer<AutoTurretMountElement>(spaceship, true))
            {
                if (entityManager.HasComponent<SpriteRenderer>(mount.Sprite))
                {
                    sprites.Add(entityManager.GetComponentObject<SpriteRenderer>(mount.Sprite));
                }
            }
        }
    }
}
