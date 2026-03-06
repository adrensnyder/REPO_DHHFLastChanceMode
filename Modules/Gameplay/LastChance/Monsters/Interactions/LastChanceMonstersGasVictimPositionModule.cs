#nullable enable

using System.Collections.Generic;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Adapters;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters.Interactions
{
    [HarmonyPatch]
    internal static class LastChanceMonstersGasVictimPositionModule
    {
        [HarmonyPatch(typeof(EnemyHeartHugger), nameof(EnemyHeartHugger.PlayersInGasLogic))]
        internal static class EnemyHeartHuggerPlayersInGasLogicPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(EnemyHeartHugger __instance)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                ExecutePlayersInGasLogic(__instance);
                return false;
            }
        }

        [HarmonyPatch(typeof(EnemyHeartHugger), nameof(EnemyHeartHugger.PlayerInGas), new[] { typeof(PlayerAvatar) })]
        internal static class EnemyHeartHuggerPlayerInGasPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(EnemyHeartHugger __instance, PlayerAvatar _player)
            {
                if (!LastChanceMonstersTargetProxyHelper.IsRuntimeEnabled())
                {
                    return true;
                }

                ExecutePlayerInGas(__instance, _player);
                return false;
            }
        }

        private static void ExecutePlayersInGasLogic(EnemyHeartHugger instance)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
            {
                return;
            }

            if (instance.playersInGas.Count > 0)
            {
                var toRemove = new List<EnemyHeartHugger.PlayersInGas>();
                foreach (var playersInGas in instance.playersInGas)
                {
                    playersInGas.playerAvatar.upgradeTumbleWingsLogic.tumbleWingPinkTimer = 1f;
                    if (playersInGas.inGasTime >= 2f)
                    {
                        playersInGas.isCaught = true;
                    }

                    playersInGas.inGasTime += Time.deltaTime;
                    var currentPosition = GetEffectivePlayerPosition(playersInGas.playerAvatar);
                    var distance = Vector3.Distance(playersInGas.lastPositionInsideGas, currentPosition);
                    if (playersInGas.outsideGasTime >= 3f || distance > 2f)
                    {
                        toRemove.Add(playersInGas);
                    }

                    playersInGas.outsideGasTime += Time.deltaTime;
                }

                foreach (var playersInGas in toRemove)
                {
                    instance.playersOnCooldown.Add(playersInGas.playerAvatar, Time.time);
                    instance.playersInGas.Remove(playersInGas);
                }
            }

            if (!SemiFunc.FPSImpulse5())
            {
                return;
            }

            foreach (var playersInGas in instance.playersInGas)
            {
                var isNew = true;
                foreach (var previous in instance.playersInGasPrevious)
                {
                    if (playersInGas.playerAvatar == previous.playerAvatar)
                    {
                        isNew = false;
                    }
                }

                if (!isNew)
                {
                    continue;
                }

                if (SemiFunc.IsMultiplayer())
                {
                    instance.photonView.RPC("PlayerInGasClientRPC", RpcTarget.All, new object[]
                    {
                        playersInGas.playerAvatar.photonView.ViewID,
                        true
                    });
                }
                else
                {
                    instance.PlayerInGasClientRPC(playersInGas.playerAvatar.photonView.ViewID, true, default);
                }
            }

            foreach (var previous in instance.playersInGasPrevious)
            {
                var removed = true;
                using (var enumerator = instance.playersInGas.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current.playerAvatar == previous.playerAvatar)
                        {
                            removed = false;
                        }
                    }
                }

                if (!removed)
                {
                    continue;
                }

                if (SemiFunc.IsMultiplayer())
                {
                    instance.photonView.RPC("PlayerInGasClientRPC", RpcTarget.All, new object[]
                    {
                        previous.playerAvatar.photonView.ViewID,
                        false
                    });
                }
                else
                {
                    instance.PlayerInGasClientRPC(previous.playerAvatar.photonView.ViewID, false, default);
                }
            }

            instance.playersInGasPrevious.Clear();
            instance.playersInGasPrevious.AddRange(instance.playersInGas);
        }

        private static void ExecutePlayerInGas(EnemyHeartHugger instance, PlayerAvatar player)
        {
            foreach (var playersInGas in instance.playersInGas)
            {
                if (playersInGas.playerAvatar != player)
                {
                    continue;
                }

                playersInGas.outsideGasTime = 0f;
                playersInGas.lastPositionInsideGas = GetEffectivePlayerPosition(player);
                return;
            }

            var newPlayerInGas = new EnemyHeartHugger.PlayersInGas
            {
                playerAvatar = player,
                outsideGasTime = 0f,
                lastPositionInsideGas = GetEffectivePlayerPosition(player)
            };
            instance.playersInGas.Add(newPlayerInGas);
        }

        internal static Vector3 GetEffectivePlayerPosition(PlayerAvatar? player)
        {
            return LastChanceMonstersTargetProxyHelper.ResolveEffectivePlayerTargetPosition(player);
        }
    }
}
