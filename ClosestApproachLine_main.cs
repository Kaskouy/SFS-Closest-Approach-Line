using System;

using ModLoader;
using HarmonyLib;

namespace ClosestApproachLine
{
    public class Main : Mod
    {
        const string C_STR_MOD_ID = "CLOSEST_APPROACH_LINE";
        const string C_STR_MOD_NAME = "Closest approach line";
        const string C_STR_AUTHOR = "Altaïr";
        const string C_STR_MODLOADER_VERSION = "1.5.7";
        const string C_STR_MOD_VERSION = "V2.3";
        const string C_STR_MOD_DESCRIPTION = "This mod adds a closest approach line when navigation is active.";

        public override string ModNameID => C_STR_MOD_ID;

        public override string DisplayName => C_STR_MOD_NAME;

        public override string Author => C_STR_AUTHOR;

        public override string MinimumGameVersionNecessary => C_STR_MODLOADER_VERSION;

        public override string ModVersion => C_STR_MOD_VERSION;

        public override string Description => C_STR_MOD_DESCRIPTION;


        public Main() : base()
        {
            Harmony.DEBUG = false;
            FileLog.logPath = "C:\\Users\\JB\\Desktop\\Jeux\\SFS PC\\ClosestApproachLine\\ClosestApproachLine\\Logs_ClosestApproachLine.txt";
            //FileLog.Log("TRACES ACTIVATED"); // logs available in harmony.log.txt on desktop
        }

        // This initializes the patcher. This is required if you use any Harmony patches
        public static Harmony patcher;

        public override void Load()
        {
            // Tells the loader what to run when your mod is loaded
        }

        public override void Early_Load()
        {
            // This method runs before anything from the game is loaded. This is where you should apply your patches, as shown below.

            // The patcher uses an ID formatted like a web domain
            Main.patcher = new Harmony($"{C_STR_MOD_ID}.{C_STR_MOD_NAME}.{C_STR_AUTHOR}");

            // This pulls your Harmony patches from everywhere in the namespace and applies them.
            Main.patcher.PatchAll();

            //base.early_load();
        }
    }
}

