using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TOR_Core.Extensions;

namespace TOR_EngineerCareer
{
    internal sealed class EngineerArtilleryControlMissionLogic : MissionLogic
    {
        private const float InitialCameraHeight = 65f;
        private const float MinCameraHeight = 35f;
        private const float MaxCameraHeight = 120f;
        private const float CameraTiltFactor = 0.35f;
        private const float CameraMoveSpeed = 42f;
        private const float MouseWorldWidthAtMinZoom = 55f;
        private const float MouseWorldWidthAtMaxZoom = 145f;
        private const float ImpactRadius = 5f;
        private const float MaxCommandRange = 600f;
        private const float TrajectoryRibbonWidth = 1.8f;
        private const float ImpactCircleRibbonWidth = 1.15f;
        private const float TrajectoryVisualLift = 1.6f;
        private const float ImpactCircleVisualLift = 0.75f;
        private const float PendingAimTimeout = 1.25f;
        private const uint ValidColor = 0xFF20FF40;
        private const uint BlockedColor = 0xFFFF3030;
        private const uint AllyContourColor = 0xFF35B6FF;
        private const uint ArtilleryContourColor = 0xFFFFE060;
        private const uint PanelColor = 0xDDE8E8E8;

        private static readonly InputKey ToggleControlModifierKey = InputKey.LeftAlt;
        private static readonly InputKey ToggleControlKey = InputKey.X;
        private static readonly InputKey FireKey = InputKey.LeftMouseButton;
        private static readonly FieldInfo AiRequestsShootField = typeof(RangedSiegeWeapon).GetField("_aiRequestsShoot", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CalculateLocalAimMethod = typeof(RangedSiegeWeapon).GetMethod("CalculateLocalDirectionAndLocalAngleToShootTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly HashSet<MissionObject> _initialMissionObjects = new();
        private readonly List<RangedSiegeWeapon> _playerArtillery = new();
        private readonly HashSet<Agent> _highlightedAllies = new();
        private readonly HashSet<RangedSiegeWeapon> _highlightedArtillery = new();
        private readonly EngineerArtilleryControlVM _viewModel;

        private bool _isControlModeActive;
        private bool _wasObjectInteractionEnabled = true;
        private bool _wasItemInteractionEnabled = true;
        private bool _wasMainAgentItemUseDisabled;
        private Agent.MovementControlFlag _previousMovementFlags = Agent.MovementControlFlag.None;
        private Vec2 _previousMovementInputVector = Vec2.Zero;
        private EquipmentIndex _previousPrimaryWieldedItemIndex;
        private EquipmentIndex _previousOffhandWieldedItemIndex;
        private int _nextWeaponIndex;
        private float _cameraHeight = InitialCameraHeight;
        private Vec3 _cameraTarget = Vec3.Zero;
        private Vec3 _cameraForward = new(0f, 1f, 0f);
        private Vec3 _cameraRight = new(1f, 0f, 0f);
        private Vec3 _aimTarget = Vec3.Invalid;
        private Vec3 _pendingFireTarget = Vec3.Invalid;
        private GameEntity _overlayEntity;
        private Mesh _overlayMesh;
        private MatrixFrame _previousCameraFrame = MatrixFrame.Identity;
        private Vec3 _previousCustomCameraTargetLocalOffset = Vec3.Zero;
        private Vec3 _previousCustomCameraLocalOffset = Vec3.Zero;
        private Vec3 _previousCustomCameraLocalOffset2 = Vec3.Zero;
        private Vec3 _previousCustomCameraGlobalOffset = Vec3.Zero;
        private Vec3 _previousCustomCameraLocalRotationalOffset = Vec3.Zero;
        private bool _previousCustomCameraIgnoreCollision;
        private float _previousCustomCameraFixedDistance;
        private float _previousCustomCameraFovMultiplier;
        private RangedSiegeWeapon _pendingFireWeapon;
        private float _pendingFireElapsed;
        private AimState _aimState = AimState.NoTarget;

        private enum AimState
        {
            NoTarget,
            Valid,
            Blocked,
            OutOfRange
        }

        public EngineerArtilleryControlMissionLogic()
        {
            _viewModel = new EngineerArtilleryControlVM(ToggleControlMode, TryFireNextReadyWeapon);
        }

        public EngineerArtilleryControlVM ViewModel => _viewModel;

        public override void AfterStart()
        {
            SnapshotInitialMissionObjects();
            RefreshArtilleryList();
            RefreshViewModel();
        }

        public override void OnMissionTick(float dt)
        {
            if (Mission?.MissionEnded == true || Mission?.MissionIsEnding == true)
            {
                if (_isControlModeActive)
                {
                    ExitControlMode(null);
                }

                return;
            }

            RefreshArtilleryList();
            if (_isControlModeActive)
            {
                BlockMainAgentControl(true);
            }

            HandleInput(dt);

            if (_isControlModeActive)
            {
                if (!CanEnterControlMode())
                {
                    ExitControlMode("Artillery control unavailable.");
                    return;
                }

                UpdateCameraTarget(dt);
                BlockMainAgentControl(true);
                UpdateAimTargetFromMouse();
                SuppressPlayerArtilleryAutoFire();
                UpdateAimState();
                ProcessPendingFire(dt);
                SuppressPlayerArtilleryAutoFire();
            }

            RefreshViewModel();
        }

        public override void OnPreDisplayMissionTick(float dt)
        {
            if (!_isControlModeActive)
            {
                return;
            }

            BlockMainAgentControl(true);
            UpdateHighlights();
            RenderAimPreview();
            RenderControlPanel();
            ApplyControlCamera();
        }

        protected override void OnEndMission()
        {
            if (_isControlModeActive)
            {
                ExitControlMode(null);
            }
        }

        public override void OnRemoveBehavior()
        {
            if (_isControlModeActive)
            {
                ExitControlMode(null);
            }
        }

        private void SnapshotInitialMissionObjects()
        {
            _initialMissionObjects.Clear();
            if (Mission?.ActiveMissionObjects == null)
            {
                return;
            }

            foreach (var missionObject in Mission.ActiveMissionObjects)
            {
                if (missionObject != null)
                {
                    _initialMissionObjects.Add(missionObject);
                }
            }
        }

        private void HandleInput(float dt)
        {
            var input = Mission?.InputManager;
            if (input == null)
            {
                return;
            }

            if (input.IsKeyDown(ToggleControlModifierKey) && input.IsKeyPressed(ToggleControlKey))
            {
                ToggleControlMode();
            }

            if (!_isControlModeActive)
            {
                return;
            }

            if (input.IsKeyPressed(InputKey.Escape) || input.IsKeyPressed(InputKey.RightMouseButton))
            {
                ExitControlMode("Artillery control released.");
                return;
            }

            if (input.IsKeyPressed(FireKey))
            {
                TryFireNextReadyWeapon();
            }
        }

        private void ToggleControlMode()
        {
            if (_isControlModeActive)
            {
                ExitControlMode("Artillery control released.");
                return;
            }

            if (!CanEnterControlMode())
            {
                ShowMessage("Engineer artillery control requires deployed artillery.");
                return;
            }

            EnterControlMode();
        }

        private void EnterControlMode()
        {
            var mainAgent = Mission?.MainAgent;
            _isControlModeActive = true;
            _previousCameraFrame = Mission.GetCameraFrame();
            _wasObjectInteractionEnabled = Mission.IsMainAgentObjectInteractionEnabled;
            _wasItemInteractionEnabled = Mission.IsMainAgentItemInteractionEnabled;
            _wasMainAgentItemUseDisabled = mainAgent?.IsItemUseDisabled == true;
            _previousMovementFlags = mainAgent?.MovementFlags ?? Agent.MovementControlFlag.None;
            _previousMovementInputVector = mainAgent?.MovementInputVector ?? Vec2.Zero;
            _previousPrimaryWieldedItemIndex = mainAgent?.GetPrimaryWieldedItemIndex() ?? EquipmentIndex.None;
            _previousOffhandWieldedItemIndex = mainAgent?.GetOffhandWieldedItemIndex() ?? EquipmentIndex.None;
            StorePreviousCameraState();
            Mission.IsMainAgentObjectInteractionEnabled = false;
            Mission.IsMainAgentItemInteractionEnabled = false;
            Mission.SetCustomCameraIgnoreCollision(true);
            BlockMainAgentControl(true);

            _cameraHeight = InitialCameraHeight;
            _cameraTarget = mainAgent?.Position ?? Vec3.Zero;
            UpdateCameraBasisTowardEnemy();
            _aimTarget = _cameraTarget;
            UpdateAimState();
            ApplyControlCamera();
            ShowMessage("Engineer artillery control: Alt+X/Esc exits, LMB fires one ready gun.");
        }

        private void ExitControlMode(string message)
        {
            _isControlModeActive = false;
            _aimState = AimState.NoTarget;
            _aimTarget = Vec3.Invalid;
            _pendingFireTarget = Vec3.Invalid;
            _pendingFireWeapon = null;
            _pendingFireElapsed = 0f;
            ClearHighlights();
            RemoveOverlayEntity();

            if (Mission != null)
            {
                Mission.IsMainAgentObjectInteractionEnabled = _wasObjectInteractionEnabled;
                Mission.IsMainAgentItemInteractionEnabled = _wasItemInteractionEnabled;
                RestoreMainAgentControl();
                RestorePreviousCameraState();
            }

            if (!string.IsNullOrEmpty(message))
            {
                ShowMessage(message);
            }
        }

        private bool CanEnterControlMode()
        {
            var mainAgent = Mission?.MainAgent;
            return Mission != null &&
                   mainAgent != null &&
                   mainAgent.IsActive() &&
                   mainAgent.Health > 0f &&
                   EngineerCareerHelper.IsEngineerHero(Hero.MainHero) &&
                   Hero.MainHero.HasAttribute("CanPlaceArtillery") &&
                   _playerArtillery.Any(IsUsablePlayerArtillery);
        }

        private void RefreshArtilleryList()
        {
            if (Mission?.ActiveMissionObjects == null)
            {
                _playerArtillery.Clear();
                return;
            }

            _playerArtillery.RemoveAll(weapon => !IsTrackedPlayerArtillery(weapon));

            foreach (var weapon in Mission.ActiveMissionObjects.OfType<RangedSiegeWeapon>())
            {
                if (IsTrackedPlayerArtillery(weapon) && !_playerArtillery.Contains(weapon))
                {
                    _playerArtillery.Add(weapon);
                }
            }

            foreach (var weapon in _playerArtillery)
            {
                EnablePlayerArtilleryAI(weapon);
            }

            if (_nextWeaponIndex >= _playerArtillery.Count)
            {
                _nextWeaponIndex = 0;
            }
        }

        private bool IsTrackedPlayerArtillery(RangedSiegeWeapon weapon)
        {
            if (weapon == null ||
                _initialMissionObjects.Contains(weapon) ||
                !weapon.CreatedAtRuntime ||
                !IsUsablePlayerArtillery(weapon))
            {
                return false;
            }

            var mainAgent = Mission?.MainAgent;
            return mainAgent?.Team != null && weapon.Side == mainAgent.Team.Side;
        }

        private static bool IsUsablePlayerArtillery(RangedSiegeWeapon weapon)
        {
            return weapon != null &&
                   !weapon.IsDisabled &&
                   !weapon.IsDestroyed &&
                   weapon.AmmoCount > 0;
        }

        private static bool IsReadyToFire(RangedSiegeWeapon weapon)
        {
            return IsUsablePlayerArtillery(weapon) &&
                   weapon.State == RangedSiegeWeapon.WeaponState.Idle;
        }

        private static void EnablePlayerArtilleryAI(RangedSiegeWeapon weapon)
        {
            if (weapon == null)
            {
                return;
            }

            try
            {
                weapon.SetIsDisabledForAI(false);
                weapon.SetPlayerForceUse(false);
            }
            catch (Exception ex)
            {
                SubModule.Log($"Failed to enable AI for engineer artillery: {ex}");
            }
        }

        private void SuppressPlayerArtilleryAutoFire()
        {
            if (AiRequestsShootField == null)
            {
                return;
            }

            foreach (var weapon in _playerArtillery)
            {
                try
                {
                    AiRequestsShootField.SetValue(weapon, false);
                }
                catch (Exception ex)
                {
                    SubModule.Log($"Failed to suppress AI artillery shot: {ex}");
                }
            }
        }

        private void UpdateCameraTarget(float dt)
        {
            var input = Mission.InputManager;
            var move = Vec3.Zero;
            if (input.IsKeyDown(InputKey.W) || input.IsKeyDown(InputKey.Up) || input.IsKeyDown(InputKey.Numpad8))
            {
                move += _cameraForward;
            }

            if (input.IsKeyDown(InputKey.S) || input.IsKeyDown(InputKey.Down) || input.IsKeyDown(InputKey.Numpad2))
            {
                move -= _cameraForward;
            }

            if (input.IsKeyDown(InputKey.D) || input.IsKeyDown(InputKey.Right) || input.IsKeyDown(InputKey.Numpad6))
            {
                move += _cameraRight;
            }

            if (input.IsKeyDown(InputKey.A) || input.IsKeyDown(InputKey.Left) || input.IsKeyDown(InputKey.Numpad4))
            {
                move -= _cameraRight;
            }

            if (move.LengthSquared > 0.01f)
            {
                move.Normalize();
                _cameraTarget += move * CameraMoveSpeed * dt * (_cameraHeight / InitialCameraHeight);
            }

            var scroll = input.GetDeltaMouseScroll();
            if (Math.Abs(scroll) > 0.01f)
            {
                _cameraHeight = MBMath.ClampFloat(_cameraHeight - scroll * 8f, MinCameraHeight, MaxCameraHeight);
            }
        }

        private void BlockMainAgentControl(bool sheatheWeapons = false)
        {
            var mainAgent = Mission?.MainAgent;
            if (mainAgent == null || !mainAgent.IsActive())
            {
                return;
            }

            var zeroMovement = Vec2.Zero;
            mainAgent.IsItemUseDisabled = true;
            mainAgent.MovementInputVector = zeroMovement;
            mainAgent.MovementFlags = Agent.MovementControlFlag.None;
            mainAgent.SetMovementDirection(in zeroMovement);
            mainAgent.SetTargetPosition(new Vec2(mainAgent.Position.x, mainAgent.Position.y));
            mainAgent.SetAttackState(0);
            mainAgent.ResetGuard();
            mainAgent.EventControlFlags = Agent.EventControlFlag.None;
            mainAgent.HandleStopUsingAction();

            if (!sheatheWeapons)
            {
                return;
            }

            try
            {
                mainAgent.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.Instant);
                mainAgent.TryToSheathWeaponInHand(Agent.HandIndex.OffHand, Agent.WeaponWieldActionType.Instant);
            }
            catch (Exception ex)
            {
                SubModule.Log($"Failed to sheathe weapons for artillery control: {ex}");
            }
        }

        private void RestoreMainAgentControl()
        {
            var mainAgent = Mission?.MainAgent;
            if (mainAgent == null || !mainAgent.IsActive())
            {
                return;
            }

            mainAgent.IsItemUseDisabled = _wasMainAgentItemUseDisabled;
            mainAgent.MovementInputVector = _previousMovementInputVector;
            mainAgent.MovementFlags = _previousMovementFlags;
            RestorePreviousWieldedWeapons(mainAgent);
        }

        private void RestorePreviousWieldedWeapons(Agent mainAgent)
        {
            try
            {
                if (_previousPrimaryWieldedItemIndex != EquipmentIndex.None)
                {
                    mainAgent.TryToWieldWeaponInSlot(_previousPrimaryWieldedItemIndex, Agent.WeaponWieldActionType.Instant, false);
                }

                if (_previousOffhandWieldedItemIndex != EquipmentIndex.None)
                {
                    mainAgent.TryToWieldWeaponInSlot(_previousOffhandWieldedItemIndex, Agent.WeaponWieldActionType.Instant, false);
                }
            }
            catch (Exception ex)
            {
                SubModule.Log($"Failed to restore weapons after artillery control: {ex}");
            }
        }

        private void UpdateAimTargetFromMouse()
        {
            var input = Mission.InputManager;
            var pointer = input.GetMousePositionRanged();
            var normalizedHeight = MBMath.ClampFloat((_cameraHeight - MinCameraHeight) / (MaxCameraHeight - MinCameraHeight), 0f, 1f);
            var worldWidth = MBMath.Lerp(MouseWorldWidthAtMinZoom, MouseWorldWidthAtMaxZoom, normalizedHeight);
            var worldDepth = worldWidth * 0.65f;

            var xOffset = (pointer.x - 0.5f) * worldWidth;
            var yOffset = (0.5f - pointer.y) * worldDepth;
            var target = _cameraTarget + (_cameraRight * xOffset) + (_cameraForward * yOffset);

            var height = Mission.Scene.GetGroundHeightAtPosition(target, BodyFlags.CommonCollisionExcludeFlags);
            target.z = height + 0.05f;
            _aimTarget = target;
        }

        private void UpdateAimState()
        {
            var weapon = GetNextCommandableWeapon();
            if (weapon == null || !_aimTarget.IsValid)
            {
                _aimState = AimState.NoTarget;
                return;
            }

            var origin = GetWeaponOrigin(weapon);
            if (origin.Distance(_aimTarget) > MaxCommandRange)
            {
                _aimState = AimState.OutOfRange;
                return;
            }

            SafeAimAtTarget(weapon, _aimTarget);
            _aimState = AimState.Valid;
        }

        private void TryFireNextReadyWeapon()
        {
            if (_pendingFireWeapon != null)
            {
                ShowMessage("Artillery is already lining up a shot.");
                return;
            }

            if (_aimState == AimState.NoTarget || _aimState == AimState.OutOfRange)
            {
                ShowMessage("No clear artillery shot.");
                return;
            }

            var weapon = GetNextReadyWeapon();
            if (weapon == null)
            {
                ShowMessage("No ready artillery pieces.");
                return;
            }

            SafeAimAtTarget(weapon, _aimTarget);
            _pendingFireWeapon = weapon;
            _pendingFireTarget = _aimTarget;
            _pendingFireElapsed = 0f;
            ShowMessage("Artillery lining up.");
        }

        private void ProcessPendingFire(float dt)
        {
            if (_pendingFireWeapon == null)
            {
                return;
            }

            _pendingFireElapsed += dt;

            if (!IsReadyToFire(_pendingFireWeapon))
            {
                ClearPendingFire();
                ShowMessage("Artillery shot cancelled.");
                return;
            }

            SafeAimAtTarget(_pendingFireWeapon, _pendingFireTarget);

            if (!SafeCheckIsTargetReached(_pendingFireWeapon, _pendingFireTarget) &&
                _pendingFireElapsed < PendingAimTimeout)
            {
                return;
            }

            if (_pendingFireWeapon.Shoot())
            {
                AdvanceWeaponIndexAfter(_pendingFireWeapon);
                ClearPendingFire();
                ShowMessage("Artillery fired.");
            }
            else
            {
                ClearPendingFire();
                ShowMessage("Artillery is not ready to fire.");
            }
        }

        private void ClearPendingFire()
        {
            _pendingFireWeapon = null;
            _pendingFireTarget = Vec3.Invalid;
            _pendingFireElapsed = 0f;
        }

        private RangedSiegeWeapon GetNextReadyWeapon()
        {
            if (_playerArtillery.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < _playerArtillery.Count; i++)
            {
                var index = (_nextWeaponIndex + i) % _playerArtillery.Count;
                var weapon = _playerArtillery[index];
                if (IsReadyToFire(weapon))
                {
                    return weapon;
                }
            }

            return null;
        }

        private RangedSiegeWeapon GetNextCommandableWeapon()
        {
            if (_playerArtillery.Count == 0)
            {
                return null;
            }

            for (var i = 0; i < _playerArtillery.Count; i++)
            {
                var index = (_nextWeaponIndex + i) % _playerArtillery.Count;
                var weapon = _playerArtillery[index];
                if (IsUsablePlayerArtillery(weapon))
                {
                    return weapon;
                }
            }

            return null;
        }

        private void AdvanceWeaponIndexAfter(RangedSiegeWeapon weapon)
        {
            var index = _playerArtillery.IndexOf(weapon);
            if (index >= 0 && _playerArtillery.Count > 0)
            {
                _nextWeaponIndex = (index + 1) % _playerArtillery.Count;
            }
        }

        private void ApplyControlCamera()
        {
            if (Mission?.MainAgent == null)
            {
                return;
            }

            var cameraPosition = _cameraTarget - (_cameraForward * _cameraHeight * CameraTiltFactor) + new Vec3(0f, 0f, _cameraHeight);
            var lookAt = _cameraTarget;
            var up = Vec3.Up;
            var frame = MatrixFrame.CreateLookAt(in cameraPosition, in lookAt, in up);
            var targetOffset = _cameraTarget - Mission.MainAgent.Position;
            var cameraOffset = -(_cameraForward * _cameraHeight * CameraTiltFactor) + new Vec3(0f, 0f, _cameraHeight);
            Mission.SetCustomCameraTargetLocalOffset(targetOffset);
            Mission.SetCustomCameraGlobalOffset(cameraOffset);
            Mission.SetCustomCameraIgnoreCollision(true);
            Mission.SetCameraFrame(ref frame, 0f);
        }

        private void UpdateCameraBasisTowardEnemy()
        {
            var mainAgent = Mission?.MainAgent;
            if (mainAgent?.Team == null || Mission?.Agents == null)
            {
                return;
            }

            var enemyCenter = Vec3.Zero;
            var enemyCount = 0;
            foreach (var agent in Mission.Agents)
            {
                if (agent == null ||
                    !agent.IsHuman ||
                    !agent.IsActive() ||
                    agent.Team == null ||
                    agent.Team.Side == mainAgent.Team.Side)
                {
                    continue;
                }

                enemyCenter += agent.Position;
                enemyCount++;
            }

            if (enemyCount <= 0)
            {
                return;
            }

            enemyCenter *= 1f / enemyCount;
            var forward = enemyCenter - mainAgent.Position;
            forward.z = 0f;
            if (forward.LengthSquared < 0.01f)
            {
                return;
            }

            forward.Normalize();
            _cameraForward = forward;
            _cameraRight = new Vec3(_cameraForward.y, -_cameraForward.x, 0f);
            _cameraRight.Normalize();
        }

        private void StorePreviousCameraState()
        {
            _previousCustomCameraTargetLocalOffset = Mission.CustomCameraTargetLocalOffset;
            _previousCustomCameraLocalOffset = Mission.CustomCameraLocalOffset;
            _previousCustomCameraLocalOffset2 = Mission.CustomCameraLocalOffset2;
            _previousCustomCameraGlobalOffset = Mission.CustomCameraGlobalOffset;
            _previousCustomCameraLocalRotationalOffset = Mission.CustomCameraLocalRotationalOffset;
            _previousCustomCameraIgnoreCollision = Mission.CustomCameraIgnoreCollision;
            _previousCustomCameraFixedDistance = Mission.CustomCameraFixedDistance;
            _previousCustomCameraFovMultiplier = Mission.CustomCameraFovMultiplier;
        }

        private void RestorePreviousCameraState()
        {
            Mission.SetCustomCameraTargetLocalOffset(_previousCustomCameraTargetLocalOffset);
            Mission.SetCustomCameraLocalOffset(_previousCustomCameraLocalOffset);
            Mission.SetCustomCameraLocalOffset2(_previousCustomCameraLocalOffset2);
            Mission.SetCustomCameraGlobalOffset(_previousCustomCameraGlobalOffset);
            Mission.SetCustomCameraLocalRotationalOffset(_previousCustomCameraLocalRotationalOffset);
            Mission.SetCustomCameraIgnoreCollision(_previousCustomCameraIgnoreCollision);
            Mission.SetCustomCameraFixedDistance(_previousCustomCameraFixedDistance);
            Mission.SetCustomCameraFovMultiplier(_previousCustomCameraFovMultiplier);

            if (!Mission.MissionEnded && !Mission.MissionIsEnding)
            {
                Mission.SetCameraFrame(ref _previousCameraFrame, 0f);
            }
        }

        private static bool SafeCanShootAtPoint(RangedSiegeWeapon weapon, Vec3 target)
        {
            try
            {
                return weapon != null && target.IsValid && weapon.CanShootAtPoint(target);
            }
            catch (Exception ex)
            {
                SubModule.Log($"Artillery CanShootAtPoint failed: {ex}");
                return false;
            }
        }

        private static bool SafeAimAtTarget(RangedSiegeWeapon weapon, Vec3 target)
        {
            try
            {
                if (weapon == null || !target.IsValid)
                {
                    return false;
                }

                if (weapon.AimAtTarget(target))
                {
                    return true;
                }

                return SafeAimAtTargetByRotation(weapon, target);
            }
            catch (Exception ex)
            {
                SubModule.Log($"Artillery AimAtTarget failed: {ex}");
                return false;
            }
        }

        private static bool SafeAimAtTargetByRotation(RangedSiegeWeapon weapon, Vec3 target)
        {
            if (CalculateLocalAimMethod == null)
            {
                return false;
            }

            try
            {
                var parameters = new object[] { target, 0f, 0f };
                CalculateLocalAimMethod.Invoke(weapon, parameters);
                weapon.AimAtRotation((float)parameters[1], (float)parameters[2]);
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log($"Artillery AimAtRotation fallback failed: {ex}");
                return false;
            }
        }

        private static bool SafeCheckIsTargetReached(RangedSiegeWeapon weapon, Vec3 target)
        {
            try
            {
                return weapon != null && target.IsValid && weapon.CheckIsTargetReached(target);
            }
            catch (Exception ex)
            {
                SubModule.Log($"Artillery CheckIsTargetReached failed: {ex}");
                return false;
            }
        }

        private void RenderAimPreview()
        {
            ClearOverlayMesh();
            var weapon = GetNextCommandableWeapon();
            if (weapon == null || !_aimTarget.IsValid)
            {
                return;
            }

            var color = _aimState == AimState.Valid ? ValidColor : BlockedColor;
            EnsureOverlayEntity();
            PrepareOverlayForColor(color);
            RenderTrajectory(weapon, GetWeaponOrigin(weapon), _aimTarget, color);
            RenderImpactCircle(_aimTarget, ImpactRadius, color);
            _overlayMesh?.ComputeNormals();
            _overlayMesh?.UpdateBoundingBox();
            _overlayMesh?.PreloadForRendering();
        }

        private void RenderTrajectory(RangedSiegeWeapon weapon, Vec3 origin, Vec3 target, uint color)
        {
            const int segments = 24;
            origin.z += TrajectoryVisualLift;
            target.z += TrajectoryVisualLift;
            var last = origin;
            var arcHeight = GetTrajectoryArcHeight(weapon, origin, target);

            for (var i = 1; i <= segments; i++)
            {
                var t = i / (float)segments;
                var point = Vec3.Lerp(origin, target, t);
                point.z += MathF.Sin(t * MathF.PI) * arcHeight;
                AddRibbonSegment(last, point, TrajectoryRibbonWidth, color);
                last = point;
            }
        }

        private float GetTrajectoryArcHeight(RangedSiegeWeapon weapon, Vec3 origin, Vec3 target)
        {
            var distance = origin.Distance(target);
            if (IsHighArcArtillery(weapon))
            {
                return MBMath.ClampFloat(distance * 0.55f, 18f, 140f);
            }

            var releaseAngle = 0f;
            try
            {
                releaseAngle = Math.Abs(weapon.GetTargetReleaseAngle(target));
            }
            catch
            {
                releaseAngle = 0f;
            }

            if (releaseAngle > MathF.PI)
            {
                releaseAngle *= MathF.DegToRad;
            }

            var angleFactor = MBMath.ClampFloat(MathF.Sin(releaseAngle), 0.12f, 0.75f);
            return MBMath.ClampFloat(distance * (0.10f + angleFactor * 0.28f), 8f, 90f);
        }

        private static bool IsHighArcArtillery(RangedSiegeWeapon weapon)
        {
            var engineType = weapon?.GetSiegeEngineType();
            var id = engineType?.StringId ?? string.Empty;
            var name = engineType?.Name?.ToString() ?? string.Empty;
            var text = (id + " " + name).ToLowerInvariant();

            return text.Contains("mortar") ||
                   text.Contains("mortir") ||
                   text.Contains("mangonel") ||
                   text.Contains("trebuchet");
        }

        private void RenderImpactCircle(Vec3 center, float radius, uint color)
        {
            const int segments = 40;
            var last = CirclePoint(center, radius, 0);
            for (var i = 1; i <= segments; i++)
            {
                var current = CirclePoint(center, radius, i / (float)segments);
                AddRibbonSegment(last, current, ImpactCircleRibbonWidth, color);
                last = current;
            }

            AddRibbonSegment(CirclePoint(center, radius * 0.35f, 0f), CirclePoint(center, radius, 0f), ImpactCircleRibbonWidth * 0.75f, color);
            AddRibbonSegment(CirclePoint(center, radius * 0.35f, 0.25f), CirclePoint(center, radius, 0.25f), ImpactCircleRibbonWidth * 0.75f, color);
            AddRibbonSegment(CirclePoint(center, radius * 0.35f, 0.5f), CirclePoint(center, radius, 0.5f), ImpactCircleRibbonWidth * 0.75f, color);
            AddRibbonSegment(CirclePoint(center, radius * 0.35f, 0.75f), CirclePoint(center, radius, 0.75f), ImpactCircleRibbonWidth * 0.75f, color);
        }

        private void EnsureOverlayEntity()
        {
            if (_overlayEntity != null && _overlayMesh != null)
            {
                return;
            }

            var material = CreateOverlayMaterial();
            _overlayMesh = Mesh.CreateMeshWithMaterial(material);
            _overlayMesh.Name = "tor_engineer_artillery_overlay_mesh";
            _overlayMesh.SetMeshRenderOrder(200);
            _overlayMesh.Color = ValidColor;
            _overlayMesh.Color2 = ValidColor;
            _overlayMesh.HintVerticesDynamic();
            _overlayMesh.HintIndicesDynamic();

            _overlayEntity = GameEntity.CreateEmpty(Mission.Scene, false, true, true);
            _overlayEntity.Name = "tor_engineer_artillery_overlay";
            _overlayEntity.AddMesh(_overlayMesh, true);
            var frame = MatrixFrame.Identity;
            _overlayEntity.SetFrame(ref frame, true);
            _overlayEntity.SetVisibilityExcludeParents(true);
            _overlayEntity.SetDoNotCheckVisibility(true);
            _overlayEntity.SetReadyToRender(true);
        }

        private static Material CreateOverlayMaterial()
        {
            foreach (var materialName in new[] { "plain_white", "plank_stake_a_vertex_color", "location_debug_mat" })
            {
                try
                {
                    var material = Material.GetFromResource(materialName);
                    if (material != null)
                    {
                        return material;
                    }
                }
                catch
                {
                }
            }

            return Material.GetDefaultMaterial();
        }

        private void PrepareOverlayForColor(uint color)
        {
            if (_overlayMesh == null)
            {
                return;
            }

            _overlayMesh.Color = color;
            _overlayMesh.Color2 = color;
            _overlayMesh.SetColorAndStroke(color, color, true);
            _overlayMesh.SetColorAlpha(255);
            _overlayEntity?.SetContourColor(color, true);
        }

        private void ClearOverlayMesh()
        {
            _overlayMesh?.ClearMesh();
        }

        private void RemoveOverlayEntity()
        {
            try
            {
                _overlayEntity?.Remove(0);
            }
            catch (Exception ex)
            {
                SubModule.Log($"Failed to remove artillery overlay entity: {ex}");
            }

            _overlayEntity = null;
            _overlayMesh = null;
        }

        private void AddRibbonSegment(Vec3 start, Vec3 end, float width, uint color)
        {
            if (_overlayMesh == null)
            {
                return;
            }

            var direction = end - start;
            if (direction.LengthSquared < 0.001f)
            {
                return;
            }

            direction.Normalize();
            var side = Vec3.CrossProduct(direction, Vec3.Up);
            if (side.LengthSquared < 0.001f)
            {
                side = Vec3.Side;
            }

            side.Normalize();
            side *= width * 0.5f;

            var normal = Vec3.Up;
            var uv0 = new Vec2(0f, 0f);
            var uv1 = new Vec2(1f, 0f);
            var uv2 = new Vec2(1f, 1f);
            var uv3 = new Vec2(0f, 1f);

            var a = start - side;
            var b = start + side;
            var c = end + side;
            var d = end - side;
            _overlayMesh.AddTriangleWithVertexColors(a, b, c, uv0, uv1, uv2, color, color, color, UIntPtr.Zero);
            _overlayMesh.AddTriangleWithVertexColors(a, c, d, uv0, uv2, uv3, color, color, color, UIntPtr.Zero);

            var verticalOffset = new Vec3(0f, 0f, width * 0.75f);
            _overlayMesh.AddTriangleWithVertexColors(a + verticalOffset, b + verticalOffset, c + verticalOffset, uv0, uv1, uv2, color, color, color, UIntPtr.Zero);
            _overlayMesh.AddTriangleWithVertexColors(a + verticalOffset, c + verticalOffset, d + verticalOffset, uv0, uv2, uv3, color, color, color, UIntPtr.Zero);
        }

        private Vec3 CirclePoint(Vec3 center, float radius, float t)
        {
            var angle = t * MBMath.TwoPI;
            var point = center + new Vec3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 0f);
            point.z = Mission.Scene.GetGroundHeightAtPosition(point, BodyFlags.CommonCollisionExcludeFlags) + ImpactCircleVisualLift;
            return point;
        }

        private void RenderControlPanel()
        {
            MBDebug.RenderDebugText(0.035f, 0.78f, "ENGINEER ARTILLERY CONTROL", PanelColor, 0.95f);
            var pending = _pendingFireWeapon == null ? string.Empty : " | LINING UP";
            MBDebug.RenderDebugText(0.035f, 0.815f, $"Alt+X/Esc: exit | WASD/arrows: pan | LMB: fire | Aim: {_aimState}{pending}", PanelColor, 0.8f);

            var y = 0.85f;
            for (var i = 0; i < _playerArtillery.Count; i++)
            {
                var weapon = _playerArtillery[i];
                var prefix = i == _nextWeaponIndex ? ">" : " ";
                var state = IsReadyToFire(weapon) ? "READY" : weapon.State.ToString();
                MBDebug.RenderDebugText(0.035f, y, $"{prefix} Gun {i + 1}: {state} | Ammo {weapon.AmmoCount}", PanelColor, 0.75f);
                y += 0.028f;
            }
        }

        private static Vec3 GetWeaponOrigin(RangedSiegeWeapon weapon)
        {
            var projectilePosition = weapon.ProjectileEntityCurrentGlobalPosition;
            if (projectilePosition.IsValid && projectilePosition.LengthSquared > 0.01f)
            {
                return projectilePosition;
            }

            return weapon.GameEntity.GlobalPosition + new Vec3(0f, 0f, 2f);
        }

        private void UpdateHighlights()
        {
            var mainAgent = Mission?.MainAgent;
            if (mainAgent?.Team == null)
            {
                return;
            }

            foreach (var agent in Mission.Agents)
            {
                if (agent == null ||
                    !agent.IsHuman ||
                    !agent.IsActive() ||
                    agent.Team != mainAgent.Team ||
                    _highlightedAllies.Contains(agent))
                {
                    continue;
                }

                agent.AgentVisuals?.SetContourColor(AllyContourColor, true);
                _highlightedAllies.Add(agent);
            }

            foreach (var weapon in _playerArtillery)
            {
                if (weapon?.GameEntity == null || _highlightedArtillery.Contains(weapon))
                {
                    continue;
                }

                weapon.GameEntity.SetContourColor(ArtilleryContourColor, true);
                _highlightedArtillery.Add(weapon);
            }
        }

        private void ClearHighlights()
        {
            foreach (var agent in _highlightedAllies)
            {
                try
                {
                    agent?.AgentVisuals?.SetContourColor(null, true);
                }
                catch
                {
                }
            }

            foreach (var weapon in _highlightedArtillery)
            {
                try
                {
                    if (weapon != null)
                    {
                        weapon.GameEntity.SetContourColor(null, true);
                    }
                }
                catch
                {
                }
            }

            _highlightedAllies.Clear();
            _highlightedArtillery.Clear();
        }

        private void RefreshViewModel()
        {
            _viewModel.IsControlModeActive = _isControlModeActive;
            _viewModel.CanEnterControlMode = CanEnterControlMode();
            _viewModel.AimState = _aimState.ToString();
            _viewModel.ArtillerySummary = BuildArtillerySummary();
            _viewModel.StatusText = _isControlModeActive
                ? "Alt+X/Esc exits. WASD/arrows pan. LMB fires one ready gun."
                : "Alt+X opens artillery control when deployed artillery is available.";
        }

        private string BuildArtillerySummary()
        {
            if (_playerArtillery.Count == 0)
            {
                return "No artillery";
            }

            return string.Join(" | ", _playerArtillery.Select((weapon, index) =>
            {
                var state = IsReadyToFire(weapon) ? "Ready" : weapon.State.ToString();
                return $"{index + 1}:{state}:{weapon.AmmoCount}";
            }));
        }

        private static void ShowMessage(string message)
        {
            InformationManager.DisplayMessage(new InformationMessage(message));
        }
    }
}
