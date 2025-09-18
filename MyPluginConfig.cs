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
    public class MyPluginConfig : INotifyPropertyChanged, IMyPluginConfig
    {
        bool _gpsCreation;

        public bool GPSCreation
        {
            get => _gpsCreation;
            set
            {
                if (_gpsCreation != value)
                {
                    _gpsCreation = value;
                    OnPropertyChanged(nameof(GPSCreation));
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
