using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace GlobalEncounterUnlimiter
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
        
        [Command("globalencounterunlimiter sync", "Gets or sets whether synchronization of Unidentified Signal GPSes with joining players is enabled.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Sync(string value = null)
        {
            if (value != null)
            {
                if (bool.TryParse(value, out bool result))
                {
                    Plugin.Instance.Config.GPSSynchronization = result;
                    Respond($"GPS synchronization is now {(result ? "ON" : "OFF")}");
                }
                else
                {
                    Respond($"Could not interpret '{value}' as a boolean (true/false) value!");
                }
            }
            else
            {
                Respond($"GPS synchronization is {(Plugin.Instance.Config.GPSSynchronization ? "ON" : "OFF")}");
            }
        }

        [Command("globalencounterunlimiter restriction", "Gets or sets whether the restriction of Global Encounter spawn locations to a plugin-defined region is enabled.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Restriction(string value = null)
        {
            if (value != null)
            {
                if (bool.TryParse(value, out bool result))
                {
                    Plugin.Instance.Config.LocationRestriction = result;
                    Respond($"Location restriction is now {(result ? "ON" : "OFF")}");
                }
                else
                {
                    Respond($"Could not interpret '{value}' as a boolean (true/false) value!");
                }
            }
            else
            {
                Respond($"Location restriction is {(Plugin.Instance.Config.LocationRestriction ? "ON" : "OFF")}");
            }
        }
        [Command("globalencounterunlimiter restrictioncenter", "Sets the center of the region to restrict Encounter spawn locations to. Only has an effect if Location restriction is active!")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetRestrictionCenter(string x = null, string y = null, string z = null)
        {
            if (x == null)
            {
                Respond("Please provide X, Y and Z coordinates or a GPS coordinate to use as a center location.");
                return;
            }
            if (y == null && z == null)
            {
                if (MyPatchUtilities.TryParseGPS(x, out var location))
                {
                    Plugin.Instance.Config.LocationRestrictionCenterX = location.X;
                    Plugin.Instance.Config.LocationRestrictionCenterY = location.Y;
                    Plugin.Instance.Config.LocationRestrictionCenterZ = location.Z;
                    Respond($"Succcessfully set the center of the Encounter spawn region to the GPS coordinate {x}");
                    return;
                }
                if (y == null)
                {
                    Respond("Could not interpret the command input as a GPS coordinate. Please provide X, Y and Z coordinates or a GPS coordinate to use as a center location.");
                    return;
                }
                Respond("Only X and Y coordinates have been provided. Please provide X, Y and Z coordinates or a GPS coordinate to use as a center location.");
                return;
            }
            if (!double.TryParse(x, out double valueX))
            {
                Respond($"Could not interpret X coordinate '{x}' as a numerical value!");
                return;
            }
            if (!double.TryParse(y, out double valueY))
            {
                Respond($"Could not interpret Y coordinate '{y}' as a numerical value!");
                return;
            }
            if (!double.TryParse(z, out double valueZ))
            {
                Respond($"Could not interpret Z coordinate '{z}' as a numerical value!");
                return;
            }
            Plugin.Instance.Config.LocationRestrictionCenterX = valueX;
            Plugin.Instance.Config.LocationRestrictionCenterY = valueY;
            Plugin.Instance.Config.LocationRestrictionCenterZ = valueZ;
            Respond($"Succcessfully set the center of the Encounter spawn region to {valueX}, {valueY}, {valueZ}");
        }
        [Command("globalencounterunlimiter restrictionminradius", "Sets the minimum radius around the center of the region to restrict Encounter spawn locations to. Only has an effect if Location restriction is active!")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetRestrictionMinRadius(string value = null)
        {
            if (value == null)
            {
                Respond($"Encounter spawn region minimum radius is {Plugin.Instance.Config.LocationRestrictionMinRadius} meters.");
                return;
            }
            if (!int.TryParse(value, out int radius))
            {
                Respond($"Could not interpret minimum radius '{value}' as a numerical value!");
                return;
            }
            Plugin.Instance.Config.LocationRestrictionMinRadius = radius;
            Respond($"Successfully set the minimum radius of the Encounter spawn region to {radius} meters.");
        }
        [Command("globalencounterunlimiter restrictionmaxradius", "Sets the maximum radius around the center of the region to restrict Encounter spawn locations to. Only has an effect if Location restriction is active!")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetRestrictionMaxRadius(string value = null)
        {
            if (value == null)
            {
                Respond($"Encounter spawn region maximum radius is {Plugin.Instance.Config.LocationRestrictionMaxRadius} meters.");
                return;
            }
            if (!int.TryParse(value, out int radius))
            {
                Respond($"Could not interpret maximum radius '{value}' as a numerical value!");
                return;
            }
            Plugin.Instance.Config.LocationRestrictionMaxRadius = radius;
            Respond($"Successfully set the maximum radius of the Encounter spawn region to {radius} meters.");
        }
        [Command("globalencounterunlimiter planetspawns", "Gets or sets whether or not Global Encounters are allowed to spawn in planets' orbits. Only has an effect if Location restriction is active!")]
        [Permission(MyPromoteLevel.Admin)]
        public void PlanetSpawns(string value = null)
        {
            if (value != null)
            {
                if (bool.TryParse(value, out bool result))
                {
                    Plugin.Instance.Config.LocationRestrictionAllowPlanets = result;
                    Respond($"Encounter spawns around planets now {(result ? "ON" : "OFF")}");
                }
                else
                {
                    Respond($"Could not interpret '{value}' as a boolean (true/false) value!");
                }
            }
            else
            {
                Respond($"Encounter spawns around planets are {(Plugin.Instance.Config.LocationRestrictionAllowPlanets ? "ON" : "OFF")}");
            }
        }
    }
}
