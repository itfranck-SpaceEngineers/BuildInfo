﻿using System;
using Digi.ComponentLib;
using Sandbox.Game;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Systems
{
    public enum HudState
    {
        OFF = 0,
        HINTS = 1,
        BASIC = 2
    }

    public class GameConfig : ModComponent
    {
        public delegate void EventHandlerHudStateChanged(HudState prevState, HudState state);
        public event EventHandlerHudStateChanged HudStateChanged;

        public event Action OptionsMenuClosed;

        public HudState HudState;
        public float HudBackgroundOpacity;
        public double AspectRatio;
        public bool RotationHints;

        public GameConfig(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
        }

        protected override void RegisterComponent()
        {
            MyAPIGateway.Gui.GuiControlRemoved += GuiControlRemoved;

            UpdateConfigValues();
        }

        protected override void UnregisterComponent()
        {
            MyAPIGateway.Gui.GuiControlRemoved -= GuiControlRemoved;
        }

        protected override void UpdateAfterSim(int tick)
        {
            // required in simulation update because it gets the previous value if used in HandleInput()
            if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOGGLE_HUD))
            {
                UpdateHudState();
            }
        }

        private void GuiControlRemoved(object obj)
        {
            try
            {
                if(obj.ToString().EndsWith("ScreenOptionsSpace")) // closing options menu just assumes you changed something so it'll re-check config settings
                {
                    UpdateConfigValues();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void UpdateConfigValues()
        {
            UpdateHudState();

            HudBackgroundOpacity = MyAPIGateway.Session.Config?.HUDBkOpacity ?? 0.6f;

            RotationHints = MyAPIGateway.Session.Config?.RotationHints ?? true;

            var viewportSize = MyAPIGateway.Session.Camera.ViewportSize;
            AspectRatio = (double)viewportSize.X / (double)viewportSize.Y;

            OptionsMenuClosed?.Invoke();
        }

        private void UpdateHudState()
        {
            var prevState = HudState;

            HudState = (HudState)(MyAPIGateway.Session.Config?.HudState ?? (int)HudState.HINTS);

            HudStateChanged?.Invoke(prevState, HudState);
        }
    }
}
