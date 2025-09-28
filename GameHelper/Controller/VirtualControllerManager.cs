#nullable enable

namespace GameHelper.Controller
{
    using Nefarius.ViGEm.Client;
    using Nefarius.ViGEm.Client.Targets;
    using Nefarius.ViGEm.Client.Targets.Xbox360;
    using SharpDX.DirectInput;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public class VirtualControllerManager : IDisposable
    {
        private readonly ViGEmClient? client;
        private readonly IXbox360Controller? virtualController;
        private bool isDisposed = false;
        private bool isInjecting = false;

        private static readonly List<(int ps3ButtonIndex, Xbox360Button x360Button)> ButtonMap = new()
        {
            (0, Xbox360Button.A), (1, Xbox360Button.B), (2, Xbox360Button.X), (3, Xbox360Button.Y),
            (4, Xbox360Button.LeftShoulder), (5, Xbox360Button.RightShoulder),
            (6, Xbox360Button.Back), (7, Xbox360Button.Start),
            (8, Xbox360Button.LeftThumb), (9, Xbox360Button.RightThumb),
        };

        public VirtualControllerManager()
        {
            try
            {
                this.client = new ViGEmClient();
                this.virtualController = this.client.CreateXbox360Controller();
                this.virtualController.Connect();
            }
            catch (Exception ex) 
            {
                Core.ControllerStatus = $"ERROR: VCM Initialization failed: {ex.Message}";
            }
        }

        public void Update(JoystickState physicalState)
        {
            if (isDisposed || this.virtualController == null || this.isInjecting) return;
            try
            {
                virtualController.SetAxisValue(Xbox360Axis.LeftThumbX, (short)(physicalState.X - 32768));
                virtualController.SetAxisValue(Xbox360Axis.LeftThumbY, (short)Math.Clamp((long)(physicalState.Y - 32768) * -1, short.MinValue, short.MaxValue));
                virtualController.SetAxisValue(Xbox360Axis.RightThumbX, (short)(physicalState.RotationX - 32768));
                virtualController.SetAxisValue(Xbox360Axis.RightThumbY, (short)Math.Clamp((long)(physicalState.RotationY - 32768) * -1, short.MinValue, short.MaxValue));
                virtualController.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(physicalState.Z / 256));
                virtualController.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(physicalState.RotationZ / 256));
                foreach (var (ps3ButtonIndex, x360Button) in ButtonMap)
                {
                    if (ps3ButtonIndex < physicalState.Buttons.Length)
                    {
                        this.virtualController.SetButtonState(x360Button, physicalState.Buttons[ps3ButtonIndex]);
                    }
                }

                if (physicalState.PointOfViewControllers.Length > 0)
                {
                    int pov = physicalState.PointOfViewControllers[0];
                    this.virtualController.SetButtonState(Xbox360Button.Up, (pov >= 0 && pov < 4500) || (pov >= 31500 && pov < 36000));
                    this.virtualController.SetButtonState(Xbox360Button.Right, pov >= 4500 && pov < 13500);
                    this.virtualController.SetButtonState(Xbox360Button.Down, pov >= 13500 && pov < 22500);
                    this.virtualController.SetButtonState(Xbox360Button.Left, pov >= 22500 && pov < 31500);
                }

                this.virtualController.SubmitReport();
            }
            catch (Exception ex)
            {
                Core.ControllerStatus = $"UPDATE ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Simulates a button press on the virtual controller for a short duration.
        /// </summary>
        /// <param name="button">The Xbox360Button to press.</param>
        /// <param name="pressDurationMs">The duration of the press in milliseconds.</param>
        public void PressButton(Xbox360Button button, int pressDurationMs = 150)
        {
            if (isDisposed || this.virtualController == null || button == null) return;
            this.isInjecting = true;
            this.virtualController.SetButtonState(button, true);
            this.virtualController.SubmitReport();
            Thread.Sleep(pressDurationMs);
            this.virtualController.SetButtonState(button, false);
            this.virtualController.SubmitReport();
            this.isInjecting = false;
        }

        /// <summary>
        /// Disposes of the virtual controller and client resources.
        /// </summary>
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                this.virtualController?.Disconnect();
                this.client?.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }
}