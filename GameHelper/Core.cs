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
        /// <summary>
        ///     Gets the GameHelper version.
        /// </summary>
        private static string version;

        /// <summary>
        ///     Gets the Virtual Controller Manager instance.
        ///     This is null if the controller mode is disabled in settings.
        /// </summary>
        public static VirtualControllerManager VController { get; private set; }

        /// <summary>
        ///     Token to cancel the controller mirroring background task.
        /// </summary>
        private static CancellationTokenSource controllerMirrorToken;

        /// <summary>
        ///     Gets the GameHelper Overlay.
        /// </summary>
        public static GameOverlay Overlay { get; internal set; } = null;

        /// <summary>
        ///     Gets the list of active coroutines.
        /// </summary>
        public static List<ActiveCoroutine> CoroutinesRegistrar { get; } = new();

        /// <summary>
        ///     Gets the GameStates instance. For details read class description.
        /// </summary>
        public static GameStates States { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the files loaded for the current area.
        /// </summary>
        public static LoadedFiles CurrentAreaLoadedFiles { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the GameProcess instance. For details read class description.
        /// </summary>
        public static GameProcess Process { get; } = new();

        /// <summary>
        ///     Gets the GameHelper settings.
        /// </summary>
        public static State GHSettings { get; } = JsonHelper.CreateOrLoadJsonFile<State>(State.CoreSettingFile);

        /// <summary>
        ///     Gets the cache for all the GGPK data with value type string.
        /// </summary>
        internal static GgpkAddresses<string> GgpkStringCache {get;} = new();

        /// <summary>
        ///     Gets the cache for all the GGPK data with value type object.
        ///     Please use <seealso cref="GgpkStringCache"/> if possible as it's
        ///     little bit faster than this one.
        /// </summary>
        internal static GgpkAddresses<object> GgpkObjectCache { get;} = new();

        /// <summary>
        ///     Gets the AreaChangeCounter instance. For details read class description.
        /// </summary>
        internal static AreaChangeCounter AreaChangeCounter { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the values associated with the Game Window Scale.
        /// </summary>
        internal static GameWindowScale GameScale { get; } = new();

        /// <summary>
        ///     Gets the size of the cull (black bar) area in the game window.
        /// </summary>
        internal static GameWindowCull GameCull { get; } = new(IntPtr.Zero);

        /// <summary>
        ///     Gets the values associated with the terrain rotation selector.
        /// </summary>
        internal static TerrainHeightHelper RotationSelector { get; } = new(IntPtr.Zero, 9);

        /// <summary>
        ///     Gets the values associated with the terrain rotator helper.
        /// </summary>
        internal static TerrainHeightHelper RotatorHelper { get; } = new(IntPtr.Zero, 25); // if length ever changes, update terrain  height algo.

        /// <summary>
        ///     Initializes the <see cref="Core" /> class.
        /// </summary>
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

            // Initialize the controller service if it's enabled in the global settings.
            // Assumes a setting like 'EnableControllerMode' exists in the 'State' (GHSettings) class.
            if (GHSettings.EnableControllerMode)
            {
                try
                {
                    VController = new VirtualControllerManager();
                    StartControllerMirroring();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to initialize Virtual Controller: {ex.Message}");
                    VController = null;
                }
            }
        }

        /// <summary>
        ///     Get GameHelper version.
        /// </summary>
        /// <returns>GameHelper version.</returns>
        public static string GetVersion()
        {
            return version.Trim();
        }

        /// <summary>
        ///     Initializes the <see cref="Core" /> class coroutines.
        /// </summary>
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

        /// <summary>
        ///     Cleans up all the resources taken by the application core.
        /// </summary>
        internal static void Dispose()
        {
            // Stop the controller mirroring task and dispose of the controller manager.
            controllerMirrorToken?.Cancel();
            VController?.Dispose();

            Process.Close(false);
        }

        /// <summary>
        ///     Converts the RemoteObjects to ImGui Widgets.
        /// </summary>
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

        /// <summary>
        ///     Show the cache details on ImGui.
        /// </summary>
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

        /// <summary>
        ///     Starts the background task for mirroring the physical controller to the virtual one.
        /// </summary>
        private static void StartControllerMirroring()
        {
            controllerMirrorToken = new CancellationTokenSource();
            Task.Run(async () =>
            {
                var directInput = new DirectInput();
                var joystickGuid = Guid.Empty;
                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                {
                    joystickGuid = deviceInstance.InstanceGuid;
                    break;
                }

                if (joystickGuid == Guid.Empty)
                {
                    foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                    {
                        joystickGuid = deviceInstance.InstanceGuid;
                        break;
                    }
                }
                if (joystickGuid == Guid.Empty)
                {
                    Console.WriteLine("VCM WARNING: No physical (DirectInput) controller was found.");
                    return;
                }

                using var joystick = new Joystick(directInput, joystickGuid);
                Console.WriteLine($"VCM INFO: Physical controller found: {joystick.Information.InstanceName}. Mirroring...");
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
                            Console.WriteLine($"VCM ERROR: Unrecoverable mirroring error: {ex.Message}.");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"VCM ERROR: General mirroring error: {ex.Message}.");
                        break;
                    }

                    await Task.Delay(16);
                }
                joystick.Unacquire();
            }, controllerMirrorToken.Token);
        }

        /// <summary>
        ///     Co-routine to update the address where the
        ///     Game Cull Value is loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateCullData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                GameCull.Address = Process.StaticAddresses["GameCullSize"];
            }
        }

        /// <summary>
        ///     Co-routine to update the address where AreaChange object is loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateAreaChangeData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                AreaChangeCounter.Address = Process.StaticAddresses["AreaChangeCounter"];
            }
        }

        /// <summary>
        ///     Co-routine to update the address where the Files are loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateFilesData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                CurrentAreaLoadedFiles.Address = Process.StaticAddresses["File Root"];
            }
        }

        /// <summary>
        ///     Co-routine to update the address where the Game States are loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateStatesData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                States.Address = Process.StaticAddresses["Game States"];
            }
        }

        /// <summary>
        ///     Co-routine to update the address where the Rotation Selector values are loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateRotationSelectorData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                RotationSelector.Address = Process.StaticAddresses["Terrain Rotation Selector"];
            }
        }

        /// <summary>
        ///     Co-routine to update the address where the rotator helper values are loaded in the game memory.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
        private static IEnumerator<Wait> UpdateRotatorHelperData()
        {
            while (true)
            {
                yield return new Wait(Process.OnStaticAddressFound);
                RotatorHelper.Address = Process.StaticAddresses["Terrain Rotator Helper"];
            }
        }

        /// <summary>
        ///     Co-routine to set All controllers addresses to Zero,
        ///     once the game closes.
        /// </summary>
        /// <returns>co-routine IWait.</returns>
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