using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using SpaceEngineers.Game.SessionComponents;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Screens.Helpers;
using System.Collections.Concurrent;
using VRageMath;
using Torch.Managers;
using Torch.API;
using VRage.Game.Components;
using System.Xml.Serialization;
using System.IO;
using NLog;
using System.Threading;
using System.Windows.Shapes;
using NLog.LayoutRenderers;

namespace GlobalEncounterUnlimiter
{
    public class MyEncounterGpsSynchronizer
    {
        static readonly FieldInfo m_encountersGps_fieldInfo = typeof(MyGlobalEncountersGenerator).GetField("m_encountersGps", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException($"Could not find field m_encountersGps in type {typeof(MyGlobalEncountersGenerator).FullName}! Unload plugin and contact author.");

        static readonly XmlSerializer _xmlSerializer = new XmlSerializer(typeof(MyEncounterGpsSynchronizer));


        public List<MyPlayerSynchronizationEntry> PlayerSyncEntries = new List<MyPlayerSynchronizationEntry>();

        public List<(long encounterId, MySerializableGpsData gpsData)> SerializableGPSes = new List<(long, MySerializableGpsData)>();

        Dictionary<long, MyGps> _actualGPSes = new Dictionary<long, MyGps>();

        Queue<MyIdentity> _joinedPlayersQueue = new Queue<MyIdentity>();


        bool _fromDeserializedImage = false;
        bool _waitingToSave = false;
        string _filePath = null;

        #region Persistence across shutdowns/restarts/crashes
        /// <summary>
        /// Loads the serialized synchronizer from a given file. Does not create a new file if none is found. Handles corrupted files gracefully.
        /// </summary>
        /// <param name="path">Path to the serialized image of the synchronizer.</param>
        /// <returns>A reconstructed synchronizer instance, or a blank one if none is found.</returns>
        public static MyEncounterGpsSynchronizer LoadFromFile(string path)
        {
            MyEncounterGpsSynchronizer synchronizer = null;
            try
            {
                if (File.Exists(path))
                {
                    using (var fileStream = File.OpenRead(path))
                    {
                        synchronizer = (MyEncounterGpsSynchronizer)_xmlSerializer.Deserialize(fileStream);
                        synchronizer.AfterDeserialized(path);
                        Plugin.Instance.Logger.Info($"GPS Synchronizer image successfully loaded from '{path}'.");
                    }
                }
                else
                {
                    Plugin.Instance.Logger.Error($"GPS Synchronizer image not found at '{path}'! Currently active encounters will not be synchronized.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Error(ex, $"GPS Synchronizer image loading failure at path '{path}'!");
                try
                {
                    var timestamp = $"{DateTime.Now:yyyyMMdd-hhmmss}";
                    var newPath = $"{path}.corrupted.{timestamp}.txt";
                    Plugin.Instance.Logger.Info($"Renaming corrupted synchronizer image file: {path} => {newPath}");
                    File.Move(path, newPath);
                }
                catch (Exception e)
                {
                    Plugin.Instance.Logger.Error(e, "An unknown error has occurred renaming corrupted synchronizer image file.");
                }
            }
            if (synchronizer == null)
            {
                synchronizer = new MyEncounterGpsSynchronizer();
                synchronizer.AfterLoad(path);
            }
            return synchronizer;
        }

        void AfterDeserialized(string path)
        {
            _fromDeserializedImage = true;
            AfterLoad(path);
        }
        void AfterLoad(string filePath) => _filePath = filePath;

        /// <summary>
        /// Serializes and saves the synchronizer's image to the path provided on plugin load. Called from a postfix patch to the session's Save() method.
        /// </summary>
        public void SaveToFile()
        {
            if (!string.IsNullOrEmpty(_filePath))
            {
                if (_waitingToSave)
                {
                    try
                    {
                        if (!File.Exists(_filePath))
                            Plugin.Instance.Logger.Info($"GPS Synchronizer image not found for overwrite at '{_filePath}'. A new file will be created.");
                        using (var fileStream = File.OpenWrite(_filePath))
                        {
                            _xmlSerializer.Serialize(fileStream, this);
                            Plugin.Instance.Logger.Info($"GPS Synchronizer image successfully saved to '{_filePath}'.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Instance.Logger.Error(ex, $"GPS Synchronizer image save failure at '{_filePath}'!");
                    }
                }
                else
                {
                    Plugin.Instance.Logger.Info($"GPS Synchronizer image save not required - no changes since last save.");
                }
            }
            else
            {
                Plugin.Instance.Logger.Error("GPS Synchronizer image save location has not been determined on plugin load! Unable to save data!");
            }
        }
        /// <summary>
        /// Execute on session load to load deserialized GPSes and reconciliate with the game's internal entries.
        /// </summary>
        public void OnSessionLoaded()
        {
            Plugin.Instance.Logger.Info($"Torch session loaded. Initializing from {SerializableGPSes.Count} GPS entries.");

            var existingGpsMap = (ConcurrentDictionary<long, long>)m_encountersGps_fieldInfo.GetValue(MySession.Static.GetComponent<MyGlobalEncountersGenerator>());

            var knownEncounters = new HashSet<long>();

            // Convert remembered GPSes that the game tracks to real GPSes, forget the rest
            for (int i = SerializableGPSes.Count - 1; i >= 0; i--)
            {
                if (existingGpsMap.ContainsKey(SerializableGPSes[i].encounterId))
                {
                    _actualGPSes[SerializableGPSes[i].encounterId] = SerializableGPSes[i].gpsData.ToRealGps();
                    knownEncounters.Add(SerializableGPSes[i].encounterId);
                }
                else
                {
                    SerializableGPSes.RemoveAt(i);
                }
            }

            // Create null entries for game-tracked GPSes that we didn't know about. Null entries are ignored.
            foreach (var encounterId in existingGpsMap.Keys)
                if (knownEncounters.Add(encounterId))
                    _actualGPSes[encounterId] = null;

            // Clear nonexistent (stale) GPS entries from players
            foreach (var playerSync in PlayerSyncEntries)
                for (int i = playerSync.ActiveEncounterGpses.Count - 1; i >= 0; i--)
                    if (!knownEncounters.Contains(playerSync.ActiveEncounterGpses[i]))
                        playerSync.ActiveEncounterGpses.RemoveAt(i);
        }
        #endregion

        #region Player and encounter handling
        /// <summary>
        /// Enqueues a returning (joining) player for GPS snychronization.
        /// </summary>
        /// <param name="identity"><see cref="MyIdentity"/> instance describing the joining player.</param>
        public void RegisterNewPlayerWithExistingIdentity(MyIdentity identity)
        {
            if (Plugin.Instance.Config.GPSSynchronization)
                _joinedPlayersQueue.Enqueue(identity);
        }

        /// <summary>
        /// Called on the game's per-tick update to handle joining players.
        /// </summary>
        public void Update()
        {
            ProcessQueuedPlayers();
        }

        void ProcessQueuedPlayers()
        {
            if (MySession.Static.GameplayFrameCounter % 60 != 0)
                return;

            if (_joinedPlayersQueue.Count != 0)
            {
                if (!_fromDeserializedImage)
                {
                    Plugin.Instance.Logger.Info($"GPS Synchronizer did not load from file. Encounters spawned before this session's start will not be synchronized.");
                }

                // relates encounter entityids with entityids of grids that GPSes are bound to (unique per GPS, which is what matters)
                var existingGpsMap = (ConcurrentDictionary<long, long>) m_encountersGps_fieldInfo.GetValue(MySession.Static.GetComponent<MyGlobalEncountersGenerator>());

                int counter = 0;

                while (_joinedPlayersQueue.TryDequeue(out MyIdentity identity))
                {
                    var entry = PlayerSyncEntries.Find(e => e.IdentityId == identity.IdentityId);
                    if (entry == null)
                    {
                        entry = MyPlayerSynchronizationEntry.Create(identity.IdentityId);
                        PlayerSyncEntries.Add(entry);
                    }
                    foreach (var gpsEntry in existingGpsMap)
                    {
                        MyGps gps = null;
                        if (!_actualGPSes.TryGetValue(gpsEntry.Key, out gps))
                        {
                            // Plugin does not know about this GPS, likely because it was created before its serialized XML was created. Setting to null to skip.
                            _actualGPSes[gpsEntry.Key] = gps;
                        }
                        entry.SynchronizeWithGps(gpsEntry.Key, gpsEntry.Value, gps);
                    }
                    counter++;
                }

                Plugin.Instance.Logger.Info($"Processed synchronization for {counter} new player(s).");

                _waitingToSave = true;
            }
        }

        /// <summary>
        /// Dynamically called from a transpiler patch to register a global encounter and its GPS for synchronization.
        /// </summary>
        /// <param name="encounterId">EntityId of the encounter.</param>
        /// <param name="gps">GPS created for the encounter.</param>
        public void OnGlobalEncounterSpawned(long encounterId, MyGps gps)
        {
            _actualGPSes[encounterId] = gps;
            SerializableGPSes.Add((encounterId, MySerializableGpsData.FromRealGps(gps)));

            foreach (var entry in PlayerSyncEntries)
                entry.EncounterGpsSpawned(encounterId);

            _waitingToSave = true;
        }

        /// <summary>
        /// Called from a postfix patch to unregister a global encounter and its GPS from synchronization when despawned.
        /// </summary>
        /// <param name="encounterId"></param>
        public void OnGlobalEncounterDespawned(long encounterId)
        {
            _actualGPSes.Remove(encounterId);
            SerializableGPSes.Remove(SerializableGPSes.Find(entry => entry.encounterId == encounterId));

            foreach (var playerSync in PlayerSyncEntries)
                playerSync.EncounterGpsDespawned(encounterId);

            _waitingToSave = true;
        }
        #endregion
    }
    /// <summary>
    /// Stores Global Encounter GPS data for serialization.
    /// </summary>
    public class MySerializableGpsData
    {
        public bool ShowOnHud, AlwaysVisible;
        public string Name, DisplayName, Description;
        public Vector3D Coords;
        public Color GPSColor;
        public long EntityId;
        /// <summary>
        /// Creates a <see cref="MyGps"/> instance from this serializable instance.
        /// </summary>
        /// <returns>A real instance of the GPS.</returns>
        public MyGps ToRealGps()
        {
            var gps = new MyGps()
            {
                ShowOnHud = ShowOnHud,
                AlwaysVisible = AlwaysVisible,
                IsGlobalEncounterGPS = true,
                Name = Name,
                DisplayName = DisplayName,
                Description = Description,
                DiscardAt = null,
                Coords = Coords,
                GPSColor = GPSColor,
                EntityId = EntityId
            };
            return gps;
        }
        /// <summary>
        /// Creates a serializable instance from the game's <see cref="MyGps"/> instance.
        /// </summary>
        /// <param name="realGps">The game's instance of the GPS.</param>
        /// <returns>A serializable instance for the encounter GPS.</returns>
        public static MySerializableGpsData FromRealGps(MyGps realGps)
            => new MySerializableGpsData()
            {
                ShowOnHud = realGps.ShowOnHud,
                AlwaysVisible = realGps.AlwaysVisible,
                Name = realGps.Name,
                DisplayName = realGps.DisplayName,
                Description = realGps.Description,
                Coords = realGps.Coords,
                GPSColor = realGps.GPSColor,
                EntityId = realGps.EntityId
            };
    }
    /// <summary>
    /// An entry for synchronizing a player with the server's Global Encounter GPSes.
    /// </summary>
    public class MyPlayerSynchronizationEntry
    {
        public long IdentityId;
        public List<long> ActiveEncounterGpses;
        /// <summary>
        /// Creates an entry for synchronizing a player with the server's Global Encounter GPSes.
        /// </summary>
        /// <param name="identityId">The IdentityId of the player.</param>
        public static MyPlayerSynchronizationEntry Create(long identityId)
            => new MyPlayerSynchronizationEntry()
            {
                IdentityId = identityId,
                ActiveEncounterGpses = new List<long>()
            };
        /// <summary>
        /// Synchronizes a player with the given encounter's GPS if not already active with it.
        /// </summary>
        /// <param name="encounterId">EntityId of the encounter to check for.</param>
        /// <param name="gpsEntityId">EntityId of the spawned grid the GPS is bound to.</param>
        /// <param name="gps">GPS to send to the player if missing.</param>
        public void SynchronizeWithGps(long encounterId, long gpsEntityId, MyGps gps)
        {
            if (!ActiveEncounterGpses.Contains(encounterId))
            {
                if (gps != null) // GPS is unknown. We treat it as if we already know it.
                    MySession.Static.Gpss.SendAddGpsRequest(IdentityId, ref gps, gpsEntityId);
                ActiveEncounterGpses.Add(encounterId);
            }
        }
        /// <summary>
        /// Signals that a given encounter has spawned while this player was online to receive its GPS normally.
        /// </summary>
        /// <param name="encounterId">EntityId of the encounter that has spawned.</param>
        public void EncounterGpsSpawned(long encounterId)
        {
            ActiveEncounterGpses.Add(encounterId);
        }
        /// <summary>
        /// Signals that a given encounter has despawned and can be removed from the player's memory
        /// </summary>
        /// <param name="encounterId">EntityId of the encounter that has despawned.</param>
        public void EncounterGpsDespawned(long encounterId)
        {
            ActiveEncounterGpses.Remove(encounterId);
        }
    }
}
