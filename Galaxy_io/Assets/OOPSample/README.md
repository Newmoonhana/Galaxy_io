# OOP Box Simulator

`Assets/ECSSample`의 DOTS 박스 시뮬레이터를 **고전적인 GameObject + MonoBehaviour**로 다시 만든
것입니다. 게임 자체는 완전히 같습니다. 2000 x 2000 월드, 스폰 때 정해진 방향으로 계속 떠다니는
흰 박스들, WASD로 움직이는 플레이어 박스, 개체수를 런타임에 바꾸는 HUD, 그리고 플레이어에 닿은
봇이 사라지는 것. 봇의 파괴가 직접 호출이 아니라 **이벤트/메시지**를 거친다는 점까지 같습니다.

존재 이유는 하나입니다. **같은 게임을 두 아키텍처로 돌려 보고 성능을 비교하는 것.**

메뉴에서 **OOP Simulator ▸ Build Simulation Scene**을 누르면 머티리얼, 프리팹, 씬이 전부
생성됩니다. 그 다음 `Scenes/OopSimulation.unity`를 열고 Play.

---

## 1. ECS 구현과의 1:1 대응표

| 역할 | ECS (`Assets/ECSSample`) | OOP (`Assets/OOPSample`) |
|---|---|---|
| 설정 데이터 | `SimulationConfig` (베이킹된 싱글톤 엔티티) | `Core/SimulationConfig.cs` (`[Serializable]` 필드) |
| 상한선 | `SimulationLimits` | `Core/SimulationLimits.cs` |
| 월드 경계 | `WorldBounds` 싱글톤 | `Core/WorldBounds.cs` (봇마다 값 복사) |
| 통계 | `SimulationStats` 싱글톤 | `Core/SimulationStats.cs` (참조 하나) |
| 난수 | `SimulationRandom` (`Unity.Mathematics.Random`) | `Core/SimulationRandom.cs` (xorshift32) |
| 개체 목록 | `EntityQuery` (공짜) | `Core/BotRegistry.cs` (직접 관리) |
| 프레임 배치 | `SimulationSystemGroups.cs` | `Core/SimulationRunner.cs` + `DefaultExecutionOrder` |
| 입력 | `PlayerInputSystem` + `PlayerInputState` | `Services/PlayerInputReader.cs` |
| 플레이어 스폰 | `PlayerSpawnSystem` | `Services/PlayerSpawner.cs` |
| 봇 스폰/컬링 | `BotSpawnSystem` | `Services/BotSpawner.cs` |
| 플레이어 이동 | `PlayerMovementSystem` + `PlayerMoveJob` | `Components/PlayerBehaviour.Update` |
| 봇 이동 | `BotMovementSystem` + `BotMoveJob` (병렬 Burst) | `Components/BotBehaviour.Update` (개체별) |
| 충돌 검출 (PRODUCER) | `CollisionDetectionSystem` + `IJobEntity` | `Components/BotBehaviour.Update` 후반부 |
| 메시지 전달 | 이벤트 엔티티 + `EventCommandBufferSystem` | `Events/SimulationEventBus.cs` |
| 메시지 페이로드 | `BotHitPlayerEvent : IComponentData` | `Events/BotHitPlayerEvent.cs` (`readonly struct`) |
| 봇 제거 (CONSUMER) | `BotDespawnSystem` | `Services/EventConsumers.cs` → `BotDespawner` |
| 점수 (CONSUMER) | `ScoreSystem` | `Services/EventConsumers.cs` → `ScoreKeeper` |
| 메시지 청소 | `EventCleanupSystem` | `SimulationRunner.LateUpdate` → `ClearAll()` |
| HUD | `UI/SimulationHud.cs` (EntityQuery로 월드 왕복) | `UI/SimulationHud.cs` (러너 참조 하나) |
| 렌더링 | Entities Graphics (BatchRendererGroup) | MeshRenderer + GPU 인스턴싱 |
| 씬 생성 | `Editor/SimulationSceneBuilder.cs` | `Editor/OopSimulationSceneBuilder.cs` |

ECS 쪽에만 있고 여기엔 없는 것: SubScene, 베이커(Baker), Authoring 컴포넌트,
`EntityCommandBuffer`, 매니지드/언매니지드 경계. 전부 "월드가 둘"이라는 사실 때문에 필요했던
장치들이라, GameObject 하나뿐인 세계에서는 통째로 사라집니다.

OOP 쪽에만 있고 ECS엔 없는 것: `BotRegistry`(쿼리를 대신할 수동 목록), 구독 해제 뒷정리,
`DefaultExecutionOrder`로 짜 맞춘 실행 순서.

---

## 2. 프레임 배치

DOTS는 시스템 그룹과 `UpdateBefore`/`UpdateAfter`로 순서를 **선언**합니다. MonoBehaviour에는
그런 스케줄러가 없으므로, (a) 러너가 명시적으로 호출하는 단계와 (b) `DefaultExecutionOrder`로
줄 세운 개체별 `Update`를 섞어 같은 순서를 만듭니다.

```
SimulationRunner.Update            [order -1000]
  PlayerInputReader.Sample() ....... 입력 샘플링           (ECS: PlayerInputSystem)
  PlayerSpawner.Tick() ............. 플레이어 1개 보장      (ECS: PlayerSpawnSystem)
  BotSpawner.Tick() ................ 개체수를 상한에 맞춤   (ECS: BotSpawnSystem)

PlayerBehaviour.Update             [order -500]
  입력 -> 속도 -> 위치, 경계 clamp                        (ECS: PlayerMovementSystem)

BotBehaviour.Update                [order 0]
  적분 + 벽 반사                                          (ECS: BotMovementSystem)
  플레이어와 AABB 검사 -> 메시지 발행  PRODUCER            (ECS: CollisionDetectionSystem)

SimulationRunner.LateUpdate        [order -1000]
  bus.DispatchAll() ................ CONSUMERS            (ECS: EventCommandBufferSystem
    BotDespawner / ScoreKeeper                                  + EventConsumerSystemGroup)
  bus.ClearAll() ................... 한 프레임 수명        (ECS: EventCleanupSystem)

SimulationHud.Update / LateUpdate  [order 500]
```

**플레이어가 봇보다 먼저 움직여야 하는 이유.** DOTS에서는 플레이어와 봇의 이동이 모두
`MovementSystemGroup` 안에서 끝난 뒤에야 `CollisionSystemGroup`이 돌기 때문에, 충돌 검사는 항상
"이번 프레임 이동을 마친 플레이어"를 봅니다. 봇이 자기 `Update` 안에서 충돌까지 처리하는 OOP
구조에서 같은 결과를 얻으려면 플레이어가 모든 봇보다 먼저 움직여야 하고, 그래서
`PlayerBehaviour`에 `[DefaultExecutionOrder(-500)]`이 붙어 있습니다. 이걸 빼면 봇마다 다른
프레임의 플레이어 위치를 보게 되어 두 구현의 결과가 갈립니다.

---

## 3. OOP에서의 메시지 패턴

ECS의 이벤트 엔티티 패턴이 보장하던 세 가지 성질을 그대로 유지하는 것이 목표였습니다.
프로듀서는 컨슈머를 모르고, 컨슈머끼리도 서로를 모르며, 메시지 수명은 정확히 한 프레임.

```
BotBehaviour.Update               PRODUCER
        │  bus.BotHitPlayer.Publish(...)   ← 리스트에 쌓기만 한다
        ▼
SimulationRunner.LateUpdate       DISPATCH
        │  bus.DispatchAll()
        ├──────────────┬────────────────────────────┐
        ▼              ▼                            │
BotDespawner       ScoreKeeper       CONSUMERS      │  (여기에 VFX든 사운드든
 봇을 파괴          횟수를 센다        서로 모름       │   추가해도 위쪽은 안 바뀐다)
        └──────────────┴────────────────────────────┘
        ▼
bus.ClearAll()                    CLEANUP
```

**왜 C# `event`를 즉시 호출하지 않는가.** 봇의 `Update` 한복판에서 `BotDespawner`가
`Destroy`를 부르고 `BotRegistry`를 수정하면, 스포너가 순회 중인 컬렉션이 발밑에서 바뀝니다.
DOTS가 구조적 변경(structural change)을 `EntityCommandBuffer`로 미루는 것과 정확히 같은 이유로,
여기서도 발행과 처리 사이에 프레임 경계를 하나 둡니다.

**왜 메시지를 하나씩이 아니라 리스트째로 넘기는가.** `ScoreKeeper`처럼 개수만 필요한 컨슈머가
델리게이트를 N번 호출하지 않아도 되고, ECS 컨슈머가 쿼리 결과를 통으로 받는 모양과도 같습니다.
봇이 10만 마리인 상황에서 델리게이트 호출 10만 번은 그냥 넘길 수 있는 비용이 아닙니다.

**ECS엔 없던 부담.** 구독은 해제해야 합니다. 그래서 버스를 전역 `static` 이벤트가 아니라
`SimulationRunner`가 소유하는 인스턴스로 두었고, 러너가 죽으면 버스와 구독이 함께 사라집니다.
이벤트 엔티티에는 애초에 구독이라는 개념이 없었다는 점이 이 패턴의 진짜 차이입니다.

---

## 4. 공정한 비교를 위해 맞춘 것들

성능을 재는 것이 목적이므로, **아키텍처와 무관한 변수**는 양쪽을 같게 맞췄습니다. 이걸 안 하면
ECS와 OOP를 비교하는 게 아니라 "Unity API 함정을 밟았는가"를 비교하게 됩니다.

* **렌더링.** 같은 1x1 쿼드 메시, 같은 URP/Unlit 흰색 머티리얼, 그림자·라이트 프로브·리플렉션
  프로브·모션 벡터 전부 off. 머티리얼의 `enableInstancing`은 **반드시 켜져 있어야 합니다.**
  ECS 쪽은 Entities Graphics(BatchRendererGroup)가 알아서 배칭하므로, 여기서 인스턴싱을 끄면
  드로우콜에 파묻혀 시뮬레이션 로직 비용은 재 보지도 못합니다. 씬 빌더가 켜 줍니다.
* **`Time.deltaTime`.** ECS 잡은 델타 타임을 잡 필드로 한 번 복사받습니다. 봇마다
  `UnityEngine.Time.deltaTime`(네이티브 프로퍼티 호출)을 때리게 두면 아키텍처가 아니라 엔진 API
  호출 횟수를 비교하게 되므로, `SimulationRunner`가 프레임당 한 번 읽어 `SimulationTime`에
  게시하고 봇들은 그 값을 읽습니다.
* **`transform` 캐싱.** `Awake`에서 한 번만 받아 둡니다. 어떤 OOP 코드베이스에서도 하는 일입니다.
* **위치 보관.** 봇은 위치를 자기 필드에 들고 있고 `Transform`에는 쓰기만 합니다.
  ECS의 `LocalTransform` 컴포넌트와 같은 취급입니다.
* **목록 제거.** `BotRegistry`는 봇이 자기 인덱스를 들고 있는 O(1) swap-back 제거를 씁니다.
  `List.Remove`의 선형 탐색으로 10만 개를 훑는 건 아키텍처 문제가 아니라 자료구조 실수입니다.
* **파라미터.** 월드 경계, 박스 크기, 속도 범위, 프레임당 스폰 예산(1000), 시드까지 동일합니다.

반대로, **일부러 최적화하지 않은 것**이 하나 있습니다. 봇의 이동과 충돌 검사는 매니저가 배열을
순회하는 방식이 아니라, 개체마다 `MonoBehaviour.Update`가 호출되는 방식 그대로 두었습니다.
매니지드 `Update` 디스패치와 개체별 `Transform` 쓰기가 바로 이 비교에서 재고 싶은 비용이기
때문입니다. 이걸 매니저 루프로 접어 넣으면 "GameObject + Components"가 아니게 됩니다.

한편 난수 시퀀스는 두 구현이 서로 다릅니다(`Unity.Mathematics.Random` vs xorshift32). 분포와
결정성은 같지만 개별 박스의 위치까지 일치하지는 않습니다. 통계적으로 같은 장면이므로 성능
비교에는 영향이 없습니다.

---

## 5. 측정할 때

* **에디터에서 잰 수치는 믿지 마세요.** 특히 하이어라키 창이 열려 있으면 봇 10만 개짜리
  하이어라키를 그리느라 에디터가 먼저 죽습니다. 최소한 하이어라키를 접거나 다른 탭으로 옮기고,
  가능하면 **스탠드얼론 빌드**로 재세요. 씬 빌더가 두 씬을 모두 Build Settings에 등록합니다.
* 양쪽 다 **같은 개체수, 같은 해상도, 같은 창 크기**로. 박스가 작아서 렌더링 비용이
  화면 픽셀 수에 민감합니다.
* Profiler에서 볼 지점: OOP는 `PlayerLoop ▸ Update.ScriptRunBehaviourUpdate`와
  `PostLateUpdate.FinishFrameRendering`, ECS는 `SimulationSystemGroup`과 Entities Graphics의
  컬링/배칭. 프레임 전체 시간만 보지 말고 이 항목들이 각각 어떻게 늘어나는지 보세요.
* **스폰 자체의 비용**도 별개의 관찰 대상입니다. 상한을 1000에서 100000으로 올리는 순간의
  스파이크는 `Object.Instantiate` N회와 `EntityManager.Instantiate(prefab, count)` 배치
  연산의 차이이며, 양쪽 다 프레임당 1000개로 예산을 잡아 뒀습니다.
* 개체수를 1000 → 5000 → 20000 → 50000 → 100000으로 올리며 재면 두 곡선이 갈리는 지점이
  보입니다. 그 지점이 이 비교의 결론입니다.

---

## 6. 조작과 튜닝

* **WASD** / **화살표 키** / 게임패드 왼쪽 스틱으로 플레이어를 움직입니다. 바인딩은
  `Assets/InputAction/SimControls.inputactions`에 있습니다(ECS 샘플의 것과 같은 내용의 별도 사본).
* **MAX BOT BOXES** + **APPLY**로 개체수 상한을 실시간으로 바꿉니다. 올리면 채워지고, 내리면
  잉여분이 정리됩니다. 텍스트 필드에 포커스가 있는 동안에는 WASD가 게임플레이로 가지 않습니다.
* 나머지 값(월드 경계, 박스 크기, 속도 범위, 시드)은 씬의 `Simulation` 오브젝트에 붙은
  `SimulationRunner` 인스펙터에 있습니다. ECS 쪽은 SubScene을 다시 베이크해야 반영되지만,
  여기서는 베이킹이 없으므로 그냥 바뀝니다.

---

## 7. 레이아웃

```
Assets/OOPSample/
  Assets/InputAction/SimControls.inputactions
  Materials/WhiteBox.mat                     씬 빌더가 생성
  Prefabs/BotBox.prefab, PlayerBox.prefab    씬 빌더가 생성
  Scenes/OopSimulation.unity                 씬 빌더가 생성
  Scripts/
    Core/
      SimulationRunner.cs        합성 루트 + 프레임 스케줄러
      SimulationConfig.cs        설정 데이터
      SimulationLimits.cs        HUD clamp 상한
      SimulationStats.cs         HUD가 읽는 카운터
      SimulationRandom.cs        결정적 xorshift32
      SimulationTime.cs          프레임당 한 번 게시되는 델타 타임
      WorldBounds.cs             월드 사각형
      BotRegistry.cs             EntityQuery를 대신하는 수동 목록
    Components/
      BotBehaviour.cs            이동 + 벽 반사 + 충돌 검출  PRODUCER
      PlayerBehaviour.cs         입력 기반 이동
    Events/
      SimulationEventBus.cs      채널 + 발행/디스패치/청소 + 패턴 설명
      BotHitPlayerEvent.cs       메시지 페이로드
    Services/
      PlayerInputReader.cs       Input System 샘플링
      PlayerSpawner.cs           플레이어 1개 보장
      BotSpawner.cs              개체수 유지 + 프레임 예산
      EventConsumers.cs          BotDespawner / ScoreKeeper  CONSUMERS
    UI/
      SimulationHud.cs           개체수 조절 + 통계 표시
    Editor/
      OopSimulationSceneBuilder.cs   메뉴 하나로 전 에셋 재생성
```
