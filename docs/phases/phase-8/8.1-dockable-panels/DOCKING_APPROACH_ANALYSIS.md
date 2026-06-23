# 8.1a 도킹 패널 — 접근 방향 분석 및 문제 기록

> **작성일:** 2026-06-23  
> **목적:** 브랜치 롤백 이후, 이전 구현 시도에서 어떤 방향이 문제였는지 정리  
> **대상 브랜치:** `origin/failed-dockable-panels`

---

## 1. 시도했던 것과 작동한 것

`origin/failed-dockable-panels` 브랜치에서 8.1a 전체 구현이 완성된 상태였다.

| 구성 요소 | 파일 | 상태 |
|----------|------|------|
| `AeroDockFactory` | `src/Docking/AeroDockFactory.cs` | ✅ 빌드/테스트 통과 |
| 모델 클래스 6개 | `src/Docking/Model/*.cs` | ✅ 빌드/테스트 통과 |
| 툴 ViewModel 4개 | `src/Docking/ToolViewModels/*.cs` | ✅ 빌드/테스트 통과 |
| `EditorDocument` | `src/Docking/DocumentViewModels/` | ✅ 빌드/테스트 통과 |
| `LayoutPersistenceService` | `src/Services/` | ✅ 빌드/테스트 통과 |
| `ShellViewModel` 토글 커맨드 | | ✅ 구현 완료 |
| `MainWindow.axaml` | DockControl 배치 | ✅ 빌드 통과 |
| 단위 테스트 | `tests/Docking/` | ✅ 545개 전부 통과 |

---

## 2. 핵심 문제: DataTemplate 바인딩 방식

### 2-1. 원래 접근 방식 (현재 `master` 브랜치)

`MainWindow.axaml`의 `Window.DataTemplates`에서 각 툴/문서 타입을 View에 연결할 때
`ShellViewModel`의 프로퍼티를 직접 참조하는 방식을 사용했다:

```xml
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView DataContext="{Binding $parent[Window].DataContext.FileExplorerViewModel}"/>
</DataTemplate>
```

이 방식은 **컴파일은 되지만 런타임에서 바인딩이 끊긴다.**  
`$parent[Window].DataContext`는 Dock.Avalonia의 `DeferredContentControl` 안에서
visual tree가 재구성될 때 Window를 찾지 못하거나, DataContext 체인이
툴/문서 ViewModel 자체로 덮어쓰여진다.

### 2-2. `failed-dockable-panels`에서 시도한 수정

DataTemplate에서 바인딩을 제거하고, 코드비하인드에서 직접 `Context` 프로퍼티에
ViewModel을 주입하는 방식으로 전환했다:

```xml
<!-- DataTemplate에서 DataContext 바인딩 없음 -->
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView/>
</DataTemplate>
```

```csharp
// MainWindow.axaml.cs — WireViewModels()
case ExplorerTool explorer:
    explorer.Context = shell.FileExplorerViewModel;
    break;
```

`Context`는 `IDockable`에 있는 프로퍼티이고, `DeferredContentControl`이
DataTemplate을 렌더링할 때 이 값을 DataContext로 사용한다.

이 수정 후 로그상으로는 모든 툴에 Context가 정상적으로 주입됐고,
폴더 오픈도 동작했다 (`FileExplorerViewModel.LoadFolderAsync` 호출 확인됨).

### 2-3. 그러나 남은 문제: 패널 렌더링

Context 주입이 성공해도 다음 문제가 발생했다:

- **Explorer 패널 하나만 전체 화면을 차지** — 좌측 사이드바, 에디터 중앙, 하단 패널이
  모두 분리되어 보여야 하는데 Explorer만 렌더링됨
- Git, Problems, Output 패널은 존재하지만 화면에 나타나지 않음
- 에디터 영역이 비어있음

이것은 **Dock.Avalonia의 레이아웃 렌더링 내부 동작이 예상과 다르게 작동**하는 문제다.
13차례 이상 디버깅 시도를 했지만 원인을 특정하지 못했다:

| 시도 | 가설 | 결과 |
|------|------|------|
| `IsExpanded = true` 설정 | 툴독이 접혀있음 | 변화 없음 |
| `ActiveDockable` 설정 | 활성 탭 없음 | 변화 없음 |
| `GripMode = Visible` 설정 | 그립 숨김 | 변화 없음 |
| 좌측 컬럼 splitter 제거 | 트리 구조 문제 | 변화 없음 |
| Factory 명시적 설정 | DockControl과 연결 끊김 | 변화 없음 |
| 레이아웃 저장 파일 확인 | 이전 레이아웃 덮어씌움 | 저장 파일 없음 확인 |

---

## 3. 근본 원인 (추정)

### 3-1. `Dock.Avalonia.Themes.Simple` 미설치

`failed-dockable-panels`의 커밋 로그 중 하나가:

```
fix(docking): add missing Dock.Avalonia.Themes.Simple package
```

Dock.Avalonia는 기본 테마 없이는 `DockControl` 내부 컨트롤들이 렌더링되지 않거나
크기가 0이 된다. `Dock.Avalonia.Themes.Simple` 패키지가 없으면:

- 각 패널 컨테이너의 크기가 0으로 계산되거나
- ProportionalDock의 splitter가 작동하지 않거나
- ToolDock의 탭 스트립이 그려지지 않는다

`App.axaml`에서 `<SimpleTheme />`이나 `<FluentTheme />`만 있고
`<dock:DockTheme/>` 또는 `<StyleInclude Source="avares://Dock.Avalonia.Themes.Simple/..."/>`가
없으면 전체 dock 레이아웃이 빈 상태로 보일 수 있다.

**현재 `master`의 `App.axaml`에 이 테마 포함 여부를 반드시 확인해야 한다.**

### 3-2. `InitializeDockControl` 타이밍 문제

`failed-dockable-panels`의 최종 구현에서 타이밍 문제가 발견됐다:

- **원래 코드 (`master`):** `MainWindow` 생성자에서 `InitializeDockControl()` 호출
  → `DataContext`가 아직 설정되지 않은 시점이라 `WireViewModels()`에서
  `shell.FileExplorerViewModel` 등이 null
- **수정된 코드:** `Initialize(IMessageBus bus)`가 호출될 때 (= `DataContext` 설정 이후)
  `InitializeDockControl(shell)` 호출

현재 `master` 브랜치는 **원래의 생성자 시점 호출 방식**으로 되어있다.
`$parent[Window].DataContext` 바인딩은 이 타이밍 문제를 DataTemplate에서 늦게 해결하려는
우회책이었는데, Dock.Avalonia의 DeferredContentControl 안에서는 동작하지 않는다.

---

## 4. 현재 `master` 브랜치 상태와 해야 할 일

현재 `master`는 `failed-dockable-panels`에서 DataTemplate 방식을 취하고 있으나,
실제로 Context 주입이 올바르게 되는지 런타임 확인이 안 된 상태다.

재시작 전에 검증해야 할 것들:

```
[ ] App.axaml에 Dock.Avalonia 테마 스타일이 포함되어 있는가?
    → Dock.Avalonia.Themes.Simple 패키지 설치 + App.axaml에 StyleInclude 필요
[ ] MainWindow 생성자에서 DockControl을 초기화할 때 ShellViewModel이 null인가?
    → InitializeDockControl()을 Initialize() 내부로 이동해야 함
[ ] DataTemplate에서 $parent[Window].DataContext 바인딩이 Dock의 DeferredContentControl 
    내부에서 실제로 작동하는가?
    → Context 프로퍼티 직접 주입 방식으로 교체가 더 안전함
[ ] ProportionalDock의 Proportion 값이 설정되어 있는가?
    → 설정 없으면 모든 자식이 동일 비율로 분할되거나 0이 됨
```

---

## 5. 권장 재구현 방향

### DataTemplate 방식 (두 가지 옵션)

**Option A — Context 직접 주입 (안전, `failed-dockable-panels`에서 검증됨)**
```xml
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView/>
    <!-- DataContext 바인딩 없음 — 코드비하인드에서 Context 주입 -->
</DataTemplate>
```
```csharp
// Initialize() 내부에서 (DataContext 설정 이후)
explorer.Context = shell.FileExplorerViewModel;
```

**Option B — ReactiveUI Binding (대안)**
```xml
<DataTemplate DataType="dockTools:ExplorerTool">
    <views:FileExplorerView DataContext="{Binding Context}"/>
    <!-- Dock의 IDockable.Context 프로퍼티 참조 — parent 체인 없음 -->
</DataTemplate>
```

Option B가 더 선언적이고 MVVM에 맞지만,
Dock.Avalonia가 DataTemplate 렌더링 시점에 Context를 DataContext로 설정하는
타이밍이 보장되는지 검증이 필요하다.

### 초기화 순서 (반드시 지켜야 함)

```csharp
// App.axaml.cs — DataContext 설정
window.DataContext = shell;

// MainWindow.Initialize() — bus 구독 + DockControl 초기화
public void Initialize(IMessageBus bus, ShellViewModel shell)
{
    // 1. 버스 구독
    _bus = bus;
    // ...

    // 2. DockControl 초기화 (DataContext 설정 이후이므로 shell 사용 가능)
    InitializeDockControl(shell);
}

private void InitializeDockControl(ShellViewModel shell)
{
    DockControl.InitializeFactory = true;   // Layout 할당 전에 먼저
    DockControl.InitializeLayout = false;
    DockControl.Factory = layout.Factory!;

    shell.ActiveLayout = layout;
    WireViewModels(layout, shell);          // Context 주입

    DockControl.Layout = layout;            // 마지막에
}
```

---

## 6. 요약

| 문제 | 원인 | 해결책 |
|------|------|--------|
| 패널 내용 비어있음 | `$parent[Window].DataContext` 바인딩이 Dock 내부에서 끊김 | `Context` 직접 주입 방식으로 교체 |
| 초기화 타이밍 | 생성자에서 `InitializeDockControl()` 호출 → `DataContext` 아직 null | `Initialize()` 내부로 이동 |
| 레이아웃이 Explorer만 채움 | `Dock.Avalonia.Themes.Simple` 미설치 (의심) | 테마 패키지 추가 및 App.axaml에 StyleInclude |
| Proportion 미설정 | `ProportionalDock` 자식들의 `Proportion` 없음 | 좌측 0.25, 우측 0.75 등 명시 |
