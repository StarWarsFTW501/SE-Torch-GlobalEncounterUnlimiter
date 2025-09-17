using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TorchPlugin
{
    internal class PersistentConfig<T> : IDisposable where T : class, INotifyPropertyChanged, new()
    {
        private const int SAVE_DELAY_MS = 2000;

        private T _data;
        private string _path;
        private Timer _saveTimer;

        object _writingLock = new object();

        public T Data
        {
            get => _data;
            private set
            {
                if (_data != value)
                {
                    if (_data != null)
                        _data.PropertyChanged -= OnPropertyChanged;

                    _data = value;

                    if (_data != null)
                        _data.PropertyChanged += OnPropertyChanged;
                }
            }
        }



        PersistentConfig(T data, string path)
        {
            _data = data;
            _path = path;
        }


        public static PersistentConfig<T> Load(MyLogger logger, string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var serializer = new XmlSerializer(typeof(T));
                    using (var streamReader = File.OpenText(path))
                        return new PersistentConfig<T>(serializer.Deserialize(streamReader) as T, path);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to load configuration file: {path}");
                try
                {
                    var timestamp = $"{DateTime.Now:yyyyMMdd-hhmmss}";
                    var newPath = $"{path}.corrupted.{timestamp}.txt";
                    logger.Info($"Renaming corrupted configuration file: {path} => {newPath}");
                    File.Move(path, newPath);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "An unknown error has occurred renaming corrupted configuration file.");
                }
            }

            logger.Info($"Configuration file does not exist at {path} - Creating default...");

            var config = new PersistentConfig<T>(default, path);

            config.SaveNow();

            return config;
        }



        /// <summary>
        /// Makes the config save automatically after the default delay.
        /// </summary>
        public void SaveLater()
        {
            if (_saveTimer == null)
                _saveTimer = new Timer(_ => SaveNow());

            _saveTimer.Change(SAVE_DELAY_MS, Timeout.Infinite);
        }
        /// <summary>
        /// Immediately saves the config and stops any waiting delayed saves.
        /// </summary>
        /// <param name="overridePath">Optional path to override the destination to. Will be remembered for subsequent saves.</param>
        public void SaveNow(string overridePath = null)
        {
            lock (_writingLock)
            {
                if (overridePath != null)
                    _path = overridePath;

                _saveTimer = null;

                using (var stream = File.CreateText(_path))
                    new XmlSerializer(typeof(T)).Serialize(stream, _data);
            }
        }


        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) => SaveLater();


        ~PersistentConfig() => Dispose();
        public void Dispose()
        {
            try
            {
                _data.PropertyChanged -= OnPropertyChanged;
                _saveTimer?.Dispose();
                SaveNow();
            }
            catch
            {
                // Ignored
            }
        }
    }
}
