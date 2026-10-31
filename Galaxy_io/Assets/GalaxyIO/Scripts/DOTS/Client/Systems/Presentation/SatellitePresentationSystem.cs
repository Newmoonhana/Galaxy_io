using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 점령전 위성 연출. 점령 범위 원을 점령하는 팀 색(없으면 점령한 팀, 아무도 없으면 흰색)으로 그림.
    /// 원은 <see cref="ConquestConfigComponent.AreaMaterial"/>(Sprite-OccupyArea)로 그리고 테두리에 블룸이 걸림.
    /// 테두리 두께 등은 머티리얼 값을 그대로 씀(복사하거나 덮어쓰지 않음).
    /// 다른 팀이 점령도를 올리기 시작하면 위성을 흔듦. 깜빡임은 상태 표시와 핑이 맡음.
    /// 흔들림은 Companion 스프라이트만 옮김. Companion 위치는 매 프레임 LocalToWorld 에서 다시 복사되므로 쌓이지 않음.
    /// 움직이지 않는 위성은 LocalToWorld 를 다시 계산하지 않아 거기에 넣으면 어긋난 채 남음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public sealed partial class SatellitePresentationSystem : SystemBase
    {
        // 점령이 시작될 때 흔드는 시간(초)과 세기(월드 유닛)
        private const float ShakeTime = 0.6F;
        private const float ShakeAmplitude = 0.6F;

        private static readonly Color RedColor = new Color(1.0F, 0.3F, 0.3F);
        private static readonly Color BlueColor = new Color(0.3F, 0.6F, 1.0F);

        private sealed class View
        {
            public SpriteRenderer Area;
            public bool Captured;
            public float ShakeRemaining;
        }

        private readonly Dictionary<Entity, View> views = new Dictionary<Entity, View>();
        private readonly List<Entity> gone = new List<Entity>();

        // 셰이더가 UV 로 원을 그리므로 흰 사각형 스프라이트 하나면 됨.
        private Sprite square;

        protected override void OnCreate()
        {
            RequireForUpdate<ConquestConfigComponent>();
        }

        protected override void OnDestroy()
        {
            foreach (View view in views.Values)
            {
                if (view.Area != null) Object.Destroy(view.Area.gameObject);
            }

            views.Clear();

            if (square != null)
            {
                Object.Destroy(square.texture);
                Object.Destroy(square);
            }
        }

        protected override void OnUpdate()
        {
            ConquestConfigComponent config = SystemAPI.GetSingleton<ConquestConfigComponent>();
            float radius = config.CaptureRadius;
            float deltaTime = UnityEngine.Time.deltaTime;

            // Companion 위치 복사가 끝난 뒤에 옮김.
            CompleteDependency();

            foreach (Entity entity in views.Keys)
            {
                if (!EntityManager.HasComponent<SatelliteComponent>(entity)) gone.Add(entity);
            }

            foreach (Entity entity in gone)
            {
                if (views[entity].Area != null) Object.Destroy(views[entity].Area.gameObject);
                views.Remove(entity);
            }

            gone.Clear();

            foreach (
                var (satelliteRef, transform, renderer, entity) in
                SystemAPI.Query<RefRO<SatelliteComponent>, RefRO<LocalTransform>, RefRO<CompanionComponent<SpriteRenderer>>>()
                    .WithEntityAccess()
            ) {
                SatelliteComponent satellite = satelliteRef.ValueRO;
                SpriteRenderer body = renderer.ValueRO.CompanionRef.Value;

                if (body == null)
                {
                    continue;
                }

                if (!views.TryGetValue(entity, out View view))
                {
                    view = new View();
                    views[entity] = view;
                }

                if (square == null)
                {
                    var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "OccupyArea" };
                    var pixels = new Color32[16];

                    for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);

                    texture.SetPixels32(pixels);
                    texture.Apply(false, true);
                    square = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5F, 0.5F), 4);
                }

                if (view.Area == null)
                {
                    view.Area = new GameObject("OccupyArea").AddComponent<SpriteRenderer>();
                    view.Area.sprite = square;
                    view.Area.sharedMaterial = config.AreaMaterial.Value;
                    view.Area.sortingLayerID = body.sortingLayerID;
                    view.Area.sortingOrder = body.sortingOrder - 1;
                    view.Area.gameObject.layer = body.gameObject.layer;
                }

                // 원 지름이 점령 반경의 두 배가 되게 늘림. 스프라이트 한 변은 1 유닛.
                Transform area = view.Area.transform;
                area.position = new Vector3(transform.ValueRO.Position.x, transform.ValueRO.Position.y, body.transform.position.z);
                area.localScale = new Vector3(radius * 2.0F, radius * 2.0F, 1.0F);

                Team occupying = satellite.Occupier != Team.None ? satellite.Occupier : satellite.Owner;
                view.Area.color = TeamColor(occupying);

                // 점령도가 오르기 시작하면 흔듦.
                bool captured = satellite.IsBeingCaptured;

                if (captured && !view.Captured)
                {
                    view.ShakeRemaining = ShakeTime;
                }

                view.Captured = captured;

                if (view.ShakeRemaining <= 0.0F)
                {
                    continue;
                }

                view.ShakeRemaining -= deltaTime;

                // 흔들림은 갈수록 약해짐. 포탑 같은 자식도 함께 옮김.
                Vector2 random = UnityEngine.Random.insideUnitCircle * (ShakeAmplitude * math.saturate(view.ShakeRemaining / ShakeTime));
                var offset = new Vector3(random.x, random.y, 0.0F);

                if (SystemAPI.HasBuffer<LinkedEntityGroup>(entity))
                {
                    foreach (LinkedEntityGroup linked in SystemAPI.GetBuffer<LinkedEntityGroup>(entity))
                    {
                        Shake(linked.Value, offset);
                    }
                }
                else
                {
                    Shake(entity, offset);
                }
            }
        }

        private void Shake(Entity entity, Vector3 offset)
        {
            if (SystemAPI.HasComponent<CompanionComponent<SpriteRenderer>>(entity))
            {
                SystemAPI.GetComponent<CompanionComponent<SpriteRenderer>>(entity).CompanionRef.Value.transform.position += offset;
            }
        }

        private static Color TeamColor(Team team)
        {
            return team == Team.Red ? RedColor : team == Team.Blue ? BlueColor : Color.white;
        }
    }
}
