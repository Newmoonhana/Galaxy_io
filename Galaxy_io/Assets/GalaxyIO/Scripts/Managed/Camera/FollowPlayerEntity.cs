using GalaxyIO.Components.Tags;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Camera
{
    /// <summary>
    /// DOTS 오브젝트인 플레이어 우주선을 따라다니는 카메라 로직
    /// </summary>
    public sealed class FollowPlayerEntity : MonoBehaviour
    {
        [SerializeField]
        private float smoothSpeed = 10F;

        private EntityManager entityManager;
        private EntityQuery playerQuery;

        private void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<LocalPlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
        }

        private void LateUpdate()
        {
            if (playerQuery.IsEmpty)
            {
                return;
            }

            Entity player = playerQuery.GetSingletonEntity();
            LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(player);

            Vector3 target = new Vector3
            (
                playerTransform.Position.x,
                playerTransform.Position.y,
                transform.position.z
            );

            transform.position = Vector3.Lerp(
                transform.position,
                target,
                1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)
            );
        }
    }
}