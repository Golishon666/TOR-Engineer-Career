using System;
using System.Collections.Generic;
using System.Linq;
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
        private const uint ValidColor = 0xFF20FF40;
        private const uint BlockedColor = 0xFFFF3030;
        private const uint PanelColor = 0xDDE8E8E8;

        private static readonly InputKey ToggleControlKey = InputKey.F6;
        private static readonly InputKey FireKey = InputKey.LeftMouseButton;

        private readonly HashSet<MissionObject> _initialMissionObjects = new();
        private readonly List<RangedSiegeWeapon> _playerArtillery = new();
        private readonly EngineerArtilleryControlVM _viewModel;

        private bool _isControlModeActive;
        private bool _wasObjectInteractionEnabled = true;
        private int _nextWeaponIndex;
        private float _cameraHeight = InitialCameraHeight;
        private Vec3 _cameraTarget = Vec3.Zero;
        private Vec3 _aimTarget = Vec3.Invalid;
        private Vec3 _pendingFireTarget = Vec3.Invalid;
        private MatrixFrame _previousCameraFrame = MatrixFrame.Identity;
        private RangedSiegeWeapon _pendingFireWeapon;
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
            RefreshArtilleryList();
            HandleInput(dt);

            if (_isControlModeActive)
            {
                if (!CanEnterControlMode())
                {
                    ExitControlMode("Artillery control unavailable.");
                    return;
                }

                UpdateCameraTarget(dt);
                UpdateAimTargetFromMouse();
                UpdateAimState();
                ProcessPendingFire();
                ApplyControlCamera();
            }

            RefreshViewModel();
        }

        public override void OnPreDisplayMissionTick(float dt)
        {
            if (!_isControlModeActive)
            {
                return;
            }

            RenderAimPreview();
            RenderControlPanel();
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

            if (input.IsKeyPressed(ToggleControlKey))
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
            Mission.IsMainAgentObjectInteractionEnabled = false;
            Mission.SetCustomCameraIgnoreCollision(true);

            _cameraHeight = InitialCameraHeight;
            _cameraTarget = mainAgent?.Position ?? Vec3.Zero;
            _aimTarget = _cameraTarget;
            UpdateAimState();
            ApplyControlCamera();
            ShowMessage("Engineer artillery control: F6/Esc exits, LMB fires one ready gun.");
        }

        private void ExitControlMode(string message)
        {
            _isControlModeActive = false;
            _aimState = AimState.NoTarget;
            _aimTarget = Vec3.Invalid;
            _pendingFireTarget = Vec3.Invalid;
            _pendingFireWeapon = null;

            if (Mission != null)
            {
                Mission.IsMainAgentObjectInteractionEnabled = _wasObjectInteractionEnabled;
                Mission.SetCustomCameraIgnoreCollision(false);
                Mission.SetCameraFrame(ref _previousCameraFrame, 0f);
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

        private void UpdateCameraTarget(float dt)
        {
            var input = Mission.InputManager;
            var move = Vec3.Zero;
            if (input.IsKeyDown(InputKey.W))
            {
                move.y += 1f;
            }

            if (input.IsKeyDown(InputKey.S))
            {
                move.y -= 1f;
            }

            if (input.IsKeyDown(InputKey.D))
            {
                move.x += 1f;
            }

            if (input.IsKeyDown(InputKey.A))
            {
                move.x -= 1f;
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

        private void UpdateAimTargetFromMouse()
        {
            var input = Mission.InputManager;
            var pointer = input.GetMousePositionRanged();
            var normalizedHeight = MBMath.ClampFloat((_cameraHeight - MinCameraHeight) / (MaxCameraHeight - MinCameraHeight), 0f, 1f);
            var worldWidth = MBMath.Lerp(MouseWorldWidthAtMinZoom, MouseWorldWidthAtMaxZoom, normalizedHeight);
            var worldDepth = worldWidth * 0.65f;

            var xOffset = (pointer.x - 0.5f) * worldWidth;
            var yOffset = (0.5f - pointer.y) * worldDepth;
            var target = _cameraTarget + new Vec3(xOffset, yOffset, 0f);

            var height = Mission.Scene.GetGroundHeightAtPosition(target, BodyFlags.CommonCollisionExcludeFlags);
            target.z = height + 0.05f;
            _aimTarget = target;
        }

        private void UpdateAimState()
        {
            var weapon = GetNextReadyWeapon();
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

            _aimState = weapon.CanShootAtPoint(_aimTarget) ? AimState.Valid : AimState.Blocked;
        }

        private void TryFireNextReadyWeapon()
        {
            if (_pendingFireWeapon != null)
            {
                ShowMessage("Artillery is already lining up a shot.");
                return;
            }

            if (_aimState != AimState.Valid)
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

            if (!weapon.CanShootAtPoint(_aimTarget))
            {
                _aimState = AimState.Blocked;
                ShowMessage("The selected artillery cannot reach that point.");
                return;
            }

            _pendingFireWeapon = weapon;
            _pendingFireTarget = _aimTarget;
            ShowMessage("Artillery lining up.");
        }

        private void ProcessPendingFire()
        {
            if (_pendingFireWeapon == null)
            {
                return;
            }

            if (!IsReadyToFire(_pendingFireWeapon))
            {
                ClearPendingFire();
                ShowMessage("Artillery shot cancelled.");
                return;
            }

            if (!_pendingFireWeapon.CanShootAtPoint(_pendingFireTarget) ||
                !_pendingFireWeapon.AimAtTarget(_pendingFireTarget))
            {
                ClearPendingFire();
                _aimState = AimState.Blocked;
                ShowMessage("Artillery cannot line up that shot.");
                return;
            }

            if (!_pendingFireWeapon.CheckIsTargetReached(_pendingFireTarget))
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
            var cameraPosition = _cameraTarget + new Vec3(0f, -_cameraHeight * CameraTiltFactor, _cameraHeight);
            var lookAt = _cameraTarget;
            var up = Vec3.Up;
            var frame = MatrixFrame.CreateLookAt(in cameraPosition, in lookAt, in up);
            Mission.SetCameraFrame(ref frame, 0f);
        }

        private void RenderAimPreview()
        {
            var weapon = GetNextReadyWeapon();
            if (weapon == null || !_aimTarget.IsValid)
            {
                return;
            }

            var color = _aimState == AimState.Valid ? ValidColor : BlockedColor;
            RenderTrajectory(GetWeaponOrigin(weapon), _aimTarget, color);
            RenderImpactCircle(_aimTarget, ImpactRadius, color);
            MBDebug.RenderDebugSphere(_aimTarget + new Vec3(0f, 0f, 0.35f), 0.45f, color, false, 0f);
        }

        private void RenderTrajectory(Vec3 origin, Vec3 target, uint color)
        {
            const int segments = 24;
            var last = origin;
            var distance = origin.Distance(target);
            var arcHeight = MBMath.ClampFloat(distance * 0.18f, 8f, 45f);

            for (var i = 1; i <= segments; i++)
            {
                var t = i / (float)segments;
                var point = Vec3.Lerp(origin, target, t);
                point.z += MathF.Sin(t * MathF.PI) * arcHeight;
                MBDebug.RenderDebugLine(last, point, color, false, 0f);
                last = point;
            }
        }

        private void RenderImpactCircle(Vec3 center, float radius, uint color)
        {
            const int segments = 40;
            var last = CirclePoint(center, radius, 0);
            for (var i = 1; i <= segments; i++)
            {
                var current = CirclePoint(center, radius, i / (float)segments);
                MBDebug.RenderDebugLine(last, current, color, false, 0f);
                last = current;
            }
        }

        private Vec3 CirclePoint(Vec3 center, float radius, float t)
        {
            var angle = t * MBMath.TwoPI;
            var point = center + new Vec3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 0f);
            point.z = Mission.Scene.GetGroundHeightAtPosition(point, BodyFlags.CommonCollisionExcludeFlags) + 0.12f;
            return point;
        }

        private void RenderControlPanel()
        {
            MBDebug.RenderDebugText(0.035f, 0.78f, "ENGINEER ARTILLERY CONTROL", PanelColor, 0.95f);
            var pending = _pendingFireWeapon == null ? string.Empty : " | LINING UP";
            MBDebug.RenderDebugText(0.035f, 0.815f, $"F6/Esc: exit | LMB: fire | Aim: {_aimState}{pending}", PanelColor, 0.8f);

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

        private void RefreshViewModel()
        {
            _viewModel.IsControlModeActive = _isControlModeActive;
            _viewModel.CanEnterControlMode = CanEnterControlMode();
            _viewModel.AimState = _aimState.ToString();
            _viewModel.ArtillerySummary = BuildArtillerySummary();
            _viewModel.StatusText = _isControlModeActive
                ? "F6/Esc exits. LMB fires one ready gun."
                : "F6 opens artillery control when deployed artillery is available.";
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
