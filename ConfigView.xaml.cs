using System.Windows.Controls;

namespace GlobalEncounterUnlimiter
{
    // ReSharper disable once UnusedType.Global
    // ReSharper disable once RedundantExtendsListEntry
    public partial class ConfigView : UserControl
    {
        public ConfigView()
        {
            InitializeComponent();
            DataContext = Plugin.Instance.Config;
        }
    }
}