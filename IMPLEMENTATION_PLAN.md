# Tiny Siege AR — Implementation plan

## Working method
One feature branch and one narrow acceptance checklist at a time. Do not merge until the feature has the best available validation; report any untested hardware behavior explicitly. Use `ARCHITECTURE.md` as decision source. Prefer user-approved changes over autonomous setup.

## Phase 0 — Baseline and project verification
- [ ] Confirm XR loaders by target in Unity Project Settings.
- [ ] Confirm XR Simulation environment loads and provides horizontal planes.
- [ ] Inspect Project Validation and Console.
- [ ] Verify Android package identifier, graphics API, minimum/target SDK and URP renderer.
- [ ] Create minimal build and run on ARCore-compatible Android (if device available).

## Phase 1 — `feature/ar-plane-detection` (FIRST)
- [ ] Create one AR scene using AR Session, XR Origin, AR camera and Plane/Raycast/Anchor managers.
- [ ] Use horizontal plane detection and visual confirmation.
- [ ] Preview a cube over valid raycast hits; invalid hits do nothing.
- [ ] Confirm input places exactly one cube, no click-through/repeated spawn.
- [ ] Anchor cube to placed position, check stable view tracking.
- [ ] Verify XR Simulation Play Mode and Android on actual device, document each test independently.

## Phase 2 — `feature/ar-board`
- [ ] Replace cube with low-poly board/fortress root and one anchor.
- [ ] Create low-cost virtual physics collider separate from visual plane/shadow receiver.
- [ ] Validate scale, pose and stable tracking in both environments.

## Phase 3 — `feature/catapult-physics`
- [ ] Implement aim, power and fire with Unity Rigidbody projectile.
- [ ] Validate collisions, gravity, no major tunneling and mobile performance.

## Phase 4 — `feature/destructible-objectives`
- [ ] Three objectives, limited ammo, single-shot state, match win/lose.
- [ ] Prebuilt destruction fragments / bounded active physics.
- [ ] A few EditMode tests of ammo, win/lose and duplicate destruction events.

## Phase 5 — `feature/ar-interaction-ui`
- [ ] Drag & Drop allowed item with explicit Rigidbody isKinematic lifecycle.
- [ ] Dedicated touch/mouse pointer UI blocker; verify no click-through.
- [ ] HUD for ammo, targets, power and fire; PlayMode checks where meaningful.

## Phase 6 — `feature/ar-lighting-shadows`
- [ ] Light Estimation and fallback.
- [ ] Implement actual URP Shadow Catcher with separate physical surface.
- [ ] Validate shadow compositing and culling on Android; capture evidence.

## Phase 7 — release and assessment
- [ ] Final stable Android APK.
- [ ] Confirm same gameplay flow in XR Simulation.
- [ ] Export complete GDD PDF and demo video including hierarchy, raycasts and culling masks.
- [ ] Record test evidence, limitations and final version/commit.

## Release blocker rule
Do not say a phase or project is fully validated until real Editor/Android execution has been observed. The assignment explicitly requires both targets to operate and detect planes.
