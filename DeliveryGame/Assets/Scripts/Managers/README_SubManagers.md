# SubManager Guide

## Current Runtime Managers
- `SceneRouter`: scene load bridge for `SceneManager.LoadSceneAsync`.
- `SceneFlowController`: Core/Lobby/Run/Loading transition orchestration.
- `AudioManager`: low-level audio stub API (`PlayBgm/StopBgm`).
- `SoundManager`: sound policy layer (scene BGM routing + delivery SFX routing).
- `AddressablesService`: runtime asset loading facade (stub-safe fallback path).
- `RunSessionManager`: 7-minute run clock, last-order phase, end conditions.
- `UIManager`: runtime UI state and toast counters for debug/runtime sync.
- `MusicChoiceManager`: music-choice timing windows and auto-resolve fallback.
- `DeliveryManager`: order/delivery demo flow lifecycle for Run scene.
- `RatingManager`: rating delta aggregation and depletion signal.
- `EconomyManager`: run rewards settlement and total currency state.
- `TelemetryManager`: lightweight runtime event ring-buffer diagnostics.

## ISubManager Contract
- `Initialize(CoreContext ctx)`: one-time setup only.
- `Tick(float unscaledDeltaTime)`: runtime update path.
- `Shutdown()`: reverse cleanup for all runtime resources.

## Init Order Rule
- `CoreRoot` sorts managers by:
1. `InitOrder` ascending
2. `Name` ascending (`StringComparison.Ordinal`)
- Keep `InitOrder` explicit and stable for deterministic startup.

## EventBus Subscription Rule
- Prefer `SubManagerBase` + `Subs.Add(Events, handler)`.
- Do not call `Events.Subscribe(...)` without registering matching unsubscribe.
- `Subs.Clear()` is automatically called during `Shutdown()`.

## Shutdown Rule
- Always release runtime state in `OnShutdown()`:
- cancel timers/jobs
- clear transient caches
- release handles/resources (Addressables, audio handles, etc.)

## Runtime Performance Rule
- `Tick` path: no LINQ, no avoidable allocation.
- Build lists, sorting, and registration only during bootstrap/initialize.
