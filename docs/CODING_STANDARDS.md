# SWITCHCAST — CODING STANDARDS & GUIDELINES

---

## 1. LANGUAGE & RUNTIME TARGETS

- **Language**: C# 12.0+
- **Runtime**: .NET 8.0+ Windows Desktop (`net8.0-windows10.0.19041.0` or later)
- **UI Framework**: WinUI 3 / Windows App SDK
- **Nullability**: `<Nullable>enable</Nullable>` enforced across all projects.

---

## 2. NAMING CONVENTIONS

| Element | Format | Example |
| :--- | :--- | :--- |
| **Classes / Structs** | PascalCase | `PresentationCoordinator`, `WindowSource` |
| **Interfaces** | IPascalCase | `ICaptureService`, `IWindowDiscoveryService` |
| **Methods** | PascalCase | `SwitchToSourceAsync()`, `RefreshWindows()` |
| **Async Methods** | PascalCase + `Async` | `StartCaptureAsync()`, `DisposeAsync()` |
| **Properties** | PascalCase | `ActiveSource`, `IsPresentationActive` |
| **Events** | PascalCase | `SourceChanged`, `FrameArrived` |
| **Parameters** | camelCase | `targetSource`, `cancellationToken` |
| **Local Variables** | camelCase | `framePool`, `windowHandle` |
| **Private Fields** | `_camelCase` | `_captureSession`, `_presentationCoordinator` |
| **Constants** | PascalCase | `MaxQueuedSources`, `DefaultFrameRate` |

---

## 3. FILE & CODE ORGANIZATION

1. **One Responsibility Per Class**: Each class must have a single, cohesive responsibility.
2. **File Scoped Namespaces**: Use file-scoped namespace declarations:
   ```csharp
   namespace SwitchCast.Core.Models;
   ```
3. **No Monolithic Utility Classes**: Avoid creating dumping grounds like `Utils.cs` or `GeneralHelper.cs`. Structure helpers into domain-specific, static extensions (e.g., `WindowInteropExtensions.cs`).
4. **Ordering within Types**:
   - Constants & Static Fields
   - Private Instance Fields
   - Constructors & Factory Methods
   - Public Properties
   - Public Methods
   - Private / Protected Internal Helper Methods
   - IDisposable Implementation

---

## 4. NULL SAFETY & DEFENSIVE CODING

- **Nullable Annotations**: Treat compiler nullability warnings (`CS8600`-`CS8604`) as critical. Do not suppress null warnings with the null-forgiving operator (`!`) unless backed by an explicit invariant check.
- **Boundary Validation**: Validate all public method parameters using modern `ArgumentNullException.ThrowIfNull(param)`:
   ```csharp
   public void SelectSource(CaptureSource source)
   {
       ArgumentNullException.ThrowIfNull(source);
       // ...
   }
   ```

---

## 5. ASYNCHRONOUS PROGRAMMING RULES

1. **Never Block UI Threads**: Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on asynchronous calls from the UI thread. Use `await`.
2. **No `async void`**: Use `async Task` or `async ValueTask` exclusively. The ONLY allowed exception is UI event handler signatures (e.g., `private async void OnSwitchButtonClicked(object sender, RoutedEventArgs e)`), which MUST wrap their entire body in a `try/catch` block.
3. **Cancellation Token Propagation**: Always propagate `CancellationToken` through asynchronous pipelines:
   ```csharp
   public async Task StartCaptureAsync(CaptureSource source, CancellationToken cancellationToken = default);
   ```
4. **Task Error Observation**: Never launch detached fire-and-forget tasks without exception observation.

---

## 6. EVENT SUBSCRIPTION & MEMORY LEAK PREVENTION

1. **Deterministic Unsubscription**: Whenever an object subscribes to an event on a longer-lived publisher, it MUST unsubscribe in its `Dispose()` or deactivation method.
2. **Weak Event Patterns**: For loose UI bindings across decoupled components, prefer `CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger`.

---

## 7. NATIVE COM & DIRECT3D RESOURCE MANAGEMENT

1. **Deterministic Disposal**: All COM pointers, WinRT capture objects, and DirectX textures MUST be released or disposed explicitly.
2. **Safe Interop Wrappers**: Encapsulate raw Win32 pointers and `IntPtr` / `HWND` / `HMONITOR` handles inside safe typed records or structs.
3. **Zero Per-Frame Allocations**: The hot capture callback loop (`Direct3D11CaptureFramePool.FrameArrived`) must not allocate new heap objects, strings, or LINQ queries. Reuse pre-allocated frame structs and DirectX buffers.

---

## 8. ERROR HANDLING & DIAGNOSTIC LOGGING

1. **Expected vs Exceptional Errors**: Handle expected domain conditions (e.g., a window was closed by the user) cleanly via state transitions without throwing uncaught exceptions.
2. **Preserve Stack Traces**: When re-throwing exceptions, use `throw;` rather than `throw ex;`.
3. **No Sensitive Content in Logs**: Logs must contain operational diagnostics (session IDs, timings, error codes) but MUST NEVER contain:
   - Captured pixel arrays or frame data.
   - Window titles that contain sensitive information (passwords, tokens, personal chats).
   - User credentials or file system paths with private identifiers.

---

## 9. THIRD-PARTY DEPENDENCY POLICY

- **Preference for First-Party APIs**: Prioritize built-in .NET 8, Windows App SDK, and WinRT APIs.
- **Strict Approval**: Any proposed third-party NuGet package must be justified in an Architecture Decision Record (ADR) detailing necessity, maintenance status, and security implications.
