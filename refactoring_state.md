# Refactoring State Tracker

## Current Phase: 1 — Architecture & SOLID Principles (Decoupling)

### Phase 1 Progress

| Step | Description | Status |
|------|-------------|--------|
| 1.1 | Create `IOscProvider` + `OscBuildContext` + `OscSegment` types | 🔄 In Progress |
| 1.2 | Create `OscOutputBuilder` (replaces OSCController.BuildOSC) | ⬜ Pending |
| 1.3 | Create adapter providers for all 12 modules | ⬜ Pending |
| 1.4 | Wire BuildOSC → OscOutputBuilder delegation | ⬜ Pending |
| 1.5 | Register providers in DI | ⬜ Pending |
| 1.6 | Remove old Add* methods from OSCController | ⬜ Pending |
| 2.1 | Break ScanLoopService: extract SaveAllDataSync → AppLifecycleService | ⬜ Pending |
| 2.2 | Break ScanLoopService: extract TTS → TtsPlaybackService | ⬜ Pending |
| 3.1 | Eliminate Lazy<T> circular deps (events/messaging) | ⬜ Pending |
| 4.1 | Implement IMessenger pub/sub | ⬜ Pending |

### Key Design Decisions (from rubber-duck critique)
- Providers are **budget-aware**: `TryBuild(OscBuildContext)` not `GetOscOutput()`
- **Adapter pattern**: providers wrap existing modules, don't force modules to implement interface
- **Status cycling** stays as pre-build step, not inside provider
- Builder returns **pure result** (OscBuildResult); presenter updates UI
- **SortKey vs UiKey** split: sort order uses "Component"/"Network", opacity uses "ComponentStat"/"NetworkStatistics"
- VR/Desktop gating **centralized** in provider metadata, not duplicated

### Files Created This Phase
- `Core/Osc/IOscProvider.cs`
- `Core/Osc/OscBuildContext.cs`
- `Core/Osc/OscSegment.cs`
- `Core/Osc/OscBuildResult.cs`
- `Core/Osc/OscOutputBuilder.cs`
- `Core/Osc/Providers/*.cs` (12 adapter providers)
