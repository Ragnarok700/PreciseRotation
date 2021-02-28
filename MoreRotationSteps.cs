using BepInEx;
using BepInEx.Configuration;
using DebugUtils;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace PreciseRotation
{
    [BepInPlugin("com.github.johndowson.PreciseRotation", "PreciseRotation", "1.0.0")]
    public class PreciseRotation : BaseUnityPlugin
    {
        public static ConfigEntry<int> RotationSteps;
        public static ConfigEntry<KeyCode> RotationModifier;

        private static readonly Harmony harmony = new(typeof(PreciseRotation).GetCustomAttributes(typeof(BepInPlugin), false)
            .Cast<BepInPlugin>()
            .First()
            .GUID);
#pragma warning disable IDE0051 // Remove unused private members
        private void Awake()
        {
            RotationModifier = Config.Bind("General", "RotationModifier", KeyCode.LeftAlt, "Key to toggle precise rotation");
            RotationSteps = Config.Bind("General", "RotationSteps", 16, "Number of rotation steps per 180 degrees, Valheim's default is 8");

            harmony.PatchAll();
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        public static class PieceRotation_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (var i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt &&
                        codes[i + 1].opcode == OpCodes.Ldc_R4 &&
                        codes[i + 2].opcode == OpCodes.Ldc_R4 &&
                        codes[i + 3].opcode == OpCodes.Ldarg_0 &&
                        codes[i + 4].opcode == OpCodes.Ldfld &&
                        codes[i + 5].opcode == OpCodes.Conv_R4 &&
                        codes[i + 6].opcode == OpCodes.Mul &&
                        codes[i + 7].opcode == OpCodes.Ldc_R4 &&
                        codes[i + 8].opcode == OpCodes.Call)

                    {
                        codes[i + 2] = CodeInstruction.Call(typeof(PieceRotation_Patch), "GetRotationSteps");
                    }
                }
                return codes.AsEnumerable();
            }
            public static float GetRotationSteps()
            {
                bool rotationModKeyDown = Input.GetKey(RotationModifier.Value);
                float rotationPerStep = 180.0f / 8;
                if (rotationModKeyDown)
                    rotationPerStep = 180.0f / RotationSteps.Value;
                
                return rotationPerStep;
            }
        }
    }
}
