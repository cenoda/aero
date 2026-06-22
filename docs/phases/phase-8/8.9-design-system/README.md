# 8.9 — Design System (Foundation)

> ⚠️ **Execute this sub-phase FIRST.** All other Phase 8 sub-phases (8.1–8.8) reference this design system for spacing, corner radius, shadows, transitions, typography, borders, and color token naming. Establishing this foundation early prevents rework.

> 디자인 에이전트가 채울 영역. 여기서는 범위만 정의함. 구체적인 값은 아래에 명시됨.

## Goal

Aero 전체 UI의 시각적 일관성을 정의하는 foundation.  
Phase 8의 모든 kid phase (8.1~8.8)는 이 디자인 시스템을 기준으로 구현됨.

## Scope

### 1. Spacing Scale (padding/margin/gap)

전체 UI에서 사용되는 간격의 계층 구조.
- Base unit: `4px` (4px 그리드)
- Scale: `4px`, `8px`, `12px`, `16px`, `24px`, `32px`
- 적용 대상: 버튼 내부 패딩, 리스트 아이템 높이, 섹션 간격, 패널 마진

### 2. Corner Radius

일관된 둥글기 레벨 (Apple / Rider 느낌).
- Button: `6px`
- Input field: `6px`
- Panel: `8px`
- Popup/Dialog: `10px`
- Tab: `4px` (상단만, 하단은 0)
- Scrollbar thumb: `4px`

### 3. Shadow System

깊이감을 표현하는 그림자 계층 (Avalonia BoxShadow).
- Subtle (surface-level): `0 1px 2px rgba(0,0,0,0.08)`
- Medium (elevated): `0 4px 12px rgba(0,0,0,0.12)`
- Popup (modal/dialog): `0 8px 24px rgba(0,0,0,0.16)`
- 적용 조건: 호버, 포커스, 모달 상태

### 4. Transition Timing

부드러운 전환을 위한 애니메이션 기본값 (Avalonia Transitions).
- Duration: `200ms`
- Easing: `CubicOut` (ease-out)
- 적용 대상: 호버, 포커스, 패널 열기/닫기, 탭 전환, 색상 변경

### 5. Typography

- Font family: `Inter` (이미 있음)
- Size scale: `11px` (status bar), `12px` (UI labels), `13px` (body), `14px` (tab titles), `16px` (heading)
- 적용 대상: 본문, 코드, UI 레이블, 탭 제목, 상태 표시줄

### 6. Border System

- Border width: `1px` (기본)
- Border style: `Solid`
- 적용 대상: 패널 구분선, 입력창, 테이블 그리드

### 7. Color Token Naming Convention

- Token 구조: `{area}.{property}` e.g. `editor.background`, `panel.border`, `button.hoverBackground`
- Phase 8.2 Theme Engine 에서 사용될 key의 명명 규칙을 여기서 정의

## Output

이 디자인 시스템은 `src/Styles/` 아래에 다음 파일들로 구체화됨:
- `src/Styles/Spacing.axaml`
- `src/Styles/CornerRadius.axaml`
- `src/Styles/Shadows.axaml`
- `src/Styles/Transitions.axaml`
- `src/Styles/Typography.axaml`
- `src/Styles/Borders.axaml`

각 파일은 Avalonia ResourceDictionary 로 정의되며, Theme (8.2) 위에서 동작함.

## Notes

- 구체적인 값은 디자인 에이전트가 작성함
- Apple / Rider 느낌을 기준으로 함
- VS Code 스타일 (flat, 각짐, 고대비) 과 반대 방향
