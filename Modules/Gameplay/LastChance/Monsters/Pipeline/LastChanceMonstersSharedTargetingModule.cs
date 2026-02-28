#nullable enable

using System.Collections.Generic;
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
            AddMethod(methods, typeof(EnemyElsa), nameof(EnemyElsa.StateStunSmall));
            AddMethod(methods, typeof(EnemyElsa), nameof(EnemyElsa.OnHurt));
            AddMethod(methods, typeof(EnemyHeadGrabber), nameof(EnemyHeadGrabber.StateBackToNavmesh));
            AddMethod(methods, typeof(EnemyOogly), nameof(EnemyOogly.FindLevelPointAndCreateCeilingRoamPoints));
            AddMethod(methods, typeof(EnemyOogly), nameof(EnemyOogly.OnInvestigate));
            AddMethod(methods, typeof(EnemyOogly), nameof(EnemyOogly.StateCeilingRoam));
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

        private static void AddMethod(List<System.Reflection.MethodBase> methods, System.Type declaringType, string methodName, params System.Type[] argumentTypes)
        {
            var method = argumentTypes.Length == 0
                ? AccessTools.DeclaredMethod(declaringType, methodName)
                : AccessTools.DeclaredMethod(declaringType, methodName, argumentTypes);
            if (method != null)
            {
                methods.Add(method);
            }
        }

        private static List<PlayerAvatar> GetAllPlayersWithinRangeLastChanceAware(float range, Vector3 position, bool doRaycastCheck, LayerMask layerMask)
        {
            var list = SemiFunc.PlayerGetAllPlayerAvatarWithinRange(range, position, doRaycastCheck, layerMask) ?? new List<PlayerAvatar>();
            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return list;
            }

            var players = GameDirector.instance?.PlayerList;
            if (players == null || players.Count == 0)
            {
                return list;
            }

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || list.Contains(player))
                {
                    continue;
                }

                if (!LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
                {
                    continue;
                }

                var dist = Vector3.Distance(position, headCenter);
                if (dist > range)
                {
                    continue;
                }

                if (doRaycastCheck && IsWallBlocking(position, headCenter, dist, layerMask))
                {
                    continue;
                }

                list.Add(player);
            }

            return list;
        }

        private static PlayerAvatar? GetNearestPlayerWithinRangeLastChanceAware(float range, Vector3 position, bool doRaycastCheck, LayerMask layerMask)
        {
            var list = GetAllPlayersWithinRangeLastChanceAware(range, position, doRaycastCheck, layerMask);
            if (list.Count == 0)
            {
                return null;
            }

            var bestDistance = range;
            PlayerAvatar? bestPlayer = null;
            for (var i = 0; i < list.Count; i++)
            {
                var player = list[i];
                if (player == null)
                {
                    continue;
                }

                var point = ResolveDistancePoint(player);
                var dist = Vector3.Distance(position, point);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestPlayer = player;
                }
            }

            return bestPlayer;
        }

        private static bool IsWallBlocking(Vector3 origin, Vector3 target, float distance, LayerMask layerMask)
        {
            var direction = target - origin;
            var hits = Physics.RaycastAll(origin, direction, distance, layerMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                var hitTransform = hits[i].collider?.transform;
                if (hitTransform != null && hitTransform.CompareTag("Wall"))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 ResolveDistancePoint(PlayerAvatar player)
        {
            if (LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                return headCenter;
            }

            var vision = player.PlayerVisionTarget?.VisionTransform;
            if (vision != null)
            {
                return vision.position;
            }

            return player.transform.position;
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
            methods = DeduplicateTargets(methods);
            return methods;
        }

        private static List<System.Reflection.MethodBase> DeduplicateTargets(List<System.Reflection.MethodBase> methods)
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

        private static void AddDestinationAndMovementTargets(List<System.Reflection.MethodBase> methods)
        {
            // Keep this module focused on shared state-machine entry points to reduce patch surface.
            AddMethod(methods, typeof(EnemyStateSneak), nameof(EnemyStateSneak.Update));
        }

        private static void AddRotationAndLookTargets(List<System.Reflection.MethodBase> methods)
        {
            // Intentionally empty after patch-surface reduction pass.
        }

        private static void AddAttackAndAbilityTargets(List<System.Reflection.MethodBase> methods)
        {
            AddMethod(methods, typeof(EnemyStateChaseBegin), nameof(EnemyStateChaseBegin.Update));
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

        private static void AddMethod(List<System.Reflection.MethodBase> methods, System.Type declaringType, string methodName, params System.Type[] argumentTypes)
        {
            var method = argumentTypes.Length == 0
                ? AccessTools.DeclaredMethod(declaringType, methodName)
                : AccessTools.DeclaredMethod(declaringType, methodName, argumentTypes);
            if (method != null)
            {
                methods.Add(method);
            }
        }

        private static Vector3 GetEffectiveTransformPosition(Transform transform)
        {
            return LastChanceMonstersTargetProxyHelper.ResolveEffectiveTransformTargetPosition(transform);
        }

    }
}
