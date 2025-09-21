using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GlobalEncounterUnlimiter
{
    public class MyPluginConfig : INotifyPropertyChanged
    {
        bool _gpsSynchronization = false;
        bool _locationRestriction = false;
        double _locationRestrictionCenterX = 0;
        double _locationRestrictionCenterY = 0;
        double _locationRestrictionCenterZ = 0;
        int _locationRestrictionMinRadius = 0;
        int _locationRestrictionMaxRadius = 10000000;
        bool _locationRestrictionAllowPlanets = true;

        public bool GPSSynchronization
        {
            get => _gpsSynchronization;
            set
            {
                if (_gpsSynchronization != value)
                {
                    _gpsSynchronization = value;
                    OnPropertyChanged(nameof(GPSSynchronization));
                }
            }
        }
        public bool LocationRestriction
        {
            get => _locationRestriction;
            set
            {
                if (_locationRestriction != value)
                {
                    _locationRestriction = value;
                    OnPropertyChanged(nameof(LocationRestriction));
                }
            }
        }
        public double LocationRestrictionCenterX
        {
            get => _locationRestrictionCenterX;
            set
            {
                if (_locationRestrictionCenterX != value)
                {
                    _locationRestrictionCenterX = value;
                    OnPropertyChanged(nameof(LocationRestrictionCenterX));
                }
            }
        }
        public double LocationRestrictionCenterY
        {
            get => _locationRestrictionCenterY;
            set
            {
                if (_locationRestrictionCenterY != value)
                {
                    _locationRestrictionCenterY = value;
                    OnPropertyChanged(nameof(LocationRestrictionCenterY));
                }
            }
        }
        public double LocationRestrictionCenterZ
        {
            get => _locationRestrictionCenterZ;
            set
            {
                if (_locationRestrictionCenterZ != value)
                {
                    _locationRestrictionCenterZ = value;
                    OnPropertyChanged(nameof(LocationRestrictionCenterZ));
                }
            }
        }
        public int LocationRestrictionMinRadius
        {
            get => _locationRestrictionMinRadius;
            set
            {
                if (_locationRestrictionMinRadius != value)
                {
                    _locationRestrictionMinRadius = value;
                    OnPropertyChanged(nameof(LocationRestrictionMinRadius));
                }
            }
        }
        public int LocationRestrictionMaxRadius
        {
            get => _locationRestrictionMaxRadius;
            set
            {
                if (_locationRestrictionMaxRadius != value)
                {
                    _locationRestrictionMaxRadius = value;
                    OnPropertyChanged(nameof(LocationRestrictionMaxRadius));
                }
            }
        }
        public bool LocationRestrictionAllowPlanets
        {
            get => _locationRestrictionAllowPlanets;
            set
            {
                if (_locationRestrictionAllowPlanets != value)
                {
                    _locationRestrictionAllowPlanets = value;
                    OnPropertyChanged(nameof(LocationRestrictionAllowPlanets));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
