using UnityEngine;

/// <summary>
/// Spaceship의 어트리뷰트를 담은 Prefab용 컴포넌트.
/// </summary>
public sealed class Spaceship : MonoBehaviour
{
    [Header("Attributes")]

    // 월드 좌표 상 이동속도
    public float Speed;

    // 체력
    public float Health;

    // 최대 체력
    public float MaxHealth;

    // 방어력
    public float Defense;

    [Header("Colliders")]

    // 충돌체 크기
    public float Size;

    private void Start()
    {

    }

    private void Update()
    {

    }
}