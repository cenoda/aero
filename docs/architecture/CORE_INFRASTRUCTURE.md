# Core Infrastructure Patterns

## MVVM — ReactiveUI 사용

모든 ViewModel은 ReactiveUI의 `ReactiveObject`를 상속한다.
직접 `ObservableObject`를 만들지 않는다.

```csharp
// ViewModel 예시
public class FileExplorerViewModel : ReactiveObject {
    // [Reactive] 어트리뷰트 하나면 자동으로 변경 알림 동작
    [Reactive] public string CurrentPath { get; set; } = "";

    // WhenAnyValue로 값 변화에 반응
    public FileExplorerViewModel() {
        this.WhenAnyValue(x => x.CurrentPath)
            .Subscribe(path => LoadFiles(path));
    }
}
```

유니티로 치면 — `[SerializeField]` 달면 Inspector에 나타나는 것처럼,
`[Reactive]` 달면 값이 바뀔 때 UI가 자동으로 업데이트된다.

### Command (버튼 클릭 등)

```csharp
public ReactiveCommand<Unit, Unit> OpenFileCommand { get; }

public MyViewModel() {
    OpenFileCommand = ReactiveCommand.CreateFromTask(OpenFileAsync);
}
```

## DI — Microsoft.Extensions.DependencyInjection 사용

수동 `ServiceLocator`를 만들지 않는다.
.NET 표준 DI 컨테이너를 사용한다.

```csharp
// Program.cs — 서비스 등록
var services = new ServiceCollection();

services.AddSingleton<IMessageBus, MessageBus>();
services.AddSingleton<DocumentManager>();
services.AddSingleton<SettingsService>();
services.AddTransient<FileExplorerViewModel>();

var provider = services.BuildServiceProvider();
```

```csharp
// 생성자 주입 — 직접 꺼내지 않고 필요한 것을 선언만 하면 자동으로 들어옴
public class DocumentManager {
    public DocumentManager(IMessageBus bus, SettingsService settings) {
        // DI가 알아서 주입해 줌
    }
}
```

## MessageBus (이벤트 버스)

ViewModel과 Service가 서로 직접 참조하지 않고 메시지로 소통한다.
유니티의 `SendMessage()` 또는 이벤트 시스템과 비슷한 개념.

```csharp
public interface IMessageBus {
    void Subscribe<T>(Action<T> handler);
    void Publish<T>(T message);
    void Unsubscribe<T>(Action<T> handler);
}

// 메시지 타입 (단순 레코드)
public record DocumentOpened(TextDocument Document);
public record DocumentClosed(TextDocument Document);
public record ActiveDocumentChanged(TextDocument? Document);
public record BuildStarted(string Project);
public record BuildFinished(int ExitCode, string Output);
public record ThemeChanged(string ThemeName);
public record FolderOpened(string Path);
```

## Startup Sequence

```
Program.Main()
    → BuildAvaloniaApp()
    → OnFrameworkInitializationCompleted()
        → ServiceCollection에 서비스 등록
        → provider.GetRequiredService<ShellViewModel>()
        → new MainWindow { DataContext = shellViewModel }
        → Show window
```
