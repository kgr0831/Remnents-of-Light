# VFX 배치 샌드박스 씬 (`Assets/Scenes/VfxSandbox.unity`)

에디터에서 실드·블룸의 **위치와 크기를 눈으로 보며 잡기 위한 전용 씬**.
빌드 세팅에 등록하지 않았으므로 실제 게임에는 아무 영향이 없다 — 마음대로 켜고 끄고 옮겨도 된다.

조정이 끝나면 **"샌드박스 씬 반영해줘"** 라고 말하면, 아래 표대로 값을 읽어 실제 코드/씬에 옮긴다.

## 읽어서 반영하는 대응표

| 씬 오브젝트 | 읽는 값 | 반영 대상 |
|---|---|---|
| `Player_StandIn` / **활성화된** `ParryShield_*` 자식 | `transform.localPosition.xy` | `PlayerController.parryShieldOffset` |
| 〃 | `transform.localScale.x` (균등) | `PlayerController.parryShieldRadius` |
| 〃 | `MeshRenderer.sharedMaterial` | `Assets/VFX/Parry/ParryShield.mat` 의 `_Thickness`·색을 그 값으로 |
| `IlseomTrail` 아래 **활성화된 `count_N` 그룹** | 그룹 이름의 `N` | `PlayerController.ilseomShieldTrailCount` |
| `PlayerBloom_Overlay` (활성 여부) | 켜져 있으면 블룸 세기를 그 머티리얼 값으로 | `Assets/VFX/Parry/PlayerBloom.mat` |

> `Player_StandIn`은 실제 플레이어와 똑같이 **스케일 1.3**이고, 실드는 그 **자식**이다.
> 그래서 자식의 `localPosition`/`localScale`이 런타임 필드와 **1:1로 그대로 대응**한다. 부모는 옮기지 말 것.

## 씬 구성

- `Main Camera` / `Global Light 2D` / `Global Volume` — 게임과 같은 조건(HDR + PostFX + `IlseomBloomProfile`)
  에서 보이도록 맞춰 둠. 블룸 임계값 1.15가 그대로 적용되므로 발광이 게임과 동일하게 보인다.
- `Player_StandIn` (스케일 1.3, `Glitch Samurai-Idle_0`)
  - `ParryShield_A_현재기본` — offset (-0.22, 0.54) / radius 0.9 · **활성**
  - `ParryShield_B_오른쪽위안` — offset (0.22, 0.54) · 비활성 (피봇 반대쪽 안, 비교용)
  - `ParryShield_C_두껍게+크게` — radius 1.1 + `ParryShield_Thick.mat` · 비활성
  - `ParryShield_D_얇게` — `ParryShield_Thin.mat` · 비활성
  - `PlayerBloom_Overlay` — 일섬/차지 때 걸리는 블룸 미리보기 · 비활성
- `IlseomTrail` — 일섬 경로(길이 7.2)에 깔리는 실드 사본. `count_4 / 6 / 8 / 12` 중 **하나만 켜서** 개수를 고른다.
- `HitFx_Preview` — 패링 성공 시 뜨는 `Hit02` 배치 참고용 · 비활성

## 주의

- 실드 링은 `ParryShieldPreview`(`[ExecuteAlways]`)가 메시를 만들기 때문에 **플레이 모드가 아니어도 보인다**.
  메시 규약은 셰이더와 동일 — 링 반지름이 로컬 1.0이 되도록 반너비 2.0짜리 쿼드를 쓴다.
- 크기는 반드시 **균등 스케일**로 조절할 것. x/y를 다르게 주면 타원이 되어 `parryShieldRadius`(단일 값)로
  옮길 수 없다.
- 새 후보를 만들고 싶으면 기존 `ParryShield_*` 하나를 복제(Ctrl+D)해서 이름만 바꾸면 된다.
