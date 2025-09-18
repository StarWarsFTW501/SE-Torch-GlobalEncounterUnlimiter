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
        bool _gpsSynchronization;

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

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
