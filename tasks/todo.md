# Survival Pressure Layer (Loden)

Plan: water meters, tools/tilling/scything, lose condition, weather foundation.
Lane: everything that threatens the farm. Stay out of inventory internals, harvest/eat flow, day clock internals.

- [x] Step 0: Sync branches (merged origin/main and origin/Ryan'sBranch into loden-work-branch)
- [x] Step 1: Per-plant water meter
  - [x] Plant data: waterDepletionRate, resistances, witheredSpr
  - [x] FarmPlot: waterLevel float, repeatable watering, growth gated on water, Withered state
  - [x] PlotWaterBar: world-space bar above plot (runtime-built, just add the component)
- [x] Step 2: Tools, tilling, scything
  - [x] PlayerTools: tool enum (Hoe, Seeds, WateringCan, Scythe), 1-4 to switch, E to use
  - [x] FarmPlot: Untilled starting state, hoe tills, scythe clears any crop
  - [x] Moved E-key handling out of PlayerPlanting, seeds now consumed via UseSeed()
- [x] Step 3: Lose condition (no living crops + no seeds = game over via DayManager)
- [x] Step 4: WeatherManager foundation (Clear, Heatwave, Rainstorm, Windstorm + plant resistances)
- [x] Batch-mode compile verified: 0 errors, 0 warnings
- [ ] In-editor playtest (needs scene wiring below)

## Editor wiring still needed (manual, in Unity)

1. Add PlayerTools component to the Player (next to PlayerPlanting).
2. Add PlotWaterBar component to each FarmPlot object.
3. In BaseDataPlants asset: set waterDepletionRate and witheredSpr per plant
   (all 3 plants: ids 1, 2, 3).
4. Optional: assign untilledSprite on FarmPlots (falls back to emptySoilSprite).

## Parked for later: weather

WeatherManager.cs is written but dormant. It does nothing until a WeatherManager
object is added to the scene (FarmPlot null-checks it). When week 2 starts:
add the scene object, assign a TMP label, and set the per-plant resistances
in BaseDataPlants.

## Review

- Existing scene plots start Untilled automatically (enum reorder maps old
  serialized 0/Empty to Untilled, which is the intended starting state).
- FarmPlot.Interact was replaced by FarmPlot.UseTool(tool, inventory); tell
  Johnny since he owned the old call site (it lived in PlayerPlanting.Update).
- Seeds are now actually consumed on planting (UseSeed was commented out before).
- Withering: growing crop at 0 water for 10s (witherGracePeriod) withers;
  weather stress can also wither; scythe to clear.
- Weather: rolls each day period change; Heatwave scales water depletion by
  plant heatResist, Rainstorm refills water but stresses non rain-resistant
  plants, Windstorm stresses non wind-resistant plants.
- Also swapped deprecated FindFirstObjectByType calls for FindAnyObjectByType
  (Unity 6000.5 deprecation) in DayManager, WeatherManager, HungerBar.
- Day/night alignment (7/17): Ryan's work (clock UI, period growth multipliers,
  3 plant types) was already fully merged; his PR #7 landed the same commit on
  main. FarmPlot now falls back to a 1x growth multiplier if no DaySystem is in
  the scene. R key cycles seed type 1-3 so all of Ryan's plants are plantable
  (scene previously locked the player to plant id 1).
- Continuous game (7/17, per team decision in Discord): DaySystem no longer
  stops at 11 PM; the clock wraps at midnight into the next day (day counter
  added, endHour removed). 12 AM to morningStart now counts as Night.
- Death rework: hunger death no longer reloads the scene. DayManager keeps a
  checkpoint of the full game state taken at each midnight (plot states, water,
  growth, inventory, seed selection, player position). On death it fades out,
  rewinds the clock to 12 AM of the current day (DaySystem.ResetToMidnight),
  restores the checkpoint, and respawns with 50% hunger (tunable via
  hungerAfterDeath). On day 1 the checkpoint is the game-start state since no
  midnight has happened yet.
- The farm-dead lose condition (no living crops, no seeds) still does a full
  scene reload since that run is unrecoverable.
