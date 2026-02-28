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
            AddMethod(methods, typeof(EnemyElsa), "StateStunSmall");
            AddMethod(methods, typeof(EnemyElsa), nameof(EnemyElsa.OnHurt));
            AddMethod(methods, typeof(EnemyHeadGrabber), "StateBackToNavmesh");
            AddMethod(methods, typeof(EnemyOogly), nameof(EnemyOogly.FindLevelPointAndCreateCeilingRoamPoints));
            AddMethod(methods, typeof(EnemyOogly), nameof(EnemyOogly.OnInvestigate));
            AddMethod(methods, typeof(EnemyOogly), "StateCeilingRoam");
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
            return methods;
        }

        private static void AddDestinationAndMovementTargets(List<System.Reflection.MethodBase> methods)
        {
            AddMethod(methods, typeof(EnemyBeamer), "StateAttackStart");
            AddMethod(methods, typeof(EnemyBeamer), "StateMeleeStart");
            AddMethod(methods, typeof(EnemyBirthdayBoy), "StateGoToPlayerAngry");
            AddMethod(methods, typeof(EnemyBirthdayBoy), "PlayerOnNavMesh");
            AddMethod(methods, typeof(EnemyBombThrower), "StateGotoPlayer");
            AddMethod(methods, typeof(EnemyBombThrower), "StateBackAwayPlayer");
            AddMethod(methods, typeof(EnemyBombThrower), "StateBackAwayHead");
            AddMethod(methods, typeof(EnemyDuck), "StateChaseNavmesh");
            AddMethod(methods, typeof(EnemyElsa), "StateGoToPlayerSmall");
            AddMethod(methods, typeof(EnemyElsa), "StateLookUnderStartBig");
            AddMethod(methods, typeof(EnemyFloater), "StateNotice");
            AddMethod(methods, typeof(EnemyFloater), "StateGoToPlayer");
            AddMethod(methods, typeof(EnemyFloater), "StateSneak");
            AddMethod(methods, typeof(EnemyHeadGrabber), "GotoLogic");
            AddMethod(methods, typeof(EnemyHeadGrabber), "GotoOverLogic");
            AddMethod(methods, typeof(EnemyHidden), "StatePlayerGoTo");
            AddMethod(methods, typeof(EnemyHidden), "StatePlayerMove");
            AddMethod(methods, typeof(EnemyHidden), "StatePlayerRelease");
            AddMethod(methods, typeof(EnemyRobe), "MoveTowardPlayer");
            AddMethod(methods, typeof(EnemyRunner), "StateSneak");
            AddMethod(methods, typeof(EnemyRunner), "StateLookUnderStart");
            AddMethod(methods, typeof(EnemyShadow), "StateFollow");
            AddMethod(methods, typeof(EnemySlowWalker), "StateGoToPlayer");
            AddMethod(methods, typeof(EnemySlowWalker), "StateSneak");
            AddMethod(methods, typeof(EnemySlowWalker), "StateLookUnderStart");
            AddMethod(methods, typeof(EnemySpinny), "StateGoToPlayer");
            AddMethod(methods, typeof(EnemyStateSneak), nameof(EnemyStateSneak.Update));
            AddMethod(methods, typeof(EnemyTumbler), "StateMoveToPlayer");
            AddMethod(methods, typeof(EnemyTumbler), "StateTumble");
            AddMethod(methods, typeof(EnemyUpscream), "StateGoToPlayer");
            AddMethod(methods, typeof(EnemyValuableThrower), "StateGetValuable");
            AddMethod(methods, typeof(EnemyValuableThrower), "StateTargetPlayer");
        }

        private static void AddRotationAndLookTargets(List<System.Reflection.MethodBase> methods)
        {
            AddMethod(methods, typeof(EnemyBombThrower), "RotationStateSet", typeof(EnemyBombThrower.RotationState));
            AddMethod(methods, typeof(EnemyCeilingEye), "RotationAnimation");
            AddMethod(methods, typeof(EnemyDuck), "RotationLogic");
            AddMethod(methods, typeof(EnemyElsa), "RotationLogic");
            AddMethod(methods, typeof(EnemyFloater), "RotationLogic");
            AddMethod(methods, typeof(EnemyHeadGrabber), "RotationLogic");
            AddMethod(methods, typeof(EnemyHidden), "RotationLogic");
            AddMethod(methods, typeof(EnemyRobe), "RotationLogic");
            AddMethod(methods, typeof(EnemyRunner), "RotationLogic");
            AddMethod(methods, typeof(EnemyShadow), "UpdateHandPositionTo", typeof(Transform));
            AddMethod(methods, typeof(EnemyShadow), "DistanceFromPlayer");
            AddMethod(methods, typeof(EnemySlowWalker), "RotationLogic");
            AddMethod(methods, typeof(EnemySpinny), "LerpToFaceTargetPlayer", typeof(float));
            AddMethod(methods, typeof(EnemySpinnyAnim), nameof(EnemySpinnyAnim.Update));
            AddMethod(methods, typeof(EnemyThinManAnim), nameof(EnemyThinManAnim.Update));
            AddMethod(methods, typeof(EnemyTick), "RotationLogic");
        }

        private static void AddAttackAndAbilityTargets(List<System.Reflection.MethodBase> methods)
        {
            AddMethod(methods, typeof(EnemyBangDirector), "StateAttackPlayer");
            AddMethod(methods, typeof(EnemyBeamer), nameof(EnemyBeamer.OnVision));
            AddMethod(methods, typeof(EnemyBirthdayBoy), "StateAttack");
            AddMethod(methods, typeof(EnemyBirthdayBoy), "StateAttackUnder");
            AddMethod(methods, typeof(EnemyBirthdayBoy), "StateAttackOver");
            AddMethod(methods, typeof(EnemyBombThrower), "AttackSetLogic");
            AddMethod(methods, typeof(EnemyBombThrower), "MeleeLogic");
            AddMethod(methods, typeof(EnemyBombThrowerHead), "StateActive", typeof(bool));
            AddMethod(methods, typeof(EnemyCeilingEye), nameof(EnemyCeilingEye.Update));
            AddMethod(methods, typeof(EnemyCeilingEye), "StateHasTarget");
            AddMethod(methods, typeof(EnemyElsa), nameof(EnemyElsa.OnVision));
            AddMethod(methods, typeof(EnemyElsa), "TargetPositionLogic");
            AddMethod(methods, typeof(EnemyElsa), "AnnoyingJumpCheck");
            AddMethod(methods, typeof(EnemyGnomeDirector), "StateAttackSet");
            AddMethod(methods, typeof(EnemyGnomeDirector), "StateAttackPlayer");
            AddMethod(methods, typeof(EnemyOogly), "VisionBlocked");
            AddMethod(methods, typeof(EnemyOogly), "StatePlayerSpotted");
            AddMethod(methods, typeof(EnemyOogly), "StateDive");
            AddMethod(methods, typeof(EnemyRobe), "StateTargetPlayer");
            AddMethod(methods, typeof(EnemyRobe), "StateLookUnderStart");
            AddMethod(methods, typeof(EnemyRobe), "StateLookUnder");
            AddMethod(methods, typeof(EnemyRobe), nameof(EnemyRobe.OnVision));
            AddMethod(methods, typeof(EnemyRunner), nameof(EnemyRunner.StateAttackPlayer));
            AddMethod(methods, typeof(EnemyRunner), nameof(EnemyRunner.StateAttackPlayerOver));
            AddMethod(methods, typeof(EnemyRunner), nameof(EnemyRunner.OnVision));
            AddMethod(methods, typeof(EnemyShadow), nameof(EnemyShadow.Update));
            AddMethod(methods, typeof(EnemyShadow), "GetHandTarget", typeof(bool));
            AddMethod(methods, typeof(EnemyShadow), "PlayerTargetTell");
            AddMethod(methods, typeof(EnemySlowMouth), "TargetPositionLogic");
            AddMethod(methods, typeof(EnemySlowWalker), nameof(EnemySlowWalker.StateLookUnderAttack));
            AddMethod(methods, typeof(EnemySlowWalker), nameof(EnemySlowWalker.OnVision));
            AddMethod(methods, typeof(EnemySpinny), "CloseToPlayerTarget", typeof(float));
            AddMethod(methods, typeof(EnemyStateChaseBegin), nameof(EnemyStateChaseBegin.Update));
            AddMethod(methods, typeof(EnemyThinMan), "StateDamage");
            AddMethod(methods, typeof(EnemyThinManAnim), "NoticeSet", typeof(bool));
            AddMethod(methods, typeof(EnemyTick), "Suck");
            AddMethod(methods, typeof(EnemyUpscreamAnim), "AttackImpulse");
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
            if (transform == null)
            {
                return Vector3.zero;
            }

            if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
            {
                return transform.position;
            }

            LastChanceMonstersTargetProxyHelper.TryResolvePlayerAvatarFromTransform(transform, out var player);
            if (player != null && LastChanceMonstersTargetProxyHelper.TryGetHeadProxyTarget(player, out var headCenter))
            {
                return headCenter;
            }

            return transform.position;
        }
    }
}
