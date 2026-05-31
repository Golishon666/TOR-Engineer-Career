using System;
using TaleWorlds.Library;

namespace TOR_EngineerCareer
{
    internal sealed class EngineerArtilleryControlVM : ViewModel
    {
        private bool _isControlModeActive;
        private bool _canEnterControlMode;
        private string _statusText = string.Empty;
        private string _aimState = "NoTarget";
        private string _artillerySummary = string.Empty;
        private readonly Action _toggleControlMode;
        private readonly Action _fire;

        public EngineerArtilleryControlVM(Action toggleControlMode, Action fire)
        {
            _toggleControlMode = toggleControlMode;
            _fire = fire;
        }

        [DataSourceProperty]
        public bool IsControlModeActive
        {
            get => _isControlModeActive;
            set
            {
                if (value != _isControlModeActive)
                {
                    _isControlModeActive = value;
                    OnPropertyChangedWithValue(value, nameof(IsControlModeActive));
                }
            }
        }

        [DataSourceProperty]
        public bool CanEnterControlMode
        {
            get => _canEnterControlMode;
            set
            {
                if (value != _canEnterControlMode)
                {
                    _canEnterControlMode = value;
                    OnPropertyChangedWithValue(value, nameof(CanEnterControlMode));
                }
            }
        }

        [DataSourceProperty]
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (value != _statusText)
                {
                    _statusText = value;
                    OnPropertyChangedWithValue(value, nameof(StatusText));
                }
            }
        }

        [DataSourceProperty]
        public string AimState
        {
            get => _aimState;
            set
            {
                if (value != _aimState)
                {
                    _aimState = value;
                    OnPropertyChangedWithValue(value, nameof(AimState));
                }
            }
        }

        [DataSourceProperty]
        public string ArtillerySummary
        {
            get => _artillerySummary;
            set
            {
                if (value != _artillerySummary)
                {
                    _artillerySummary = value;
                    OnPropertyChangedWithValue(value, nameof(ArtillerySummary));
                }
            }
        }

        public void ExecuteToggleControlMode()
        {
            _toggleControlMode?.Invoke();
        }

        public void ExecuteFire()
        {
            _fire?.Invoke();
        }
    }
}
