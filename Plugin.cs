#nullable enable

using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using DHHFLastChanceMode.Modules.Config;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Guards;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Monsters;
using DHHFLastChanceMode.Modules.Gameplay.LastChance.Runtime;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DHHFLastChanceMode
{
    [BepInDependency(CorePluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string CorePluginGuid = "AdrenSnyder.DeathHeadHopperFix";
        private const string PluginGuid = "AdrenSnyder.DHHFLastChanceMode";
        private const string PluginName = "DHHF LastChance Mode";
        private const string PluginVersion = "0.1.0";
        private const string TargetAssemblyName = "DeathHeadHopper";

        private Harmony? _harmony;
        private Assembly? _targetAssembly;
        private bool _runtimeInitialized;
        private Coroutine? _deferredBootstrapRoutine;
        private static ManualLogSource? s_log;

        private void Awake()
        {
            s_log = Logger;
            _harmony = new Harmony(PluginGuid);
            _deferredBootstrapRoutine = StartCoroutine(DeferredBootstrap());
        }

        private void OnDestroy()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ConfigManager.HostControlledChanged -= OnHostControlledChanged;

            if (_deferredBootstrapRoutine != null)
            {
                StopCoroutine(_deferredBootstrapRoutine);
                _deferredBootstrapRoutine = null;
            }
        }

        private IEnumerator DeferredBootstrap()
        {
            while (!_runtimeInitialized)
            {
                if (IsCoreLoaded())
                {
                    InitializeRuntime();
                    yield break;
                }

                yield return new WaitForSeconds(1f);
            }
        }

        private bool IsCoreLoaded()
        {
            if (Chainloader.PluginInfos.ContainsKey(CorePluginGuid))
            {
                return true;
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var asmName = asm.GetName().Name;
                if (string.Equals(asmName, "DeathHeadHopperFix", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void InitializeRuntime()
        {
            if (_runtimeInitialized)
            {
                return;
            }

            _runtimeInitialized = true;
            _deferredBootstrapRoutine = null;

            s_log?.LogInfo("[LastChance][CompatGate][Trace] InitializeRuntime begin.");
            ConfigManager.Initialize(Config);
            s_log?.LogInfo("[LastChance][CompatGate][Trace] Config initialized.");
            AllPlayersDeadGuard.EnsureEnabled();
            if (FeatureFlags.LastChangeMode)
            {
                LastChanceTimerController.PrewarmGlobalAssetsAtBoot();
            }

            var harmony = _harmony;
            if (harmony == null)
            {
                return;
            }

            harmony.PatchAll(typeof(Plugin).Assembly);

            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ConfigManager.HostControlledChanged += OnHostControlledChanged;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                TryPatchIfTargetAssembly(asm);
            }
        }

        private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            TryPatchIfTargetAssembly(args.LoadedAssembly);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            s_log?.LogInfo($"[LastChance][CompatGate][Trace] OnSceneLoaded name='{scene.name}' mode={mode}.");
            CompatibilityGate.EnsureCreated();
            s_log?.LogInfo("[LastChance][CompatGate][Trace] OnSceneLoaded ensured CompatibilityGate.");

            var shouldHandleRuntimeScene = ShouldHandleRuntimeScene();
            LastChanceTimerController.OnLevelLoaded(shouldHandleRuntimeScene);
            if (!shouldHandleRuntimeScene)
            {
                s_log?.LogInfo("[LastChance][CompatGate][Trace] OnSceneLoaded runtime handling skipped by ShouldHandleRuntimeScene.");
                return;
            }

            ConfigSyncManager.EnsureCreated();
            ConfigSyncManager.RequestHostSnapshotBroadcast();
            ReconcileConditionalMonsterPatches();
        }

        private void OnHostControlledChanged()
        {
            LastChanceTimerController.OnHostControlledConfigChanged();
            if (FeatureFlags.LastChangeMode)
            {
                LastChanceTimerController.PrewarmGlobalAssetsAtBoot();
            }
            ReconcileConditionalMonsterPatches();
        }

        private static bool ShouldHandleRuntimeScene()
        {
            if (RunManager.instance == null)
            {
                return false;
            }

            if (SemiFunc.RunIsLobbyMenu() ||
                SemiFunc.RunIsLobby() ||
                SemiFunc.RunIsShop() ||
                SemiFunc.RunIsArena() ||
                SemiFunc.RunIsTutorial() ||
                SemiFunc.MenuLevel())
            {
                return false;
            }

            return true;
        }

        private void TryPatchIfTargetAssembly(Assembly asm)
        {
            if (asm == null || _targetAssembly != null)
            {
                return;
            }

            var name = asm.GetName().Name;
            if (!string.Equals(name, TargetAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _targetAssembly = asm;
            ReconcileConditionalMonsterPatches();
            s_log?.LogInfo($"Detected {TargetAssemblyName} assembly load for LastChance patches.");
        }

        private void ReconcileConditionalMonsterPatches()
        {
            var harmony = _harmony;
            var asm = _targetAssembly;
            if (harmony == null || asm == null)
            {
                return;
            }

            var enableMonsterPipelinePatches =
                FeatureFlags.LastChangeMode &&
                FeatureFlags.LastChanceMonstersSearchEnabled;

            LastChanceMonstersPatchLifecycle.ReconcilePipeline(enableMonsterPipelinePatches, harmony, asm);
        }
    }
}
