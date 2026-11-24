using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.RuntimeDetour;

namespace HKMP.Rounds.Server
{
    internal static class HkmpGlobalSettingsSaveGuard
    {
        private static Hook _hook;
        private static Type _hkmpModType;
        private static Type _modSettingsType;
        private static MethodInfo _onSaveGlobalMethod;
        private static MethodInfo _memberwiseCloneMethod;
        private static PropertyInfo _serverSettingsProperty;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                Assembly hkmpAssembly = FindHkmpAssembly();
                if (hkmpAssembly == null)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Global settings save guard: HKMP assembly was not found.");
                    return;
                }

                _hkmpModType = hkmpAssembly.GetType("Hkmp.HkmpMod", false);
                _modSettingsType = hkmpAssembly.GetType("Hkmp.Game.Settings.ModSettings", false);
                if (_hkmpModType == null || _modSettingsType == null)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Global settings save guard: HKMP save types were not found.");
                    return;
                }

                _onSaveGlobalMethod = _hkmpModType.GetMethod(
                    "OnSaveGlobal",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _serverSettingsProperty = _modSettingsType.GetProperty(
                    "ServerSettings",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _memberwiseCloneMethod = typeof(object).GetMethod(
                    "MemberwiseClone",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (_onSaveGlobalMethod == null ||
                    _onSaveGlobalMethod.ReturnType != _modSettingsType ||
                    _onSaveGlobalMethod.GetParameters().Length != 0 ||
                    _serverSettingsProperty == null ||
                    !_serverSettingsProperty.CanRead ||
                    !_serverSettingsProperty.CanWrite ||
                    _memberwiseCloneMethod == null)
                {
                    Modding.Logger.LogError("[HKMP.Rounds] Global settings save guard: HKMP save method layout was unexpected.");
                    return;
                }

                MethodInfo dynamicHook = CreateHookMethod();
                _hook = new Hook(_onSaveGlobalMethod, dynamicHook);
                _initialized = _hook != null;
                if (_initialized)
                {
                    Modding.Logger.Log("[HKMP.Rounds] Global settings save guard initialized.");
                }
            }
            catch (Exception exception)
            {
                _hook = null;
                _initialized = false;
                Modding.Logger.LogError("[HKMP.Rounds] Global settings save guard initialization failed: " + exception);
            }
        }

        private static MethodInfo CreateHookMethod()
        {
            Type originalDelegateType = Expression.GetDelegateType(
                _hkmpModType,
                _modSettingsType);

            DynamicMethod dynamicMethod = new DynamicMethod(
                "HKMPRounds_OnSaveGlobalHook",
                _modSettingsType,
                new[] { originalDelegateType, _hkmpModType },
                typeof(HkmpGlobalSettingsSaveGuard).Module,
                true);

            ILGenerator il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Newarr, typeof(object));
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stelem_Ref);

            MethodInfo dynamicInvoke = typeof(Delegate).GetMethod(
                "DynamicInvoke",
                new[] { typeof(object[]) });
            if (dynamicInvoke == null)
            {
                throw new MissingMethodException("Delegate.DynamicInvoke was not found.");
            }

            il.Emit(OpCodes.Callvirt, dynamicInvoke);
            il.Emit(OpCodes.Castclass, _modSettingsType);
            il.Emit(OpCodes.Call, typeof(HkmpGlobalSettingsSaveGuard).GetMethod(
                nameof(PrepareSettingsForSave),
                BindingFlags.Static | BindingFlags.NonPublic));
            il.Emit(OpCodes.Castclass, _modSettingsType);
            il.Emit(OpCodes.Ret);
            return dynamicMethod;
        }

        private static object PrepareSettingsForSave(object modSettings)
        {
            if (modSettings == null || !PvpModeServerSettings.IsActive)
            {
                return modSettings;
            }

            try
            {
                object liveServerSettings = _serverSettingsProperty.GetValue(modSettings, null);
                if (liveServerSettings == null)
                {
                    return modSettings;
                }

                object saveSettings = _memberwiseCloneMethod.Invoke(modSettings, null);
                object saveServerSettings = _memberwiseCloneMethod.Invoke(liveServerSettings, null);
                if (!PvpModeServerSettings.PreparePersistentServerSettings(saveServerSettings))
                {
                    return modSettings;
                }

                _serverSettingsProperty.SetValue(saveSettings, saveServerSettings, null);
                return saveSettings;
            }
            catch (Exception exception)
            {
                Modding.Logger.LogError("[HKMP.Rounds] Could not protect HKMP global settings during save: " + exception);
                return modSettings;
            }
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

                try
                {
                    if (assembly.GetType("Hkmp.HkmpMod", false) != null)
                    {
                        return assembly;
                    }
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
