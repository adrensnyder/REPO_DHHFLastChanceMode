#nullable enable

using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Support;
using HarmonyLib;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Pipeline
{
    [HarmonyPatch]
    internal static class LastChanceMonstersSharedPlayerSearchModule
    {
        private static readonly System.Reflection.MethodInfo? s_getAllVanillaMethod = AccessTools.Method(
            typeof(SemiFunc),
            nameof(SemiFunc.PlayerGetAllPlayerAvatarWithinRange),
            new[] { typeof(float), typeof(Vector3), typeof(bool), typeof(LayerMask) });

        private static readonly System.Reflection.MethodInfo? s_getNearestVanillaMethod = AccessTools.Method(
            typeof(SemiFunc),
            nameof(SemiFunc.PlayerGetNearestPlayerAvatarWithinRange),
            new[] { typeof(float), typeof(Vector3), typeof(bool), typeof(LayerMask) });

        private static readonly System.Reflection.MethodInfo? s_getAllProxyMethod =
            AccessTools.Method(typeof(LastChanceMonstersSharedPlayerSearchModule), nameof(GetAllPlayersWithinRangeLastChanceAware));

        private static readonly System.Reflection.MethodInfo? s_getNearestProxyMethod =
            AccessTools.Method(typeof(LastChanceMonstersSharedPlayerSearchModule), nameof(GetNearestPlayerWithinRangeLastChanceAware));

        [HarmonyTargetMethods]
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var methods = new List<System.Reflection.MethodBase>();
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyElsa), nameof(EnemyElsa.StateStunSmall));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyElsa), nameof(EnemyElsa.OnHurt));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyHeadGrabber), nameof(EnemyHeadGrabber.StateBackToNavmesh));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyOogly), nameof(EnemyOogly.FindLevelPointAndCreateCeilingRoamPoints));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyOogly), nameof(EnemyOogly.OnInvestigate));
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyOogly), nameof(EnemyOogly.StateCeilingRoam));
            return methods;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ReplaceSharedPlayerSearchCalls(IEnumerable<CodeInstruction> instructions)
        {
            if (s_getAllVanillaMethod == null || s_getNearestVanillaMethod == null || s_getAllProxyMethod == null || s_getNearestProxyMethod == null)
            {
                return instructions;
            }

            var list = new List<CodeInstruction>(instructions);
            for (var i = 0; i < list.Count; i++)
            {
                var ins = list[i];
                if (ins.opcode != System.Reflection.Emit.OpCodes.Call && ins.opcode != System.Reflection.Emit.OpCodes.Callvirt)
                {
                    continue;
                }

                if (ins.operand is not System.Reflection.MethodInfo called)
                {
                    continue;
                }

                if (called == s_getAllVanillaMethod)
                {
                    ins.opcode = System.Reflection.Emit.OpCodes.Call;
                    ins.operand = s_getAllProxyMethod;
                    continue;
                }

                if (called == s_getNearestVanillaMethod)
                {
                    ins.opcode = System.Reflection.Emit.OpCodes.Call;
                    ins.operand = s_getNearestProxyMethod;
                }
            }

            return list;
        }

        private static List<PlayerAvatar> GetAllPlayersWithinRangeLastChanceAware(float range, Vector3 position, bool doRaycastCheck, LayerMask layerMask)
        {
            return LastChanceMonstersTargetingOrchestrator.GetAllPlayersWithinRangeLastChanceAware(range, position, doRaycastCheck, layerMask);
        }

        private static PlayerAvatar? GetNearestPlayerWithinRangeLastChanceAware(float range, Vector3 position, bool doRaycastCheck, LayerMask layerMask)
        {
            return LastChanceMonstersTargetingOrchestrator.GetNearestPlayerWithinRangeLastChanceAware(range, position, doRaycastCheck, layerMask);
        }
    }

    [HarmonyPatch]
    internal static class LastChanceMonstersEffectiveTargetPointModule
    {
        private static readonly System.Reflection.MethodInfo? s_transformGetPositionMethod =
            AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.position));

        private static readonly System.Reflection.MethodInfo? s_effectiveTransformPositionMethod =
            AccessTools.Method(typeof(LastChanceMonstersEffectiveTargetPointModule), nameof(GetEffectiveTransformPosition));

        [HarmonyTargetMethods]
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var methods = new List<System.Reflection.MethodBase>();
            AddDestinationAndMovementTargets(methods);
            AddRotationAndLookTargets(methods);
            AddAttackAndAbilityTargets(methods);
            methods = LastChanceMonstersPatchTargetHelper.Deduplicate(methods);
            return methods;
        }

        private static void AddDestinationAndMovementTargets(List<System.Reflection.MethodBase> methods)
        {
            // Keep this module focused on shared state-machine entry points to reduce patch surface.
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyStateSneak), nameof(EnemyStateSneak.Update));
        }

        private static void AddRotationAndLookTargets(List<System.Reflection.MethodBase> methods)
        {
            // Intentionally empty after patch-surface reduction pass.
        }

        private static void AddAttackAndAbilityTargets(List<System.Reflection.MethodBase> methods)
        {
            LastChanceMonstersPatchTargetHelper.AddDeclaredMethod(methods, typeof(EnemyStateChaseBegin), nameof(EnemyStateChaseBegin.Update));
        }

        [HarmonyPrepare]
        private static bool Prepare()
        {
            foreach (var _ in TargetMethods())
            {
                return true;
            }

            return false;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ReplaceTargetPositionReads(IEnumerable<CodeInstruction> instructions)
        {
            if (s_transformGetPositionMethod == null || s_effectiveTransformPositionMethod == null)
            {
                return instructions;
            }

            var list = new List<CodeInstruction>(instructions);
            for (var i = 0; i < list.Count; i++)
            {
                var ins = list[i];
                if ((ins.opcode != System.Reflection.Emit.OpCodes.Call && ins.opcode != System.Reflection.Emit.OpCodes.Callvirt) || ins.operand is not System.Reflection.MethodInfo called)
                {
                    continue;
                }

                if (called == s_transformGetPositionMethod)
                {
                    ins.opcode = System.Reflection.Emit.OpCodes.Call;
                    ins.operand = s_effectiveTransformPositionMethod;
                }
            }

            return list;
        }

        private static Vector3 GetEffectiveTransformPosition(Transform transform)
        {
            return LastChanceMonstersTargetingOrchestrator.ResolveEffectiveTransformTargetPoint(transform);
        }

    }
}
