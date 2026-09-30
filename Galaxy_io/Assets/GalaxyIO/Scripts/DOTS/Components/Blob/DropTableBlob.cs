using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Blob
{
    /// <summary>
    /// 파괴 시 떨어뜨릴 아이템 목록. Roll 마다 따로 굴림.
    /// </summary>
    public struct DropTableBlob
    {
        public BlobArray<DropRollBlob> Rolls;
    }

    /// <summary>
    /// 확률에 성공하면 항목 중 하나를 가중치로 골라 떨어뜨림.
    /// </summary>
    public struct DropRollBlob
    {
        // Roll 성공 확률 (0 ~ 1)
        public float Chance;

        // 항목 가중치 합
        public int TotalWeight;

        public BlobArray<DropEntryBlob> Entries;
    }

    public struct DropEntryBlob
    {
        public ItemId ItemId;

        // 한 번에 떨어뜨릴 개수 범위 (양 끝 포함)
        public int MinCount;
        public int MaxCount;

        // 같은 Roll 안에서 뽑힐 비중
        public int Weight;
    }
}
