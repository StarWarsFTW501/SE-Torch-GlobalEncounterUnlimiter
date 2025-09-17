using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorchPlugin
{
    internal interface IMyPluginConfig
    {
        bool Enabled { get; set; }
    }
}
