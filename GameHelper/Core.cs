// <copyright file="Core.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Diagnostics;
    using Coroutine;
    using CoroutineEvents;
    using GameHelper.Cache;
    using ImGuiNET;
    using RemoteObjects;
    using Settings;
    using Utils;
    using System.Threading;
    using System.Threading.Tasks;
    using GameHelper.Controller;
    using SharpDX.DirectInput;

    /// <summary>
    ///     Main Class that depends on the GameProcess Events
    ///     and updates the RemoteObjects. It also manages the
    ///     GameHelper settings.
    /// </summary>
    public static class Core
    {
        private static string version;
        
        /// <summary>
        ///     Gets the current status of the Controller Service for display on the UI.
        /// </summary>
        public static string ControllerStatus { get; internal set; } = "Disabled";
        
        /// <summary>
        ///     Gets the Virtual Controller Manager instance.
        ///     This is null if the controller mode is disabled in settings.
        /// </summary>
        public static VirtualControllerManager VController { get; private set; }
        
        /// <summary>
        ///     Token to cancel the controller mirroring background task.
        /// </summary>
        private static CancellationTokenSource controllerMirrorToken;
        
        public static GameOverlay Overlay { get; internal set; } = null;
        public static List<ActiveCoroutine> CoroutinesRegistrar { get; } = new();
        public static GameStates States { get; } = new(IntPtr.Zero);
        public static LoadedFiles CurrentAreaLoadedFiles { get; } = new(IntPtr.Zero);
        public static GameProcess Process { get; } = new();
        public static State GHSettings { get; } = JsonHelper.CreateOrLoadJsonFile<State>(State.CoreSettingFile);
        internal static GgpkAddresses<string> GgpkStringCache {get;} = new();
        internal static GgpkAddresses<object> GgpkObjectCache { get;} = new();
        internal static AreaChangeCounter AreaChangeCounter { get; } = new(IntPtr.Zero);
        internal static GameWindowScale GameScale { get; } = new();
        internal static GameWindowCull GameCull { get; } = new(IntPtr.Zero);
        internal static TerrainHeightHelper RotationSelector { get; } = new(IntPtr.Zero, 9);
        internal static TerrainHeightHelper RotatorHelper { get; } = new(IntPtr.Zero, 25);

        public static void Initialize()
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
                var versionStr = versionInfo.FileVersion;
                if (string.IsNullOrEmpty(versionStr) || versionStr == "1.0.0.0")
                {
                    version = "Dev";
                }
                else
                {
                    var parts = versionStr.Split('.');
                    version = $"v{parts[0]}.{parts[1]}.{parts[2]}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read GameHelper version: {ex.Message}.");
                version = "Dev";
            }
            
            if (GHSettings.EnableControllerMode)
            {
                try
                {
                    ControllerStatus = "Initializing...";
                    VController = new VirtualControllerManager();
                    StartControllerMirroring();
                }
                catch (Exception ex)
                {
                    ControllerStatus = $"ERROR: Failed to initialize VCM: {ex.Message}";
                    VController = null;
                }
            }
        }
        
        public static string GetVersion()
        {
            return version.Trim();
        }

        internal static void InitializeCororutines()
        {
            CoroutineHandler.Start(GameClosedActions());
            CoroutineHandler.Start(UpdateStatesData(), priority: int.MaxValue - 3);
            CoroutineHandler.Start(UpdateFilesData(), priority: int.MaxValue - 2);
            CoroutineHandler.Start(UpdateAreaChangeData(), priority: int.MaxValue - 1);
            CoroutineHandler.Start(UpdateCullData(), priority: int.MaxValue);
            CoroutineHandler.Start(UpdateRotationSelectorData(), priority: int.MaxValue);
            CoroutineHandler.Start(UpdateRotatorHelperData(), priority: int.MaxValue);
        }

        internal static void Dispose()
        {
            controllerMirrorToken?.Cancel();
            VController?.Dispose();
            Process.Close(false);
        }

        internal static void RemoteObjectsToImGuiCollapsingHeader()
        {
            const BindingFlags propertyFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
            foreach (var property in RemoteObjectBase.GetToImGuiMethods(typeof(Core), propertyFlags, null))
            {
                if (ImGui.CollapsingHeader(property.Name))
                {
                    property.ToImGui.Invoke(property.Value, null);
                }
            }
        }
        
        internal static void CacheImGui()
        {
            if (ImGui.CollapsingHeader("GGPK String Data Cache"))
            {
                GgpkStringCache.ToImGui();
            }

            if (ImGui.CollapsingHeader("GGPK Object Cache"))
            {
                GgpkObjectCache.ToImGui();
            }
        }
        
        private static void StartControllerMirroring()
        {
            controllerMirrorToken = new CancellationTokenSource();
            Task.Run(async () =>
            {
                var directInput = new DirectInput();
                var joystickGuid = Guid.Empty;
                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices)) { joystickGuid = deviceInstance.InstanceGuid; break; }
                if (joystickGuid == Guid.Empty)
                {
                    foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices)) { joystickGuid = deviceInstance.InstanceGuid; break; }
                }
                if (joystickGuid == Guid.Empty) { ControllerStatus = "ERROR: No physical controller found."; return; }

                using var joystick = new Joystick(directInput, joystickGuid);
                ControllerStatus = $"OK: Mirroring '{joystick.Information.InstanceName}'";
                joystick.Properties.BufferSize = 128;
                joystick.Acquire();
                while (!controllerMirrorToken.IsCancellationRequested)
                {
                    try
                    {
                        joystick.Poll();
                        var state = joystick.GetCurrentState();
                        VController?.Update(state);
                    }
                    catch (SharpDX.SharpDXException ex)
                    {
                        if (ex.ResultCode == SharpDX.DirectInput.ResultCode.InputLost || ex.ResultCode == SharpDX.DirectInput.ResultCode.NotAcquired)
                        {
                            try { joystick.Acquire(); } catch { await Task.Delay(1000); }
                        }
                        else
                        {
                            ControllerStatus = $"ERROR: Unrecoverable mirroring error: {ex.Message}";
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        ControllerStatus = $"ERROR: General mirroring error: {ex.Message}";
                        break;
                    }

                    await Task.Delay(16);
                }
                joystick.Unacquire();
            }, controllerMirrorToken.Token);
        }
        
        private static IEnumerator<Wait> UpdateCullData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                GameCull.Address = Process.StaticAddresses["GameCullSize"];
            }
        }

        private static IEnumerator<Wait> UpdateAreaChangeData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                AreaChangeCounter.Address = Process.StaticAddresses["AreaChangeCounter"];
            }
        }

        private static IEnumerator<Wait> UpdateFilesData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                CurrentAreaLoadedFiles.Address = Process.StaticAddresses["File Root"];
            }
        }

        private static IEnumerator<Wait> UpdateStatesData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                States.Address = Process.StaticAddresses["Game States"];
            }
        }

        private static IEnumerator<Wait> UpdateRotationSelectorData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                RotationSelector.Address = Process.StaticAddresses["Terrain Rotation Selector"];
            }
        }

        private static IEnumerator<Wait> UpdateRotatorHelperData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                RotatorHelper.Address = Process.StaticAddresses["Terrain Rotator Helper"];
            }
        }

        private static IEnumerator<Wait> GameClosedActions()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.OnClose);
                States.Address = IntPtr.Zero;
                CurrentAreaLoadedFiles.Address = IntPtr.Zero;
                AreaChangeCounter.Address = IntPtr.Zero;
                GameCull.Address = IntPtr.Zero;
                RotationSelector.Address = IntPtr.Zero;
                RotatorHelper.Address = IntPtr.Zero;

                if (GHSettings.CloseWhenGameExit)
                {
                    Overlay?.Close();
                }
            }
        }
    }
}