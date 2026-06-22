# Phase 8 Brainstorming (Raw)

> 날짜: 2026-06-22
> 성격: Plan 이전 단계. 니 의도 + 내 분석을 그냥 기록.

---

## 니가 한 말 (핵심 발췌) — Your Words (Core Excerpts)

- "Phase 8은 내 의도가 반영되는 첫 구간" — "Phase 8 is the first section where my intent gets reflected."
- "VSC가 싫어서 이 프로젝트를 만든 거라 단순히 베끼는 건 의미 없음" — "I created this project because I hate VS Code, so simply copying it is meaningless."
- "윈도우 레이아웃은 VSC가 표준이니 따를 거임 (근데 이미 거의 완성)" — "Window layout follows the VS Code standard since it's the de facto standard (but it's already mostly done)."
- "웹사이트들처럼 동글동글하고 깔끔한 morphing 애니메이션으로 구성하고 싶음 (근데 CSS가 아님)" — "I want to compose it with rounded, clean morphing animations like websites have (but this isn't CSS)."
- "솔직히 최소 MVP는 JetBrains Rider 수준. 그 정도 디자인은 되야 의미 있음" — "Honestly, the minimum MVP is JetBrains Rider level. Only then does the design mean something."
- "테마는 유저가 직접 풀 커스텀 가능해야 함. 프리셋도 있고, 모든 색깔을 유저가 오버라이드 가능해야 함" — "The theme must be fully customizable by the user. Presets should exist, and the user must be able to override every single color."
- "morphing 애니메이션은 없어도, 부드러운 화면 전환이나 padding은 되야 함" — "Even without morphing animations, smooth screen transitions and padding are essential."
- "VSCode는 너무 투박함" — "VS Code is too clunky."

## 니 상황 — Your Situation

- 바이브 코더 (Vibe coder)
- 디자이너 아님 — Not a designer
- 에이전트에 전적으로 의존해야 함 — Must rely entirely on agents
- 근데 에이전트 특성상 디자인은 약함 — But by nature, agents are weak at design
- 바이브 코딩의 올바른 사이클: Plan → Review → Apply → Re-review — The correct cycle for vibe coding
- "그냥 생각나는 대로 적용하면 무조건 망함" — "Applying ideas as they come to mind always fails."

---

## 내가 분석한 것 — Analysis

### Phase 8이 이전 Phase들과 다른 점 — How Phase 8 Differs from Previous Phases

| 이전 Phase들 (0~7) — Previous Phases | Phase 8 |
|-----------------------|---------|
| 공학 (LSP, Git, Build) — 정답이 있음 (Engineering — right answer exists) | 디자인 — 정답이 없음 (Design — no right answer) |
| 에이전트가 규칙/테스트를 정해줌 (Agent defines rules/tests) | 니 취향이 반영되어야 함 (Your taste must be reflected) |
| 니가 오케스트레이션만 하면 됨 (You only orchestrate) | 니가 결정해야 하는 순간이 많음 (Many moments you must decide) |
| 결과가 명확 (동작함/안함) (Clear: works/doesn't) | 결과가 모호 (예쁨/안예쁨) (Ambiguous: pretty/ugly) |

### VS Code를 싫어한다는 게 의미하는 방향 — What "Hating VS Code" Means

- VS Code 복사 (Copy) 접근법 → 폐기 (Copy approach → discarded)
- VS Code 레이아웃만 표준이라 따름 (Only VS Code layout as standard is followed)
- 나머지는 "Rider 수준의 디자인 품질" (Everything else: "Rider-level design quality")
- "투박함"을 피해야 함 → 패딩, 트랜지션, 통일된 디자인 언어가 필수 (Avoid "clunkiness" → padding, transitions, unified design language essential)

### Rider 수준 디자인의 기술적 요구사항 (추정) — Rider-Level Technical Requirements

**80-100 color tokens needed:**
- 단순히 bg/fg/selection으로는 부족 (bg/fg/selection alone is insufficient)
- 스크롤바, 버튼 호버, 탭 액티브 언더라인, 인레이 힌트, 디버그 상태 등 (Scrollbar, button hover, tab active underline, inline hints, debug states etc.)

**Unified padding/spacing system:**
- 4px 그리드 기반 (4/8/12/16/24/32) — 4px grid base
- 버튼 내부 패딩, 리스트 아이템 높이, 섹션 간격이 모두 일관되어야 함 (Button padding, list item height, section gaps must all be consistent)

**Unified CornerRadius:**
- 버튼, 입력창, 드롭다운, 팝업 모두 같은 둥글기 (Buttons, inputs, dropdowns, popups all same rounding)

**Smooth transitions (200ms, ease):**
- 탭 전환, 패널 열기/닫기, hover 효과 (Tab switching, panel open/close, hover effects)

### 가능성 체크 (Avalonia 11.3) — Feasibility Check

**쉬움 — Easy:**
- CornerRadius (둥글기 — rounding)
- BoxShadow (그림자 — shadows)
- GradientBrush (그라데이션 — gradients)
- RenderTransform (호버 애니메이션 — hover animation)
- Inter 폰트 (이미 있음 — already imported)
- 반투명 배경 (rgba) — Semi-transparent backgrounds

**공수 좀 듦 — Medium effort:**
- 커스텀 페이지 트랜지션 (PageSlide는 쉬움) — Custom page transitions (PageSlide easy)
- 애니메이션 시스템 (CSS `transition: 0.2s` 한 줄 같은 건 없음) — Animation system (no CSS `transition: 0.2s` one-liner)

**어려움 또는 불가능 — Hard or impossible:**
- `backdrop-filter: blur()` (진짜 glassmorphism) — 네이티브 미지원 (true glassmorphism — no native support)
- CSS Grid/Flexbox와 다른 레이아웃 엔진 — Different layout engine from CSS Grid/Flexbox

---

## 논의된 구조적 결정 — Structural Decisions

### Phase 8.5 폐기 — Phase 8.5 Removed
- Phase 8.5를 없애고 전부 `docs/phases/phase-8/` 아래 kid phase로 통합 (Removed; everything integrated under `docs/phases/phase-8/` as kid phases)
- 8.1~8.8로 구성 (Now 8.1–8.8 + 8.9)

### Kid Phase 목록 (현재) — Kid Phase List

| # | Name (EN) | Scope (EN) |
|---|-----------|------------|
| 8.1 | Dockable Panels | Wire existing panels into Dock.Avalonia |
| 8.2 | Theme Engine | 80-100 color tokens + JSON full customization |
| 8.3 | Command Palette | Ctrl+Shift+P |
| 8.4 | Welcome Page | Landing tab |
| 8.5 | Icon Decision | Resolve TOFIX R3.1 |
| 8.6 | Settings Page | Single dialog, JSON persistence |
| 8.7 | Workspace Persistence | Remember state across restarts |
| 8.8 | Keybinding Display | Read-only |
| (new) | 8.9 Design System | Define padding/radius/shadow/transition rules |

### 8.2 Theme의 방향 재정의 필요 — Theme Direction Re-Defined

처음 제안 (Original): 15개 색깔 Light/Dark 프리셋 (15 colors, Light/Dark presets)
수정 제안 (Revised): 80~100개 color token + JSON 오버라이드 가능 + 프리셋 (80-100 color tokens + JSON override + presets)
→ VS Code의 workbench.colorCustomizations 같은 개념 (Same concept as VS Code's `workbench.colorCustomizations`)

### 현재 추정 워킹 리듬 — Estimated Working Rhythm

1. 내가 Plan 작성 (선택지 2~3개로 좁힘) — Agent writes Plan (narrowed to 2-3 options)
2. 니가 Review (고르거나 방향 제시) — You Review (choose or give direction)
3. 내가 Apply (정해진 대로만) — Agent Applies (strictly as decided)
4. 니가 Re-review — You Re-review

---

## 아직 결정되지 않은 것 — Undecided Items (→ Resolved 2026-06-22)

1. 8.2 Theme Engine vs 8.9 Design System — 어느 걸 먼저? (Which comes first?)
   → **결정: 병렬 진행 (Parallel).** 독립적이므로 동시에 작업 가능.

2. 8.9 Design System을 별도 kid phase로 추가할지, 각 kid phase 안에서 자연스럽게 정의할지 (Separate kid phase or naturally defined within each sub-phase?)
   → **결정: 분리된 8.9 (Separate).** 중앙 집중식 단일 진리 공급원으로 운영. 모든 kid phase가 참조.

3. "Rider 수준"의 구체적인 color token 목록은 아직 정의 안 됨 (Specific "Rider-level" color token list not yet defined)
   → **결정: 오픈 상태 유지.** 디자인 에이전트가 8.2 시작 시 리서치 후 제안.

4. 나머지 4개 (Welcome, Settings, Persistence, Keybinding) — 정말 필요한지, Optional로 뺄지 (Are the remaining 4 truly needed or optional?)
   → **결정: 전부 Phase 8에 포함.** Optional이지만 구현이 쉽고 품질을 결정하므로 Phase 8에 포함하는 것이 타당함.

---

## 2차 브레인스토밍 (2026-06-22) — Second Brainstorming

### VS Code의 에이전트 업데이트에 대한 반응 — Reaction to VS Code's Agent Update

VS Code가 최근 업데이트에서 기존 텍스트 영역은 건드리지 않고, 새 창을 별도로 만들어서 에이전트를 거기에 넣음.
→ 기존 유저는 안 쓰고, 진보 유저는 거꾸로 일반 창을 안 쓰게 됨 (비효율)
→ 니 반응: "나도 저걸 먼저 하고 싶었는데 그런 생각을 못 했다"

VS Code's recent update didn't touch the existing text area; it created a separate new window for the agent.
→ Conservative users don't use it; progressive users conversely stop using the regular window (inefficient)
→ Your reaction: "I wanted to do that first but didn't think of it."

### "따라가고 싶지 않다" — Aero의 정체성 / "I Don't Want to Follow" — Aero's Identity

> "VSC를 그냥 Avalonia로 만들었다는데 의의가 생기고 끝"
> "The meaning ends at 'we just recreated VSC in Avalonia'"

순수한 기술 이식 (VSC를 Avalonia로) 은 의미가 없다.  
Aero만의 차별점이 있어야 함.

Pure technology porting (VSC in Avalonia) has no meaning.  
Aero must have its own differentiation.

### 등장한 아이디어: 리눅스 DE 같은 선택권 — Idea: Linux DE-Like Choice

- Hyperland 쓸지 KDE Plasma 쓸지 유저가 고르듯이 (Like choosing between Hyperland vs KDE Plasma)
- Aero에서도 유저가 자신의 레이아웃을 설정 가능하게 (In Aero, users configure their own layout)
- 특히 멀티 에이전트 시대에는 단일 채팅창 1개로는 부족함 (Multi-agent era: single chat window is insufficient)
- "극진보 유저" — 여러 에이전트를 동시에 쓰고, orchestration 하는 유저를 만족시켜야 함 ("Ultra-progressive users" — multi-agent + orchestration must be satisfied)
- Hyperland의 단점 (창을 내 맘대로 조절 못함) 은 반드시 고쳐야 함 (Hyperland's flaw — can't freely adjust windows — must be fixed)

### 등장한 Aero 디자인 철학 (초안) — Aero Design Philosophy (Draft)

> "Aero는 IDE일 뿐만 아니라, 유저가 자신의 오케스트레이션 환경을 직접 구성할 수 있는 플랫폼이다"
> "Aero is not just an IDE, but a platform where users configure their own orchestration environment."

| 유저 타입 (User Type) | 니즈 (Need) | Aero의 대응 (Response) |
|-----------|------|------------|
| 보수 유저 (Conservative) | 전통적인 IDE (텍스트 중앙, 에이전트 오른쪽) — Traditional IDE (text center, agent right) | 프리셋 레이아웃 제공 (Preset layout provided) |
| 진보 유저 (Progressive) | VS Code 스타일 (에이전트 메인) — VS Code style (agent as main) | 프리셋 레이아웃 제공 (Preset layout provided) |
| 극진보 유저 (Ultra-progressive) | 멀티 에이전트 뷰 (분할, 파이프라인, 여러 창) — Multi-agent view (split, pipeline, multiple windows) | Dock.Avalonia 기반 풀 커스텀 가능 (Full custom via Dock.Avalonia) |
| orchestration 유저 | 에이전트 1 → 에이전트 2 자동 연결 (Auto-route Agent A → Agent B) | Phase A4 (Agent-to-Agent Pipeline) |

### Phase 8에 미치는 영향 — Impact on Phase 8

- Dock.Avalonia (8.1)는 단순히 "패널을 움직이게" 하는 게 아니라, "유저가 자신의 레이아웃을 구성할 수 있는 플랫폼" 이라는 철학을 담아야 함 (8.1 isn't just "making panels movable" — it must embody user-configured layout philosophy)
- 8.6 Settings 는 레이아웃 프리셋 선택 기능을 포함해야 함 (8.6 Settings must include layout preset selection)
- 8.2 Theme Engine 은 단순 색깔 변경을 넘어서, "유저가 IDE의 모든 측면을 커스텀 가능하게" 하는 철학의 일부 (8.2 is part of "let users customize every aspect of the IDE")

### VS Code의 단점 — VS Code's Flaws

- 하나의 레이아웃을 강제함 (Forces a single layout)
- 유저가 "에이전트 창 쓰다가 일반 창 써야지" 할 수 없음 (두 개가 분리되어 있어서) (User can't switch between agent and regular windows — they're separated)
- 너의 말: "창이 2개라 보수 유저는 에이전트 창 안쓰고, 진보는 거꾸로 일반 창 안써서 약간 비효율"
  (Your words: "With two windows, conservative users don't use agent window, progressive don't use regular — inefficient")

### 이 철학이 의미하는 바 — What This Philosophy Means

> "Aero는 단일 레이아웃을 강제하지 않고, 유저가 자신의 워크플로우에 맞게 IDE를 구성할 수 있게 한다."
> "Aero does not force a single layout; it lets users configure the IDE to fit their workflow."

Phase 8의 모든 결정은 이 질문으로 귀결됨 (Every Phase 8 decision comes down to this question):
"이게 유저에게 선택권을 주는가? 아니면 강제하는가?" ("Does this give the user choice, or does it force them?")

### Layout Modes 결정 (2026-06-22) — Layout Modes Decision

Aero는 두 가지 레이아웃 모드를 제공함 (Aero provides two layout modes):

| 모드 (Mode) | 설명 (Description) | 기본값? (Default?) |
|------|------|---------|
| **Tile Mode** | Hyperland 스타일. 패널이 자동으로 배치됨. 키보드 네비게이션에 최적화. (Auto-arranged, keyboard-nav optimized) | ✅ 기본값 (Default) |
| **Freeform Mode** | 전통적인 IDE 방식. 유저가 패널을 직접 드래그. Dock.Avalonia 풀 기능. (Traditional: user drags panels manually) | 설정에서 전환 (Switchable in settings) |

핵심 원칙 (Core principles):
- Tile Mode여도 "내 맘대로 창 조절"이 가능해야 함 (Hyperland의 단점 보완) — Even in Tile Mode, manual adjustment must be possible
- 유저가 설정에서 두 모드를 전환 가능해야 함 (User must be able to switch modes in settings)
- 모드 전환이 IDE 재시작 없이 즉시 적용되어야 함 (Mode switching must be instant, no restart)

### Tear-Away Windows (2026-06-22)

패널을 메인 창 밖으로 빼내서 **독립된 OS 창**으로 분리 가능해야 함. (Panels detachable into independent OS windows)
- Chrome 탭 분리와 같은 개념 (Like Chrome tab tear-away)
- Tile Mode와 궁합이 좋음: 빼낸 창은 OS 레벨에서 타일링 WM으로 관리 (Complements Tile Mode: managed by OS tiling WM)
- 필요 없으면 다시 메인 창에 도킹 (Re-dockable when no longer needed)

8.1 Dock 범위에 직접 영향 (Direct impact on 8.1 scope):
- 단순 "패널 이동" → "패널을 메인 창에서 분리해서 별도 OS Window로 만들 수 있음" (From "panel moving" → "panels can be separated into standalone OS Windows")
- Avalonia: 런타임에 `Window` 생성 + 컨텐츠 이동 방식으로 구현 가능 (Runtime `Window` creation + content transfer)

### Tile + Stack (2026-06-22)

Tile Mode라고 해서 무조건 분할만 있는 게 아님. **타일링 + 탭(stack/merge)이 혼합**되어야 함.
(Tile Mode doesn't mean only splitting. **Tiling + tabs must be mixed.**)

- 패널을 옆에 붙이면 타일링 (좌우 분할) — Placed side-by-side → tiling (split)
- 패널을 같은 공간에 겹쳐서 놓으면 탭으로 합쳐짐 (notebook 스타일) — Placed in same space → merged into tabs (notebook style)
- VS Code의 editor groups / Windows 11 snap layouts 과 유사 (Similar to VS Code editor groups / Windows 11 snap layouts)
- 타일 + 탭 자유롭게 혼합 가능해야 실용적임 (Free mixing of tiles + tabs must be possible)
- Hyperland의 단점 (타일 강제, 맘대로 창 조절 불가) 을 의식적으로 피하는 설계 (Consciously avoids Hyperland's flaw: forced tiling, no manual adjustment)
