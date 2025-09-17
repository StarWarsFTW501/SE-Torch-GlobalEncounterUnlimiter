using HarmonyLib;
using Sandbox.Game;
using System.IO;
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

namespace TorchPlugin
{
    public class Plugin : TorchPluginBase, IWpfPlugin
    {
        const string PLUGIN_NAME = "TorchPlugin";

        public static Plugin Instance;

        internal MyLogger Logger { get; private set; }

        internal IMyPluginConfig Config => _config?.Data;
        private PersistentConfig<MyPluginConfig> _config;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            Instance = this;

            Logger = new MyLogger(PLUGIN_NAME, PLUGIN_NAME);

            var configPath = Path.Combine(StoragePath, $"{PLUGIN_NAME}.cfg");
            _config = PersistentConfig<MyPluginConfig>.Load(Logger, configPath);

            var harmony = new Harmony(PLUGIN_NAME);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.Info("Plugin initialized.");
        }

        public UserControl GetControl() => _configurationView ?? (_configurationView = new ConfigView());
        private UserControl _configurationView;
    }
}