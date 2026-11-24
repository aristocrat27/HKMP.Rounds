using System;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace HKMP.Rounds.Client
{
    internal static class OneShotElegyFix
    {
        private static bool _initialized;
        private static ILHook _hook;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            try
            {
                Assembly hkmpAssembly = FindHkmpAssembly();
                if (hkmpAssembly == null)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] OneShot Elegy fix: HKMP assembly was not found.");
                    return;
                }

                Type slashBaseType = hkmpAssembly.GetType("Hkmp.Animation.Effects.SlashBase", false);
                if (slashBaseType == null)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] OneShot Elegy fix: SlashBase type was not found.");
                    return;
                }

                Type slashType = slashBaseType.GetNestedType("SlashType", BindingFlags.NonPublic);
                if (slashType == null)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] OneShot Elegy fix: SlashType was not found.");
                    return;
                }

                MethodInfo playMethod = slashBaseType.GetMethod(
                    "Play",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(GameObject), typeof(bool[]), typeof(GameObject), slashType },
                    null);

                if (playMethod == null)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] OneShot Elegy fix: SlashBase.Play was not found.");
                    return;
                }

                _hook = new ILHook(playMethod, PatchSlashBasePlay);
                Modding.Logger.Log("[HKMP.Rounds] OneShot Elegy fix initialized.");
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError("[HKMP.Rounds] OneShot Elegy fix failed: " + exception);
                _hook = null;
            }
        }

        public static void Clear()
        {
            if (_hook != null)
            {
                try
                {
                    _hook.Dispose();
                }
                catch (Exception exception)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] OneShot Elegy fix disposal failed: " + exception);
                }

                _hook = null;
            }

            _initialized = false;
        }

        private static Assembly FindHkmpAssembly()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                AssemblyName name = assembly.GetName();
                if (name != null && string.Equals(name.Name, "HKMP", StringComparison.OrdinalIgnoreCase))
                {
                    return assembly;
                }
            }

            return null;
        }

        private static void PatchSlashBasePlay(ILContext context)
        {
            try
            {
                ILCursor cursor = new ILCursor(context);
                if (!cursor.TryGotoNext(
                    MoveType.After,
                    instruction => instruction.MatchLdloc(1)))
                {
                    Modding.Logger.LogError("[HKMP.Rounds] OneShot Elegy fix could not find the health check.");
                    return;
                }

                Instruction nextInstruction = cursor.Next;
                if (nextInstruction == null ||
                    (nextInstruction.OpCode != OpCodes.Brtrue && nextInstruction.OpCode != OpCodes.Brtrue_S))
                {
                    Modding.Logger.LogError("[HKMP.Rounds] OneShot Elegy fix found an unexpected SlashBase.Play layout.");
                    return;
                }

                cursor.Emit(OpCodes.Ldloc, 2);
                cursor.EmitDelegate(new Func<bool, bool, bool>(AllowElegyAtOneHealth));
                Modding.Logger.Log("[HKMP.Rounds] OneShot Elegy fix patched SlashBase.Play.");
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError("[HKMP.Rounds] OneShot Elegy patch failed: " + exception);
            }
        }

        private static bool AllowElegyAtOneHealth(bool isOnFullHealth, bool hasFuryCharm)
        {
            return isOnFullHealth || hasFuryCharm;
        }
    }
}
