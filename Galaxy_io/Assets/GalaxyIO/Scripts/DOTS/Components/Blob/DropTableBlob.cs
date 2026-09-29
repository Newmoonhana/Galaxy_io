using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Blob
{
    /// <summary>
    /// 파괴 시 떨어뜨릴 아이템 목록.
    /// </summary>
    public struct DropTableBlob
    {
        public BlobArray<DropEntryBlob> Entries;
    }

    // TODO: 아이템 레지스트리 구현 시 구조 확정. 현재는 임시 필드.
    public struct DropEntryBlob
    {
        // TODO: 아이템 레지스트리 키
        public ushort ItemId;

        // TODO: 드랍 확률 (0 ~ 1)
        public float Chance;
    }
}
