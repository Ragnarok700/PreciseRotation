using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace PreciseRotation
{
    [BepInPlugin("com.github.johndowson.PreciseRotation", "PreciseRotation", "3.0.0")]
    public class PreciseRotation : BaseUnityPlugin
    {
        public static ConfigEntry<int> RotationSteps;
        public static ConfigEntry<bool> OverrideRotation;
        public static ConfigEntry<KeyCode> RotationModifier;
        public static ConfigEntry<KeyCode> NextAxis;

        public static bool Precision;
        public static int RotationX, RotationY, RotationZ = 0;
        public static Axis CurrentAxis = Axis.Y;
        public enum Axis
        {
            X,
            Y,
            Z,
        }

        private static readonly Harmony harmony = new(typeof(PreciseRotation).GetCustomAttributes(typeof(BepInPlugin), false)
            .Cast<BepInPlugin>()
            .First()
            .GUID);
#pragma warning disable IDE0051 // Remove unused private members
        private void Awake()
        {
            RotationModifier = Config.Bind("General", "RotationModifier", KeyCode.LeftAlt, "Key to toggle precise rotation");
            OverrideRotation = Config.Bind("General", "OverrideRotation", false, "Override rotation steps without the need to hold a button.");
            RotationSteps = Config.Bind("General", "RotationSteps", 16, "Number of rotation steps per 180 degrees, Valheim's default is 8");
            NextAxis = Config.Bind("General", "NextAxis", KeyCode.V, "Key to toggle rotation axis");
            Precision = OverrideRotation.Value;
            harmony.PatchAll();
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        [HarmonyPatch(typeof(Player))]
        public static class PieceRotation_Patch
        {

            [HarmonyPatch("UpdatePlacementGhost")]
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> UpdatePlacementGhostPatch(IEnumerable<CodeInstruction> instructions)
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
                        for (int y = 1; y < 8; y++)
                            codes[i + y].opcode = OpCodes.Nop;
                        codes[i + 8] = CodeInstruction.Call(typeof(PieceRotation_Patch), "Rotate");
                    }
                }
                return codes.AsEnumerable();
            }
            [HarmonyPatch("UpdatePlacement")]
            [HarmonyPrefix]
            static void RememberOldRotation(Player __instance, bool takeInput, float dt, ref int __state)
            {
                __state = __instance.m_placeRotation;
            }

            [HarmonyPatch("UpdatePlacement")]
            [HarmonyPostfix]
            static void StoreRotationDifference(Player __instance, bool takeInput, float dt, ref int __state)
            {
                int difference = -(__state - __instance.m_placeRotation);
                switch (CurrentAxis)
                {
                    case Axis.X:
                        RotationX += difference;
                        break;
                    case Axis.Y:
                        RotationY += difference;
                        break;
                    case Axis.Z:
                        RotationZ += difference;
                        break;
                }
            }

            [HarmonyPatch("Update")]
            [HarmonyPostfix]
            static void UpdateRotationAxis(Player __instance)
            {
                if (Input.GetKeyDown(NextAxis.Value))
                    ToggleAxis();
            }
            [HarmonyPatch("Update")]
            [HarmonyPostfix]
            static void UpdatePrecision(Player __instance)
            {
                if (!OverrideRotation.Value && Input.GetKeyDown(RotationModifier.Value))
                {
                    Precision = !Precision;
                    Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Pecision toggled: " + Precision, 0, null);
                }
            }


            public static Quaternion Rotate()
            {
                var rotationSteps = GetRotationSteps();
                return Quaternion.Euler(rotationSteps * (float)RotationX, rotationSteps * (float)RotationY, rotationSteps * (float)RotationZ);

            }
            public static float GetRotationSteps()
            {
                float rotationPerStep = 180.0f / 8;
                if (Precision)
                    rotationPerStep = 180.0f / RotationSteps.Value;

                return rotationPerStep;
            }
            public static void ToggleAxis()
            {
                switch (CurrentAxis)
                {
                    case Axis.X:
                        CurrentAxis = Axis.Y;
                        break;
                    case Axis.Y:
                        CurrentAxis = Axis.Z;
                        break;
                    case Axis.Z:
                        CurrentAxis = Axis.X;
                        break;
                }
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Rotation toggled: " + CurrentAxis, 0, null);
            }
        }
    }
}
