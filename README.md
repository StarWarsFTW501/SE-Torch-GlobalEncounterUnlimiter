## Global Encounter Unlimiter

This plugin removes the hardcoded limits imposed by Space Engineers on Global Encounter removal timers and ensures synchronization of existing encounters' GPS coordinates to players joining after their generation.

### Removal timers
By default, the GlobalEncounterMaxRemovalTimer setting gets clamped to between 91 and 180 minutes, the GlobalEncounterMinRemovalTimer setting then clamps to between 90 and the maximum timer.

Global Encounter Unlimiter alters the encounter generation code to remove the limits on the maximum timer (**Warning**: You can set it to a negative value. Bad things will happen!) and change them on the minimum timer to between 0 (instead of 90) and the maximum timer.

The exact reason Keen imposed these limits is a great mystery. Maybe it had a reason, so let us hope nothing breaks down the line!

### GPS Synchronization
Global Encounter Unlimiter hooks into the game's code to intercept the generation and removal of Global Encounters and their GPS coordinates, as well as the code handling the joining of players. It remembers who saw each currently active encounter generate and creates a new GPS for any new players joining during its lifetime.

Data related to GPS Synchronization can be viewed (and technically modified, though I don't know why you'd do that) in the `GlobalEncounterUnlimiter_MyEncounterGpsSynchronizer.xml` file in your Instance directory. This file serves as the plugin's memory. If Global Encounter Unlimiter does not remember an encounter when the game loads up, perhaps because it just got installed or its memory got deleted, it will be ignored from synchronization - no duplicate entries.