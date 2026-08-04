# 노션 발행 권한 적용 (사용자 실행 1회)

> 2026-08-04 · 사용자가 "1번"(허용 목록에 Notion 도구 추가)을 선택해 준비한 문서.
> **원격 루프 세션은 이 두 단계를 스스로 못 한다** — `Edit(*.py)`가 허용 목록 밖이고,
> `executor.py` 재시작은 그 세션 자신을 죽인다(`LOOP_ENGINEERING.md` 버그 #5, 두 번 겪음).
>
> 🔁 **재확인 2026-08-04(원격 루프 세션이 실제로 시도)**: "1·2단계를 실행한 뒤 발행해줘"라는
> 지시를 받고 그대로 시도했으나 **세 지점 전부 거부**됐다 —
> ① `notion-fetch("self")` → `Permission ... denied (don't ask mode)`
> ② `Edit(tools/claude_bridge.py)` → 같은 거부(허용은 `*.cs`/`*.md`뿐)
> ③ 재시작용 임의 셸 명령 없음(Bash는 `dotnet test*`/`report_video.py*` 2개만).
> 허용 목록은 **프로세스 시작 시 `--allowedTools`로 고정**이라 `--resume`으로 답장해도 동일하다.
> 즉 이 문서의 1·2단계는 **구조적으로 사용자 몫**이다. 루프로 다시 보내지 말 것.

---

## 1단계 — `tools/claude_bridge.py` 수정

`LOOP_ALLOWED_TOOLS` 리스트의 **닫는 `]` 바로 위**에 아래를 붙여넣는다.
(앵커: `"mcp__UnityMCP__execute_code",` 줄 다음)

```python
    # Notion (진행 현황 기록). 2026-08-04 사용자 승인으로 추가 — 루프 작업이
    # 노션에 진행 현황을 쓰려는데 읽기 전용 search/fetch까지 전부 auto-deny였고
    # 우회 경로도 없었다(Bash는 2개 명령, Edit/Write는 *.cs/*.md로 좁혀져 있음).
    # 와일드카드 대신 열거 — 나중에 추가되는 Notion 도구가 조용히 권한을 얻지 않게.
    # 삭제성 호출(in_trash, 자식 페이지를 날리는 replace_content)은 그대로
    # 루프 모드의 NEEDS_APPROVAL 게이트를 거친다.
    "mcp__claude_ai_Notion__notion-search",
    "mcp__claude_ai_Notion__notion-fetch",
    "mcp__claude_ai_Notion__notion-create-pages",
    "mcp__claude_ai_Notion__notion-update-page",
    "mcp__claude_ai_Notion__notion-get-async-task",
```

**왜 이 5개인가**
| 도구 | 필요한 이유 |
|---|---|
| `notion-search` | 기존 `Remnants of Light` 페이지 ID를 찾는다 |
| `notion-fetch` | 페이지 읽기 + `notion://docs/enhanced-markdown-spec` 조회 |
| `notion-create-pages` | 하위 페이지 5개 생성 |
| `notion-update-page` | 메인 페이지 본문 갱신 |
| `notion-get-async-task` | 생성/갱신이 비동기로 큐잉됐을 때 완료 확인 |

데이터베이스(칸반·타임라인 뷰)까지 쓰고 싶어지면 그때
`notion-create-database` · `notion-update-data-source` · `notion-create-view` · `notion-query-data-sources`를
같은 방식으로 추가하면 된다. 지금 작업엔 불필요해서 뺐다.

---

## 2단계 — executor 재시작

> ⚠️ **진행 중인 루프 작업이 없을 때** 할 것. 재시작하면 실행 중이던 세션이 죽는다.

```powershell
# 1) 지금 돌고 있는 작업이 없는지 확인
Get-CimInstance Win32_Process -Filter "Name='pythonw.exe' OR Name='python.exe'" |
    Select-Object ProcessId, CommandLine

# 2) 재시작
Stop-ScheduledTask  -TaskName RemnentsOfLight-Executor
Start-ScheduledTask -TaskName RemnentsOfLight-Executor

# 3) 살아났는지 확인 (heartbeat 채널에 60초 내 갱신이 찍히면 정상)
Get-ScheduledTask -TaskName RemnentsOfLight-Executor | Get-ScheduledTaskInfo
```

수동으로 띄워 쓰고 있었다면 `taskkill /PID <executor pid>` 후 평소 방식으로 다시 실행.
⚠️ 태스크가 떠 있는 상태에서 수동 실행을 겹치면 **두 프로세스가 같은 큐를 먹는다.**

---

## 3단계 — 디스코드에서 재요청

```
/claude-loop docs/notion/ 의 원고 6개를 노션 "Remnants of Light" 페이지와 하위 페이지로 발행해줘
```

💡 **2단계와 3단계는 순서를 바꿔도 된다 — 오히려 그게 편하다.**
`relay_bot.py`는 원격(dishost.kr)이라 로컬 executor가 죽어 있어도 큐 채널에 `CMD:`를 계속 쌓는다.
`executor.py`의 커서(`last_queue_msg_id`)는 `.secrets/executor_state.json`에 저장돼 있고, 재시작하면
**커서보다 뒤인 메시지를 전부 이어서 집어간다**(`executor.py:236-265`, 최근 20개 조회).
그래서 **① 위 명령을 먼저 보내고 → ② Stop/Start** 하면 재시작 직후 3초 안에 자동으로 발행이 시작된다.
"재시작하고 나서 다시 보내는 걸 잊는" 왕복이 사라진다.

---

## 원고 프리플라이트 (2026-08-04 완료 — 발행 세션은 다시 안 해도 됨)

발행 전에 원고 6개를 정적 점검했다. **구조적 결함 0건**:

| 점검 | 결과 |
|---|---|
| `<`로 시작하는 줄 (확장 XML 문법과 충돌 위험) | ✅ 0건 — 원고가 표준 마크다운뿐이라 안전 |
| 코드펜스 균형 | ✅ 짝수 (01=2, 04=6, 05=2, 나머지 0) |
| 하위 5개 페이지 H1 ↔ `00_MAIN.md` 하위 페이지 표 | ✅ 5개 전부 문자열 일치 |
| 표 열 수 일관성 (00·05 전수) | ✅ 헤더/구분선/본문 모두 일치 |

**고친 것 1건**: `00_MAIN.md`의 하위 페이지 표가 함정 노트를 "교훈 **15**건"으로 적고 있었으나
`05_Unity-함정-노트.md`의 실제 항목은 **42건**(1~42번)이다 → 42로 수정.

⚠️ 남은 미확정은 여전히 **파이프 표의 API 렌더링 하나뿐**이다(아래 3번에서 확인).

---

## 발행 세션이 따라야 할 절차

1. `notion-fetch`로 **`notion://docs/enhanced-markdown-spec`을 먼저 읽는다.** (아래 조사 결과와 대조)
2. `notion-search`로 기존 `Remnants of Light` 페이지 ID 확보.
3. **표 렌더링을 먼저 검증한다** — 원고가 표 중심이라 여기서 갈린다.
   메인 페이지를 `notion-update-page`로 올린 뒤 `notion-fetch`로 되읽어서,
   파이프 표(`| a | b |`)가 **진짜 table 블록**이 됐는지 확인한다.
   글자로 그대로 남았다면 아래 `<table>` XML 형식으로 변환해야 한다.
4. 하위 5개는 `notion-create-pages` (`parent.page_id` = 메인).
5. 스펙 확인 후 인용문(`>`)을 콜아웃으로, 긴 목록을 토글로 승격.

---

## 조사해둔 Notion 확장 마크다운 문법

출처: <https://developers.notion.com/guides/data-apis/enhanced-markdown> (확인 2026-08-04)
표준 마크다운을 **XML 유사 태그 + 속성 목록**으로 확장한 형식이다.
**들여쓰기는 탭**이고, 자식 블록은 부모보다 탭 하나 더 깊다.

```
<callout icon="🎯" color="blue_bg">
	Rich text
</callout>

<details color="Color">
<summary>토글 제목</summary>
	자식 블록 (들여쓰기 필수)
</details>

# 토글 헤딩 {toggle="true" color="Color"}
	자식 블록

<columns>
	<column>
		왼쪽
	</column>
	<column>
		오른쪽
	</column>
</columns>

> 인용 {color="Color"}
> 여러 줄은 Line 1<br>Line 2<br>Line 3 {color="Color"}

# 제목 {color="Color"}
- [ ] 안 한 것 {color="Color"}
- [x] 한 것 {color="Color"}

<table fit-page-width="true" header-row="true" header-column="false">
	<tr>
		<td>셀</td>
	</tr>
</table>
```

**색상 값**
- 글자: `gray` `brown` `orange` `yellow` `green` `blue` `purple` `pink` `red`
- 배경: `gray_bg` `brown_bg` `orange_bg` `yellow_bg` `green_bg` `blue_bg` `purple_bg` `pink_bg` `red_bg`

> ⚠️ **GFM 파이프 표를 입력으로 받는지는 공식 문서에 안 나와 있다.**
> 위 3번에서 실제로 올려보고 확인할 것. 지금 원고는 파이프 표로 돼 있다
> (붙여넣기 경로에서는 확실히 동작하고, API 경로만 미확인).

### 승격 제안 (스펙 확인 후 적용)

| 지금 | 승격 후 |
|---|---|
| `> **한 줄 요약 —** ...` (00_MAIN) | `<callout icon="🎯" color="blue_bg">` |
| `> ⚠️ Map1에 Wall 콜라이더 미배치` | `<callout icon="⚠️" color="red_bg">` |
| `> 💡 UI는 전부 런타임 Canvas 절차 생성` | `<callout icon="💡" color="yellow_bg">` |
| 05번 함정 노트의 주제별 표 | `<details>` 토글로 접어 스크롤 부담 감소 |
| 00_MAIN의 "5주 로드맵" + "계획 외 추가 구현" | `<columns>`로 나란히 배치 |
| 03번의 우선순위 목록 | `- [ ]` 체크박스 + `{color="red"}` |
