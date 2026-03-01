# SubManager Guide

## Current Runtime Managers
- `SceneRouter`: scene load bridge for `SceneManager.LoadSceneAsync`.
- `SceneFlowController`: Core/Lobby/Run/Loading transition orchestration.
- `AudioManager`: BGM/SFX playback and scene audio routing.
- `MusicDraftManager`: music track draft generation at run choice points.
- `LobbyHubManager`: lobby interaction hub and panel orchestration.
- `AddressablesService`: runtime asset loading facade (stub-safe fallback path).
- `RunSessionManager`: run clock/state machine, choice points, and end conditions.
- `RunSessionLegacyBridgeManager`: bridges domain run-session events to legacy event contracts.
- `RunDayNightLightingManager`: run-time directional light progression.
- `RunModifierManager`: run modifier stack and music synergy application.
- `MetaProgressionManager`: region/unlock/upgrade progression orchestration.
- `SectorThemeManager`: run sector theme assignment and marker sync.
- `RegionGateManager`: lobby/run region gate lock visuals and state.
- `PlayerBikeModifierLink`: applies modifier stack values to bike controller.
- `TrafficSystemManager`: road network snapshot for traffic systems.
- `TrafficSignalManager`: signal state simulation and visual updates.
- `TrafficNpcManager`: traffic NPC spawn/simulation lifecycle.
- `RoadQueryManager`: nearest-road query service and cache.
- `OrderFlowManager`: offer/order lifecycle and objective publishing.
- `FuelManager`: fuel drain/refuel states and station interactions.
- `FoodStateManager`: carry-food temperature/spill progression.
- `NpcCollisionPenaltyManager`: NPC collision penalty handling.
- `UIManager`: UI feature host and shared UI runtime state.
- `MusicChoiceManager`: music choice request/apply/auto-resolve coordination.
- `DeliveryManager`: delivery flow bridge events and compatibility path.
- `RatingManager`: rating delta aggregation and depletion events.
- `EconomyManager`: session/total currency state.
- `TelemetryManager`: runtime event ring-buffer diagnostics.

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
