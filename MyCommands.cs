using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace TorchPlugin
{
    public class MyCommands : CommandModule
    {
        /// <summary>
        /// Responds to the issued command in chat.
        /// </summary>
        /// <param name="message">Message to respond with.</param>
        void Respond(string message)
        {
            Context?.Respond(message);
        }

        [Command("plugin examplecommand", "Example command description.")]
        [Permission(MyPromoteLevel.Admin)]
        public void ExampleCommand()
        {
            Respond("The example command was executed.");
        }
    }
}
