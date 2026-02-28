#nullable enable

using System;
using System.Collections.Generic;
using HarmonyLib;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support
{
    internal static class LastChanceMonstersPatchTargetHelper
    {
        internal static List<System.Reflection.MethodBase> BuildTargetList(params Action<List<System.Reflection.MethodBase>>[] addSteps)
        {
            var methods = new List<System.Reflection.MethodBase>();
            if (addSteps == null)
            {
                return methods;
            }

            for (var i = 0; i < addSteps.Length; i++)
            {
                addSteps[i]?.Invoke(methods);
            }

            return Deduplicate(methods);
        }

        internal static void AddDeclaredMethod(List<System.Reflection.MethodBase> methods, Type declaringType, string methodName, params Type[] argumentTypes)
        {
            var method = argumentTypes.Length == 0
                ? AccessTools.DeclaredMethod(declaringType, methodName)
                : AccessTools.DeclaredMethod(declaringType, methodName, argumentTypes);
            if (method != null)
            {
                methods.Add(method);
            }
        }

        internal static List<System.Reflection.MethodBase> Deduplicate(List<System.Reflection.MethodBase> methods)
        {
            var unique = new List<System.Reflection.MethodBase>(methods.Count);
            var seen = new HashSet<System.Reflection.MethodBase>();
            for (var i = 0; i < methods.Count; i++)
            {
                var method = methods[i];
                if (method == null || !seen.Add(method))
                {
                    continue;
                }

                unique.Add(method);
            }

            return unique;
        }
    }
}
