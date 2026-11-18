using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace EquipmentExperience
{
    /// <summary>
    /// A set of patches that only make sense together. Before patching, every game member the group depends on
    /// is looked up; if any is missing (renamed in a game update), the whole group is skipped with a log message
    /// instead of failing later inside game code. If a patch fails to apply, the ones already applied are undone.
    /// </summary>
    public sealed class PatchGroup
    {
        private readonly string _name;
        private readonly List<string> _missing = new List<string>();
        private readonly List<(MethodBase Original, MethodInfo Patch, HarmonyPatchType Kind)> _patches =
            new List<(MethodBase, MethodInfo, HarmonyPatchType)>();

        public PatchGroup(string name)
        {
            _name = name;
        }

        public string Name => _name;

        /// <summary>Game members (or patch parameters) this group needs that the running game doesn't have.</summary>
        public IReadOnlyList<string> Missing => _missing;

        public PatchGroup With(Func<PatchGroup, PatchGroup> requirements)
        {
            return requirements(this);
        }

        public PatchGroup RequireField(Type type, string name)
        {
            if (AccessTools.Field(type, name) == null)
            {
                _missing.Add($"field {type.Name}.{name}");
            }
            return this;
        }

        public PatchGroup RequireMethod(Type type, string name, params Type[] args)
        {
            if (AccessTools.Method(type, name, args.Length == 0 ? null : args) == null)
            {
                _missing.Add($"method {type.Name}.{name}");
            }
            return this;
        }

        /// <summary>
        /// Requires that a game method still calls another one, for transpilers that replace that call. Checked
        /// against the method's IL, so a game update that inlines or restructures the call disables the group.
        /// </summary>
        public PatchGroup RequireCall(Type type, string method, Type[] args, MethodInfo callee)
        {
            MethodBase original = AccessTools.Method(type, method, args);
            if (original == null || callee == null)
            {
                return this; // reported by Patch or RequireMethod
            }
            try
            {
                if (!CallsInIl(original, callee))
                {
                    _missing.Add($"call to {callee.DeclaringType?.Name}.{callee.Name} in {type.Name}.{method}");
                }
            }
            catch (Exception e)
            {
                _missing.Add($"readable IL of {type.Name}.{method} ({e.GetType().Name})");
            }
            return this;
        }

        /// <summary>
        /// Looks for a call or callvirt to callee in the raw IL. Reads the bytes directly instead of through Harmony,
        /// so tools/GameApiCheck can run it outside the game. Both methods must be in the same module for the
        /// metadata token to match.
        /// </summary>
        private static bool CallsInIl(MethodBase method, MethodInfo callee)
        {
            byte[] il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null || method.Module != callee.Module)
            {
                return false;
            }
            int token = callee.MetadataToken;
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if ((il[i] == 0x28 || il[i] == 0x6F) && BitConverter.ToInt32(il, i + 1) == token)
                {
                    return true;
                }
            }
            return false;
        }

        public PatchGroup Patch(Type patchClass, Type type, string method, Type[] args, HarmonyPatchType kind, string patchMethod)
        {
            MethodBase original = AccessTools.Method(type, method, args);
            if (original == null)
            {
                _missing.Add($"patch target {type.Name}.{method}({string.Join(", ", Array.ConvertAll(args, a => a.Name))})");
                return this;
            }
            MethodInfo patch = AccessTools.Method(patchClass, patchMethod)
                               ?? throw new MissingMethodException(patchClass.Name, patchMethod);
            // Harmony passes the original's arguments to patch parameters by name, so a renamed parameter in a game
            // update would make patching throw. Special parameters (__instance, __result, ...) start with "__".
            // A transpiler's parameters are Harmony's own (instructions, generator, original), not the original's.
            ParameterInfo[] originalParameters = original.GetParameters();
            foreach (ParameterInfo parameter in kind == HarmonyPatchType.Transpiler ? new ParameterInfo[0] : patch.GetParameters())
            {
                if (!parameter.Name.StartsWith("__", StringComparison.Ordinal)
                    && Array.TrueForAll(originalParameters, p => p.Name != parameter.Name))
                {
                    _missing.Add($"parameter '{parameter.Name}' of {type.Name}.{method}");
                }
            }
            _patches.Add((original, patch, kind));
            return this;
        }

        public bool Apply(Harmony harmony)
        {
            if (_missing.Count > 0)
            {
                Plugin.Log.LogError($"{_name}: disabled, game version {Version.GetVersionString()} is missing: " +
                                    string.Join("; ", _missing));
                return false;
            }

            var applied = new List<(MethodBase Original, MethodInfo Patch)>();
            try
            {
                foreach ((MethodBase original, MethodInfo patch, HarmonyPatchType kind) in _patches)
                {
                    var hm = new HarmonyMethod(patch);
                    harmony.Patch(original,
                        prefix: kind == HarmonyPatchType.Prefix ? hm : null,
                        postfix: kind == HarmonyPatchType.Postfix ? hm : null,
                        finalizer: kind == HarmonyPatchType.Finalizer ? hm : null,
                        transpiler: kind == HarmonyPatchType.Transpiler ? hm : null,
                        ilmanipulator: null);
                    applied.Add((original, patch));
                }
            }
            catch (Exception e)
            {
                foreach ((MethodBase original, MethodInfo patch) in applied)
                {
                    harmony.Unpatch(original, patch);
                }
                Plugin.Log.LogError($"{_name}: disabled, patching failed on game version {Version.GetVersionString()}.\n{e}");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Error reporting for patch bodies. Patches keep their logic in a separate method called from inside a
    /// try block, so even a member missing at JIT time surfaces here rather than in the game's own code.
    /// </summary>
    public static class Guard
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();

        public static void Report(string where, Exception e)
        {
            if (Reported.Add(where))
            {
                Plugin.Log.LogError($"{where} failed (further errors from it are suppressed), " +
                                    $"game version {Version.GetVersionString()}.\n{e}");
            }
        }
    }
}
