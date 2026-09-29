using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Blob;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 파괴 시 떨어뜨릴 아이템 정의.
    /// </summary>
    public sealed class DropTableAuthoring : MonoBehaviour
    {
        // TODO: 드랍 테이블 레지스트리 항목 참조 추가.
    }

    public sealed class DropTableBaker : Baker<DropTableAuthoring>
    {
        public override void Bake(DropTableAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            var builder = new BlobBuilder(Allocator.Temp);
            ref DropTableBlob table = ref builder.ConstructRoot<DropTableBlob>();

            // TODO: 드랍 테이블 레지스트리 항목으로 채우기.
            builder.Allocate(ref table.Entries, 0);

            BlobAssetReference<DropTableBlob> blob = builder.CreateBlobAssetReference<DropTableBlob>(Allocator.Persistent);
            builder.Dispose();

            // 같은 내용의 블롭은 하나로 합쳐져 인스턴스끼리 공유됨.
            AddBlobAsset(ref blob, out _);

            AddComponent(entity, new DropTableComponent { Value = blob });
        }
    }
}
