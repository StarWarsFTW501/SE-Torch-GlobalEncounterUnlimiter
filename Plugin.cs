using GlobalEncounterUnlimiter;
using HarmonyLib;
using Sandbox.Game;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using SpaceEngineers.Game.SessionComponents;
using Steamworks;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;
using VRage.FileSystem;
using VRage.Utils;

namespace GlobalEncounterUnlimiter
{
    public class Plugin : TorchPluginBase, IWpfPlugin
    {
        const string PLUGIN_NAME = "GlobalEncounterUnlimiter";

        public static Plugin Instance;

        public MyEncounterGpsSynchronizer EncounterGpsSynchronizer;

        internal MyLogger Logger { get; private set; }

        internal MyPluginConfig Config => _config?.Data;
        private PersistentConfig<MyPluginConfig> _config;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            Instance = this;

            Logger = new MyLogger(PLUGIN_NAME, PLUGIN_NAME);

            var configPath = Path.Combine(StoragePath, $"{PLUGIN_NAME}.cfg");
            _config = PersistentConfig<MyPluginConfig>.Load(Logger, configPath);

            var synchronizerPath = Path.Combine(StoragePath, $"{PLUGIN_NAME}_{nameof(MyEncounterGpsSynchronizer)}.xml");
            EncounterGpsSynchronizer = MyEncounterGpsSynchronizer.LoadFromFile(synchronizerPath);

            var harmony = new Harmony(PLUGIN_NAME);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            var method = typeof(MySession).GetMethod("Save",
                new Type[]
                {
                    typeof(MySessionSnapshot).MakeByRefType(),
                    typeof(string),
                    typeof(Action<SaveProgress>)
                });
            harmony.Patch(method, null, typeof(MyPatches).GetMethod("MySession_Save_DynamicPostfix"));

            var manager = torch.Managers.GetManager<TorchSessionManager>();
            if (manager != null)
            {
                manager.SessionStateChanged += OnSessionStateChanged;
            }
            else
            {
                Logger.Error("Could not attach session state change handler. Problems may occur on use of deserialized synchronizer data.");
            }

            Logger.Info("Plugin initialized.");
        }

        void OnSessionStateChanged(ITorchSession session, TorchSessionState newState)
        {
            if (newState == TorchSessionState.Loaded)
            {
                EncounterGpsSynchronizer.OnSessionLoaded();
            }
        }

        public override void Update()
        {
            base.Update();
            EncounterGpsSynchronizer.Update();
        }

        public UserControl GetControl() => _configurationView ?? (_configurationView = new ConfigView());
        private UserControl _configurationView;
    }
}