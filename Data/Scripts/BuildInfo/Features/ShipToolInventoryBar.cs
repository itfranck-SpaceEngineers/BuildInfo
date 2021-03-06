﻿using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features
{
    public class ShipToolInventoryBar : ModComponent
    {
        public bool Shown = false;

        private bool show = false;
        private float filledRatio = 0f;
        private int skippedTicks = SKIP_TICKS;
        private const int SKIP_TICKS = 10; // how many ticks to skip to run one fill ratio update
        private readonly List<IMyShipDrill> drills = new List<IMyShipDrill>();
        //private readonly List<IMyShipWelder> welders = new List<IMyShipWelder>();
        private readonly List<IMyShipGrinder> grinders = new List<IMyShipGrinder>();

        private const BlendTypeEnum BLEND_TYPE = BlendTypeEnum.PostPP;
        private readonly MyStringId BAR_BG_MATERIAL = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryBarBg");
        private readonly MyStringId BAR_MATERIAL = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryBarProgress");
        private readonly Vector2 BAR_HUDPOS = new Vector2(0.5f, 0.835f);
        private readonly Vector2 BAR_SIZE = new Vector2(32f, 2.8f) * 0.001f;
        private readonly Vector4 BAR_BG_COLOR = new Color(123, 162, 186).ToVector4();
        private readonly Vector4 BAR_COLOR = new Color(136, 218, 240).ToVector4();
        private readonly Vector4 BAR_WARN_COLOR = new Vector4(1f, 0.75f, 0f, 1f);
        private const float BAR_WARNING_ABOVE = 0.7f;

        public ShipToolInventoryBar(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_DRAW;
        }

        protected override void RegisterComponent()
        {
            GameConfig.HudStateChanged += GameConfig_HudStateChanged;
            EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
        }

        protected override void UnregisterComponent()
        {
            GameConfig.HudStateChanged -= GameConfig_HudStateChanged;
            EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
        }

        private void GameConfig_HudStateChanged(HudState prevState, HudState newState)
        {
            UpdateShow();
        }

        private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            UpdateShow();
        }

        private void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(shipController != null && show)
            {
                if(++skippedTicks > SKIP_TICKS)
                {
                    skippedTicks = 0;

                    if(TextAPIEnabled && Main.Config.ShipToolInventoryBar.Value && !MyAPIGateway.Gui.IsCursorVisible)
                        ComputeFillRatio();
                }
            }
        }

        private void UpdateShow()
        {
            show = (GameConfig.HudState != HudState.OFF && (EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder) || EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_Drill)));

            //show = (EquipmentMonitor.IsAnyTool
            //    && !EquipmentMonitor.IsCubeBuilder
            //    && EquipmentMonitor.HandEntity == null
            //    && GameConfig.HudState != HudState.OFF);
        }

        private void ComputeFillRatio()
        {
            filledRatio = 0;

            var shipController = MyAPIGateway.Session.ControlledObject as IMyShipController;

            if(shipController == null)
                return;

            if(EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_Drill))
            {
                filledRatio = GetFilledRatio(shipController.CubeGrid, drills);
            }
            else if(EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder))
            {
                filledRatio = GetFilledRatio(shipController.CubeGrid, grinders);
            }
            //else if(EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_ShipWelder))
            //{
            //    FindFilledRatio(shipController, welders);
            //}
        }

        private static float GetFilledRatio<T>(IMyCubeGrid grid, List<T> blocks) where T : class, IMyTerminalBlock
        {
            float volume = 0;
            float maxVolume = 0;

            // ship tool toolbar actuates tools beyond rotors/pistons and connectors!
            var GTS = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
            GTS.GetBlocksOfType(blocks);

            foreach(T block in blocks)
            {
                var inv = block.GetInventory(0);

                if(inv == null)
                    continue;

                volume += (float)inv.CurrentVolume;
                maxVolume += (float)inv.MaxVolume;
            }

            blocks.Clear();

            return (maxVolume > 0 ? MathHelper.Clamp(volume / maxVolume, 0, 1) : 0);
        }

        protected override void UpdateDraw()
        {
            Shown = false;

            if(!show || !TextAPIEnabled || !Main.Config.ShipToolInventoryBar.Value || MyAPIGateway.Gui.IsCursorVisible)
                return;

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var worldPos = DrawUtils.HUDtoWorld(BAR_HUDPOS);
            float w = BAR_SIZE.X * DrawUtils.ScaleFOV;
            float h = BAR_SIZE.Y * DrawUtils.ScaleFOV;

            MyTransparentGeometry.AddBillboardOriented(BAR_BG_MATERIAL, Color.White, worldPos, camMatrix.Left, camMatrix.Up, w, h, Vector2.Zero, blendType: BLEND_TYPE);

            if(filledRatio > 0)
            {
                var color = Color.White;

                if(filledRatio > BAR_WARNING_ABOVE)
                    color = Vector4.Lerp(color, BAR_WARN_COLOR, ((filledRatio - BAR_WARNING_ABOVE) / (1 - BAR_WARNING_ABOVE)));

                const float MIN = 0.06f;
                const float MAX = 0.98f;
                var barFill = MIN + ((MAX - MIN) * filledRatio);
                var uv = new Vector2(-(1 - barFill), 0);
                worldPos += camMatrix.Left * ((1 - barFill) * BAR_SIZE.X * 2 * DrawUtils.ScaleFOV);

                MyTransparentGeometry.AddBillboardOriented(BAR_MATERIAL, color, worldPos, camMatrix.Left, camMatrix.Up, w, h, uv, blendType: BLEND_TYPE);
            }

            Shown = true;
        }
    }
}
