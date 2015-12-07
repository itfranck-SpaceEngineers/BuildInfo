﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Sandbox.Common;
using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Common.Utils;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRage.Utils;
using Digi.Utils;
using VRageRender;

namespace Digi.BuildInfo
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class BuildInfo : MySessionComponentBase
    {
        public static bool init { get; private set; }
        
        private static bool showBuildInfo = true;
        
        private short skip = 0;
        private int maxLineWidthPx = 0;
        
        private List<IMyHudNotification> hudLines;
        private HashSet<Vector3I> cubes = new HashSet<Vector3I>();
        private StringBuilder stringBuilder = new StringBuilder();
        private MyObjectBuilderType lastTypeId;
        private MyStringHash lastSubTypeId;
        
        private const string INT_FORMAT = "0";
        private const string FLOAT_FORMAT = "0.00";
        private const string FLOAT3_FORMAT = "0.000";
        private const string NUMBER_FORMAT = "N";
        
        private HashSet<string> blendingSubTypeIds = new HashSet<string>()
        {
            "LargeBlockArmorRoundedSlope",
            "LargeBlockArmorRoundedCorner",
            "LargeHeavyBlockArmorRoundedSlope",
            "LargeHeavyBlockArmorRoundedCorner",
            "LargeBlockArmorAngledSlope",
            "LargeBlockArmorAngledCorner",
            "LargeHeavyBlockArmorAngledSlope",
            "LargeHeavyBlockArmorAngledCorner",
            "LargeBlockArmorSlope2BaseSmooth",
            "LargeBlockArmorSlope2TipSmooth",
            "LargeBlockArmorCorner2BaseSmooth",
            "LargeBlockArmorCorner2TipSmooth",
            "LargeBlockArmorInvCorner2BaseSmooth",
            "LargeBlockArmorInvCorner2TipSmooth",
            "LargeHeavyBlockArmorSlope2BaseSmooth",
            "LargeHeavyBlockArmorSlope2TipSmooth",
            "LargeHeavyBlockArmorCorner2BaseSmooth",
            "LargeHeavyBlockArmorCorner2TipSmooth",
            "LargeHeavyBlockArmorInvCorner2BaseSmooth",
            "LargeHeavyBlockArmorInvCorner2TipSmooth",
            "SmallBlockArmorRoundedSlope",
            "SmallBlockArmorRoundedCorner",
            "SmallHeavyBlockArmorRoundedSlope",
            "SmallHeavyBlockArmorRoundedCorner",
            "SmallBlockArmorAngledSlope",
            "SmallBlockArmorAngledCorner",
            "SmallHeavyBlockArmorAngledSlope",
            "SmallHeavyBlockArmorAngledCorner",
            "SmallBlockArmorSlope2BaseSmooth",
            "SmallBlockArmorSlope2TipSmooth",
            "SmallBlockArmorCorner2BaseSmooth",
            "SmallBlockArmorCorner2TipSmooth",
            "SmallBlockArmorInvCorner2BaseSmooth",
            "SmallBlockArmorInvCorner2TipSmooth",
            "SmallHeavyBlockArmorSlope2BaseSmooth",
            "SmallHeavyBlockArmorSlope2TipSmooth",
            "SmallHeavyBlockArmorCorner2BaseSmooth",
            "SmallHeavyBlockArmorCorner2TipSmooth",
            "SmallHeavyBlockArmorInvCorner2BaseSmooth",
            "SmallHeavyBlockArmorInvCorner2TipSmooth",
        };
        
        private static Dictionary<char, int> charSize = new Dictionary<char, int>();
        private const int SPACE_SIZE = 8;
        
        static BuildInfo()
        {
            charSize.Clear();
            
            // generated from fonts/white_shadow/FontData.xml
            AddCharsSize(" !I`ijl ¡¨¯´¸ÌÍÎÏìíîïĨĩĪīĮįİıĵĺļľłˆˇ˘˙˚˛˜˝ІЇії‹›∙", 8);
            AddCharsSize("\"-rª­ºŀŕŗř", 10);
            AddCharsSize("#0245689CXZ¤¥ÇßĆĈĊČŹŻŽƒЁЌАБВДИЙПРСТУХЬ€", 19);
            AddCharsSize("$&GHPUVY§ÙÚÛÜÞĀĜĞĠĢĤĦŨŪŬŮŰŲОФЦЪЯжы†‡", 20);
            AddCharsSize("%ĲЫ", 24);
            AddCharsSize("'|¦ˉ‘’‚", 6);
            AddCharsSize("(),.1:;[]ft{}·ţťŧț", 9);
            AddCharsSize("*²³¹", 11);
            AddCharsSize("+<=>E^~¬±¶ÈÉÊË×÷ĒĔĖĘĚЄЏЕНЭ−", 18);
            AddCharsSize("/ĳтэє", 14);
            AddCharsSize("3FKTabdeghknopqsuy£µÝàáâãäåèéêëðñòóôõöøùúûüýþÿāăąďđēĕėęěĝğġģĥħĶķńņňŉōŏőśŝşšŢŤŦũūŭůűųŶŷŸșȚЎЗКЛбдекруцяёђћўџ", 17);
            AddCharsSize("7?Jcz¢¿çćĉċčĴźżžЃЈЧавийнопсъьѓѕќ", 16);
            AddCharsSize("@©®мшњ", 25);
            AddCharsSize("ABDNOQRSÀÁÂÃÄÅÐÑÒÓÔÕÖØĂĄĎĐŃŅŇŌŎŐŔŖŘŚŜŞŠȘЅЊЖф□", 21);
            AddCharsSize("L_vx«»ĹĻĽĿŁГгзлхчҐ–•", 15);
            AddCharsSize("MМШ", 26);
            AddCharsSize("WÆŒŴ—…‰", 31);
            AddCharsSize("\\°“”„", 12);
            AddCharsSize("mw¼ŵЮщ", 27);
            AddCharsSize("½Щ", 29);
            AddCharsSize("¾æœЉ", 28);
            AddCharsSize("ю", 23);
            AddCharsSize("ј", 7);
            AddCharsSize("љ", 22);
            AddCharsSize("ґ", 13);
            AddCharsSize("™", 30);
            AddCharsSize("", 40);
            AddCharsSize("", 41);
            AddCharsSize("", 32);
            AddCharsSize("", 34);
        }
        
        private static void AddCharsSize(string chars, int size)
        {
            for(int i = 0; i < chars.Length; i++)
            {
                charSize.Add(chars[i], size);
            }
        }
        
        private static int GetStringSize(string text)
        {
            int size = 0;
            int len;
            for(int i = 0; i < text.Length; i++)
            {
                if(charSize.TryGetValue(text[i], out len))
                {
                    size += len;
                }
                else
                {
                    //Log.Error("No character size for "+text[i]);
                }
            }
            
            return size;
        }
        
        public void Init()
        {
            Log.Info("Initialized.");
            init = true;
            
            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
        }
        
        protected override void UnloadData()
        {
            Log.Info("Mod unloaded");
            Log.Close();
            
            init = false;
            cubes = null;
            hudLines = null;
            
            MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
        }
        
        private long lastScroll = 0;
        private const int SCROLL_FROM_LINE = 2;
        private int atLine = SCROLL_FROM_LINE;
        private const int MAX_LINES = 8;
        
        public override void UpdateAfterSimulation()
        {
            try
            {
                if(!init)
                {
                    if(MyAPIGateway.Session == null)
                        return;
                    
                    Init();
                }
                
                if(showBuildInfo && MyCubeBuilder.Static != null && MyCubeBuilder.Static.HudBlockDefinition != null)
                {
                    if(++skip < 6)
                        return;
                    
                    skip = 0;
                    var def = MyCubeBuilder.Static.HudBlockDefinition;
                    long now = DateTime.UtcNow.Ticks;
                    
                    if(def.Id.SubtypeId != lastSubTypeId || def.Id.TypeId != lastTypeId)
                    {
                        lastTypeId = def.Id.TypeId;
                        lastSubTypeId = def.Id.SubtypeId;
                        
                        var defType = def.Id.TypeId.ToString();
                        bool isDoor = false;
                        
                        switch(defType)
                        {
                            case "MyObjectBuilder_AdvancedDoor":
                            case "MyObjectBuilder_AirtightDoorGeneric":
                            case "MyObjectBuilder_AirtightHangarDoor":
                            case "MyObjectBuilder_Door":
                                isDoor = true;
                                break;
                        }
                        
                        int airTightFaces = 0;
                        int totalFaces = 0;
                        bool airTight = (isDoor || IsAirTight(def, ref airTightFaces, ref totalFaces));
                        bool deformable = def.BlockTopology == MyBlockTopology.Cube;
                        int assembleTime = (int)(def.MaxIntegrity / def.IntegrityPointsPerSec);
                        bool buildModels = def.BuildProgressModels != null && def.BuildProgressModels.Length > 0;
                        bool blending = blendingSubTypeIds.Contains(def.Id.SubtypeName);
                        
                        maxLineWidthPx = 0;
                        int line = 0;
                        
                        SetText(line++, MassFormat(def.Mass)+", "+VectorFormat(def.Size)+", "+TimeFormat((int)(assembleTime / MyAPIGateway.Session.WelderSpeedMultiplier))+(def.DisassembleRatio > 1 ? ", Disassemble ratio: "+def.DisassembleRatio.ToString(FLOAT_FORMAT) : "")+(blending ? ", Blends with armor!" : "")+(buildModels ? "" : " (No construction models)"), (blending ? MyFontEnum.DarkBlue : MyFontEnum.White));
                        SetText(line++, "Integrity: "+def.MaxIntegrity+", Deformable: "+(deformable ? "Yes ("+def.DeformationRatio.ToString(FLOAT_FORMAT)+")" : "No"), (deformable ? MyFontEnum.Blue : MyFontEnum.White));
                        SetText(line++, "Air-tight: "+(airTight ? "Yes"+(isDoor ? " (when closed)" : "") : (airTightFaces == 0 ? "No" : airTightFaces+" of "+totalFaces+" grid faces")), (isDoor || airTight ? MyFontEnum.Green : (airTightFaces == 0 ? MyFontEnum.Red : MyFontEnum.DarkBlue)));
                        
                        GetExtraInfo(ref line, def, defType);
                        
                        if(!def.Context.IsBaseGame)
                            SetText(line++, "Mod: "+def.Context.ModName, MyFontEnum.Blue);
                        
                        lastScroll = now + TimeSpan.TicksPerSecond;
                        atLine = SCROLL_FROM_LINE;
                        
                        for(int l = 0; l < hudLines.Count; l++)
                        {
                            var hud = hudLines[l];
                            
                            if(l >= line)
                            {
                                hud.Text = "";
                                continue;
                            }
                            
                            int textWidthPx = GetStringSize(hud.Text);
                            
                            textWidthPx = maxLineWidthPx - textWidthPx;
                            
                            int fillchars = (int)Math.Floor((float)textWidthPx / (float)SPACE_SIZE);
                            
                            if(fillchars < 1)
                                continue;
                            
                            string fill = new String(' ', fillchars);
                            
                            hud.Text += fill;
                        }
                    }
                    
                    int lines = 0;
                    
                    foreach(var hud in hudLines)
                    {
                        if(hud.Text.Length > 0)
                            lines++;
                        
                        hud.Hide();
                    }
                    
                    if(lines > MAX_LINES)
                    {
                        int l;
                        
                        for(l = 0; l < lines; l++)
                        {
                            var hud = hudLines[l];
                            
                            if(l < SCROLL_FROM_LINE)
                            {
                                hud.ResetAliveTime();
                                hud.Show();
                            }
                        }
                        
                        int d = SCROLL_FROM_LINE;
                        l = atLine;
                        
                        while(d < MAX_LINES)
                        {
                            var hud = hudLines[l];
                            
                            if(hud.Text.Length == 0)
                                break;
                            
                            hud.ResetAliveTime();
                            hud.Show();
                            
                            if(++l >= lines)
                                l = SCROLL_FROM_LINE;
                            
                            d++;
                        }
                        
                        if(lastScroll < now)
                        {
                            if(++atLine >= lines)
                                atLine = SCROLL_FROM_LINE;
                            
                            lastScroll = now + TimeSpan.TicksPerSecond;
                        }
                    }
                    else
                    {
                        for(int l = 0; l < lines; l++)
                        {
                            var hud = hudLines[l];
                            hud.ResetAliveTime();
                            hud.Show();
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        private bool IsAirTight(MyCubeBlockDefinition def, ref int airTightFaces, ref int totalFaces)
        {
            if(def.IsAirTight)
                return true;
            
            airTightFaces = 0;
            totalFaces = 0;
            
            if(!def.IsAirTight)
            {
                cubes.Clear();
                
                foreach(var kv in def.IsCubePressurized)
                {
                    cubes.Add(kv.Key);
                }
                
                foreach(var kv in def.IsCubePressurized)
                {
                    foreach(var kv2 in kv.Value)
                    {
                        if(cubes.Contains(kv.Key + kv2.Key))
                            continue;
                        
                        if(kv2.Value)
                            airTightFaces++;
                        
                        totalFaces++;
                    }
                }
            }
            
            return (def.IsAirTight || airTightFaces == totalFaces);
        }
        
        private void GetExtraInfo(ref int line, MyCubeBlockDefinition def, string defType)
        {
            switch(defType)
            {
                case "MyObjectBuilder_CubeBlock":
                case "MyObjectBuilder_TerminalBlock":
                    return;
            }
            
            var piston = def as MyPistonBaseDefinition;
            if(piston != null)
            {
                SetText(line++, "Requires power: "+PowerFormat(piston.RequiredPowerInput)+", Sink group: "+PowerGroup(piston.ResourceSinkGroup));
                SetText(line++, "Extended length: "+DistanceFormat(piston.Maximum));
            }
            
            var motor = def as MyMotorStatorDefinition;
            if(motor != null)
            {
                SetText(line++, "Requires power: "+PowerFormat(motor.RequiredPowerInput)+", Sink group: "+PowerGroup(motor.ResourceSinkGroup));
                
                if(!(def is MyMotorSuspensionDefinition))
                {
                    SetText(line++, "Max force: "+ForceFormat(motor.MaxForceMagnitude));
                    
                    if(motor.RotorDisplacementMin < motor.RotorDisplacementMax)
                        SetText(line++, "Displacement: "+DistanceFormat(motor.RotorDisplacementMin)+" to "+DistanceFormat(motor.RotorDisplacementMax));
                }
            }
            
            var suspension = def as MyMotorSuspensionDefinition;
            if(suspension != null)
            {
                SetText(line++, "Force: "+ForceFormat(suspension.PropulsionForce)+", Steer speed: "+TorqueFormat(suspension.SteeringSpeed * 100)+", Steer angle: "+RadAngleFormat(suspension.MaxSteer));
                SetText(line++, "Height: "+DistanceFormat(suspension.MinHeight)+" to "+DistanceFormat(suspension.MaxHeight));
            }
            
            if(piston != null || motor != null)
            {
                string topPart = null;
                
                if(motor != null)
                    topPart = motor.RotorPart;
                else if(piston != null)
                    topPart = piston.TopPart;
                
                var group = MyDefinitionManager.Static.TryGetDefinitionGroup(topPart);
                
                if(group != null)
                {
                    MyCubeBlockDefinition partDef = null;
                    
                    if(def.CubeSize == MyCubeSize.Large)
                        partDef = group.Large;
                    else
                        partDef = group.Small;
                    
                    int airTightFaces = 0;
                    int totalFaces = 0;
                    bool airTight = IsAirTight(partDef, ref airTightFaces, ref totalFaces);
                    bool deformable = def.BlockTopology == MyBlockTopology.Cube;
                    bool buildModels = def.BuildProgressModels != null && def.BuildProgressModels.Length > 0;
                    
                    SetText(line++, "Subpart - "+MassFormat(partDef.Mass)+", "+VectorFormat(partDef.Size)+", "+TimeFormat((int)((def.MaxIntegrity / def.IntegrityPointsPerSec) / MyAPIGateway.Session.WelderSpeedMultiplier))+(partDef.DisassembleRatio > 1 ? ", Disassemble ratio: "+partDef.DisassembleRatio.ToString(FLOAT_FORMAT) : "")+(buildModels ? "" : " (No construction models)"));
                    SetText(line++, "             - Integrity: "+partDef.MaxIntegrity.ToString(INT_FORMAT)+(deformable ? ", Deformable ("+partDef.DeformationRatio.ToString(FLOAT_FORMAT)+")" : "")+", Air-tight: "+(airTight ? "Yes" : (airTightFaces == 0 ? "No" : airTightFaces+" of "+totalFaces+" grid faces")));
                }
                
                return;
            }
            
            var cockpit = def as MyCockpitDefinition;
            if(cockpit != null)
            {
                SetText(line++, (cockpit.IsPressurized ? "Pressurized: Yes, Oxygen capacity: "+cockpit.OxygenCapacity.ToString(NUMBER_FORMAT)+" O2" : "Pressurized: No"), (cockpit.IsPressurized ? MyFontEnum.Green : MyFontEnum.Red));
            }
            
            var rc = def as MyRemoteControlDefinition;
            if(rc != null)
            {
                SetText(line++, "Requires power: "+PowerFormat(rc.RequiredPowerInput)+", Sink group: "+PowerGroup(rc.ResourceSinkGroup));
            }
            
            var shipController = def as MyShipControllerDefinition;
            if(shipController != null)
            {
                SetText(line++, "Ship controls: "+(shipController.EnableShipControl ? "Yes" : "No"), (shipController.EnableShipControl ? MyFontEnum.Green : MyFontEnum.Red));
                return;
            }
            
            var thrust = def as MyThrustDefinition;
            if(thrust != null)
            {
                SetText(line++, "Force: "+ForceFormat(thrust.ForceMagnitude)+", Dampener factor: "+thrust.SlowdownFactor.ToString(FLOAT_FORMAT));
                SetText(line++, "Usage - Idle: "+PowerFormat(thrust.MinPowerConsumption)+", Max: "+PowerFormat(thrust.MaxPowerConsumption)+", Sink group: "+PowerGroup(thrust.ResourceSinkGroup));
                
                if(!thrust.FuelConverter.FuelId.IsNull())
                {
                    SetText(line++, "Requires fuel: "+thrust.FuelConverter.FuelId.SubtypeId+", Efficiency: "+Math.Round(thrust.FuelConverter.Efficiency * 100, 2)+"%");
                }
                
                SetText(line++, "Thrust damage scale: "+thrust.FlameDamage.ToString(FLOAT_FORMAT)+", Distance scale: "+thrust.FlameDamageLengthScale.ToString(NUMBER_FORMAT));
                SetText(line++, "Requires atmosphere: "+(thrust.NeedsAtmosphereForInfluence ? "Yes" : "No"));
                
                if(thrust.EffectivenessAtMinInfluence < 1.0f || thrust.EffectivenessAtMaxInfluence < 1.0f)
                {
                    SetText(line++, "Near planet, "+Math.Round(thrust.MinPlanetaryInfluence * 100, 0)+"% influence has "+Math.Round(thrust.EffectivenessAtMinInfluence * 100, 0)+"% eff; "+Math.Round(thrust.MaxPlanetaryInfluence * 100, 0)+"% influence has "+Math.Round(thrust.EffectivenessAtMaxInfluence * 100, 0)+"% eff");
                }
                
                if(thrust.ConsumptionFactorPerG > 0)
                    SetText(line++, "Consumption factor per g: "+thrust.ConsumptionFactorPerG);
                
                return;
            }
            
            var vent = def as MyAirVentDefinition;
            if(vent != null)
            {
                SetText(line++, "Fill rate: "+vent.VentilationCapacityPerSecond.ToString(NUMBER_FORMAT)+" O2/s");
                SetText(line++, "Usage - Idle: "+PowerFormat(vent.StandbyPowerConsumption)+", Operational: "+PowerFormat(vent.OperationalPowerConsumption)+", Sink group: "+PowerGroup(vent.ResourceSinkGroup));
                return;
            }
            
            var light = def as MyLightingBlockDefinition;
            if(light != null)
            {
                SetText(line++, "Requires power: "+PowerFormat(light.RequiredPowerInput)+", Sink group: "+PowerGroup(light.ResourceSinkGroup));
                SetText(line++, "Max radius: "+DistanceFormat(light.LightRadius.Max));
                
                if(defType == "MyObjectBuilder_InteriorLight")
                    SetText(line++, "Physical collisions: "+(light.HasPhysics ? "On" : "Off"), MyFontEnum.DarkBlue);
                
                return;
            }
            
            var oreDetector = def as MyOreDetectorDefinition;
            if(oreDetector != null)
            {
                SetText(line++, "Power sink group: "+PowerGroup(oreDetector.ResourceSinkGroup));
                SetText(line++, "Range: "+DistanceFormat(oreDetector.MaximumRange));
                return;
            }
            
            /* definition is empty
            var oxygenTank = def as MyOxygenTankDefinition;
            if(oxygenTank != null)
            {
            }
             */
            
            var gasTank = def as MyGasTankDefinition;
            if(gasTank != null)
            {
                SetText(line++, "Power sink group: "+PowerGroup(gasTank.ResourceSinkGroup));
                SetText(line++, "Stores: "+gasTank.StoredGasId.SubtypeName+", Capacity: "+gasTank.Capacity.ToString(NUMBER_FORMAT));
                return;
            }
            
            var gyro = def as MyGyroDefinition;
            if(gyro != null)
            {
                SetText(line++, "Requires power: "+PowerFormat(gyro.RequiredPowerInput)+", Sink group: "+PowerGroup(gyro.ResourceSinkGroup));
                SetText(line++, "Force: "+ForceFormat(gyro.ForceMagnitude));
                return;
            }
            
            var projector = def as MyProjectorDefinition;
            if(projector != null)
            {
                SetText(line++, "Requires power: "+PowerFormat(projector.RequiredPowerInput)+", Sink group: "+PowerGroup(projector.ResourceSinkGroup));
                return;
            }
            
            var door = def as MyDoorDefinition;
            if(door != null)
            {
                SetText(line++, "Door speed: "+DistanceFormat(door.OpeningSpeed));
                SetText(line++, "Power sink group: "+PowerGroup(door.ResourceSinkGroup));
                return;
            }
            
            var advDoor = def as MyAdvancedDoorDefinition;
            if(advDoor != null)
            {
                SetText(line++, "Usage - Idle: "+PowerFormat(advDoor.PowerConsumptionIdle)+", Moving: "+PowerFormat(advDoor.PowerConsumptionMoving));
                return;
            }
            
            var laserAntenna = def as MyLaserAntennaDefinition;
            if(laserAntenna != null)
            {
                SetText(line++, "Usage - Idle: "+PowerFormat(laserAntenna.PowerInputIdle)+", Turning: "+PowerFormat(laserAntenna.PowerInputTurning)+", Connected: "+PowerFormat(laserAntenna.PowerInputLasing));
                SetText(line++, "Range: "+DistanceFormat(laserAntenna.MaxRange)+", Line-of-sight: "+(laserAntenna.RequireLineOfSight ? "Required" : "Not required!"), (laserAntenna.RequireLineOfSight ? MyFontEnum.White : MyFontEnum.Green));
                SetText(line++, "Rotation Pitch: "+AngleFormat(laserAntenna.MinElevationDegrees)+" to "+AngleFormat(laserAntenna.MaxElevationDegrees)+", Yaw: "+AngleFormat(laserAntenna.MinAzimuthDegrees)+" to "+AngleFormat(laserAntenna.MaxAzimuthDegrees));
                return;
            }
            
            var assembler = def as MyAssemblerDefinition;
            if(assembler != null)
            {
                SetText(line++, "Assembly speed: "+assembler.AssemblySpeed.ToString(FLOAT3_FORMAT));
            }
            
            var refinery = def as MyRefineryDefinition;
            if(refinery != null)
            {
                SetText(line++, "Refine speed: "+refinery.RefineSpeed.ToString(FLOAT3_FORMAT)+", Efficiency: "+refinery.MaterialEfficiency.ToString(FLOAT3_FORMAT));
            }
            
            var oxygenGenerator = def as MyOxygenGeneratorDefinition;
            if(oxygenGenerator != null)
            {
                SetText(line++, "Ice consumption: "+oxygenGenerator.IceConsumptionPerSecond.ToString(NUMBER_FORMAT)+" kg per second");
                
                stringBuilder.Clear();
                
                foreach(var gas in oxygenGenerator.ProducedGases)
                {
                    stringBuilder.Append(gas.Id.SubtypeName).Append(" (ratio ").Append(gas.IceToGasRatio).Append("), ");
                }
                
                stringBuilder.Length -= 2;
                
                SetText(line++, "Produces: "+stringBuilder);
            }
            
            var production = def as MyProductionBlockDefinition;
            if(production != null)
            {
                SetText(line++, "Usage - Idle: "+PowerFormat(production.StandbyPowerConsumption)+", Working: "+PowerFormat(production.OperationalPowerConsumption));
                SetText(line++, "Inventory: "+((production.InventoryMaxVolume > 0 ? production.InventoryMaxVolume : production.InventorySize.Volume) * 1000).ToString(NUMBER_FORMAT)+" L");
                
                if(production.BlueprintClasses != null)
                {
                    if(production.BlueprintClasses.Count == 0)
                    {
                        SetText(line++, "Has no blueprint classes (?!)", MyFontEnum.Red);
                    }
                    else
                    {
                        stringBuilder.Clear();
                        
                        foreach(var bp in production.BlueprintClasses)
                        {
                            stringBuilder.Append(bp.DisplayNameText).Append(',');
                        }
                        
                        stringBuilder.Length -= 1;
                        
                        if(def is MyRefineryDefinition)
                            SetText(line++, "Refines: "+stringBuilder);
                        else
                            SetText(line++, "Builds: "+stringBuilder);
                    }
                }
                
                return;
            }
            
            var reactor = def as MyReactorDefinition;
            if(reactor != null)
            {
                SetText(line++, "Inventory: "+((reactor.InventoryMaxVolume > 0 ? reactor.InventoryMaxVolume : reactor.InventorySize.Volume) * 1000).ToString(NUMBER_FORMAT)+" L");
            }
            
            var battery = def as MyBatteryBlockDefinition;
            if(battery != null)
            {
                SetText(line++, "Power group: "+battery.ResourceSinkGroup.String+", Power source group: "+PowerGroup(battery.ResourceSourceGroup));
                SetText(line++, "Power capacity: "+PowerFormat(battery.MaxStoredPower)+", Input required: "+PowerFormat(battery.RequiredPowerInput)+(battery.AdaptibleInput ? " (adaptable)" : "")+", Sink group: "+PowerGroup(battery.ResourceSinkGroup));
                return;
            }
            
            var solarPanel = def as MySolarPanelDefinition;
            if(solarPanel != null)
            {
                if(!solarPanel.IsTwoSided)
                    SetText(line++, "One-sided!", MyFontEnum.Red);
            }
            
            var powerProducer = def as MyPowerProducerDefinition;
            if(powerProducer != null)
            {
                SetText(line++, "Power output: "+PowerFormat(powerProducer.MaxPowerOutput)+", Source group: "+PowerGroup(powerProducer.ResourceSourceGroup));
                return;
            }
            
            var oxygenFarm = def as MyOxygenFarmDefinition;
            if(oxygenFarm != null)
            {
                if(!oxygenFarm.IsTwoSided)
                    SetText(line++, "One-sided!", MyFontEnum.Red);
                
                SetText(line++, "Produces: "+oxygenFarm.ProducedGas.SubtypeName+", Max rate: "+oxygenFarm.MaxGasOutput.ToString(NUMBER_FORMAT)+" per second");
                SetText(line++, "Power usage: "+PowerFormat(oxygenFarm.OperationalPowerConsumption)+", Sink group: "+PowerGroup(oxygenFarm.ResourceSinkGroup));
                return;
            }
            
            var antenna = def as MyRadioAntennaDefinition;
            if(antenna != null)
            {
                SetText(line++, "Power sink group: "+PowerGroup(antenna.ResourceSinkGroup));
            }
            
            var beacon = def as MyBeaconDefinition;
            if(beacon != null)
            {
                SetText(line++, "Power sink group: "+PowerGroup(beacon.ResourceSinkGroup));
            }
            
            var timer = def as MyTimerBlockDefinition;
            if(timer != null)
            {
                SetText(line++, "Power sink group: "+PowerGroup(timer.ResourceSinkGroup));
            }
            
            var pb = def as MyProgrammableBlockDefinition;
            if(pb != null)
            {
                SetText(line++, "Power sink group: "+PowerGroup(pb.ResourceSinkGroup));
            }
            
            var sound = def as MySoundBlockDefinition;
            if(sound != null)
            {
                SetText(line++, "Power sink group: "+PowerGroup(sound.ResourceSinkGroup));
            }
            
            var sensor = def as MySensorBlockDefinition;
            if(sensor != null)
            {
                //SetText(line++, "Requires power: "+sensor.RequiredPowerInput+", Sink group: "+PowerGroup(sensor.ResourceSinkGroup)); // TODO RequiredPowerInput doesn't seem to be working?
                SetText(line++, "Power sink group: "+PowerGroup(sensor.ResourceSinkGroup)); // set on the same line with "Requires power" when that's fixed
                SetText(line++, "Range: "+DistanceFormat(sensor.MaxRange));
                return;
            }
            
            var artificialMass = def as MyVirtualMassDefinition;
            if(artificialMass != null)
            {
                SetText(line++, "Requires power: "+PowerFormat(artificialMass.RequiredPowerInput)+", Sink group: "+PowerGroup(artificialMass.ResourceSinkGroup));
                SetText(line++, "Artificial mass: "+MassFormat(artificialMass.VirtualMass));
                return;
            }
            
            var ball = def as MySpaceBallDefinition;
            if(ball != null)
            {
                SetText(line++, "Max artificial mass: "+MassFormat(ball.MaxVirtualMass));
                return;
            }
            
            var warhead = def as MyWarheadDefinition;
            if(warhead != null)
            {
                SetText(line++, "Radius: "+DistanceFormat(warhead.ExplosionRadius));
                SetText(line++, "Damage: "+warhead.WarheadExplosionDamage.ToString(NUMBER_FORMAT));
                return;
            }
            
            var button = def as MyButtonPanelDefinition;
            if(button != null)
            {
                SetText(line++, "Power sink group: "+PowerGroup(button.ResourceSinkGroup));
                return;
            }
            
            var camera = def as MyCameraBlockDefinition;
            if(camera != null)
            {
                SetText(line++, "Requires power: "+PowerFormat(camera.RequiredPowerInput)+", Sink group: "+PowerGroup(camera.ResourceSinkGroup));
                SetText(line++, "Field of view: "+RadAngleFormat(camera.MinFov)+" to "+RadAngleFormat(camera.MaxFov));
                return;
            }
            
            var cargo = def as MyCargoContainerDefinition;
            if(cargo != null)
            {
                SetText(line++, "Inventory: "+(cargo.InventorySize.Volume * 1000).ToString(NUMBER_FORMAT)+" L");
            }
            
            var poweredCargo = def as MyPoweredCargoContainerDefinition;
            if(poweredCargo != null)
            {
                SetText(line++, "Power sink group: "+PowerGroup(poweredCargo.ResourceSinkGroup));
                return;
            }
            
            var sorter = def as MyConveyorSorterDefinition;
            if(sorter != null)
            {
                SetText(line++, "Requires power: "+PowerFormat(sorter.PowerInput)+", Sink group: "+PowerGroup(sorter.ResourceSinkGroup));
                SetText(line++, "Inventory: "+(sorter.InventorySize.Volume * 1000).ToString(NUMBER_FORMAT)+" L");
                return;
            }
            
            var gravityFlat = def as MyGravityGeneratorDefinition;
            if(gravityFlat != null)
            {
                SetText(line++, "Requires power: "+PowerFormat(gravityFlat.RequiredPowerInput)+", Sink group: "+PowerGroup(gravityFlat.ResourceSinkGroup));
                return;
            }
            
            var gravitySphere = def as MyGravityGeneratorSphereDefinition;
            if(gravitySphere != null)
            {
                SetText(line++, "Base power usage: "+PowerFormat(gravitySphere.BasePowerInput)+", Consumption: "+PowerFormat(gravitySphere.ConsumptionPower)+", Sink group: "+PowerGroup(gravitySphere.ResourceSinkGroup));
                SetText(line++, "Radius: "+DistanceFormat(gravitySphere.MaxRadius));
                return;
            }
            
            var jumpDrive = def as MyJumpDriveDefinition;
            if(jumpDrive != null)
            {
                SetText(line++, "Power required: "+PowerFormat(jumpDrive.RequiredPowerInput)+", For jump: "+PowerFormat(jumpDrive.PowerNeededForJump)+", Sink group: "+PowerGroup(jumpDrive.ResourceSinkGroup));
                SetText(line++, "Max distance: "+DistanceFormat((float)jumpDrive.MaxJumpDistance));
                SetText(line++, "Max mass: "+MassFormat((float)jumpDrive.MaxJumpMass));
                return;
            }
            
            var merger = def as MyMergeBlockDefinition;
            if(merger != null)
            {
                SetText(line++, "Magnetic force: "+ForceFormat(merger.Strength));
                return;
            }
            
            var largeTurret = def as MyLargeTurretBaseDefinition;
            if(largeTurret != null)
            {
                SetText(line++, "Range: "+DistanceFormat(largeTurret.MaxRangeMeters)+", AI: "+(largeTurret.AiEnabled ? "Yes" : "No")+(largeTurret.IdleRotation ? " (With idle rotation)" : "(No idle rotation)"));
                SetText(line++, "Speed - Pitch: "+TorqueFormat(largeTurret.ElevationSpeed * 100)+", Yaw: "+TorqueFormat(largeTurret.RotationSpeed * 100));
                SetText(line++, "Rotation - Pitch: "+AngleFormat(largeTurret.MinElevationDegrees)+" to "+AngleFormat(largeTurret.MaxElevationDegrees)+", Yaw: "+AngleFormat(largeTurret.MinAzimuthDegrees)+" to "+AngleFormat(largeTurret.MaxAzimuthDegrees));
            }
            
            var weapon = def as MyWeaponBlockDefinition;
            if(weapon != null)
            {
                SetText(line++, "Power sink group: "+PowerGroup(weapon.ResourceSinkGroup));
                SetText(line++, "Inventory: "+(weapon.InventoryMaxVolume * 1000).ToString(NUMBER_FORMAT)+" L");
                
                var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);
                
                stringBuilder.Clear();
                
                for(int i = 0; i < wepDef.AmmoMagazinesId.Length; i++)
                {
                    var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[i]);
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                    var weaponData = wepDef.WeaponAmmoDatas[(int)ammo.AmmoType];
                    
                    stringBuilder.Append(mag.Id.SubtypeName).Append(" (").Append(weaponData.RateOfFire).Append(" RPM)").Append(", ");
                }
                
                stringBuilder.Length -= 2;
                
                SetText(line++, "Ammo: "+stringBuilder.ToString());
                return;
            }
        }
        
        private string PowerGroup(MyStringHash hash)
        {
            return PowerGroup(hash == null ? "" : hash.String);
        }
        
        private string PowerGroup(string group) // some ResourceSinkGroup fields are MyStringHash while others are string type :/
        {
            return (group == null || group.Length == 0 ? "(UNDEFINED)" : group);
        }
        
        private string ForceFormat(float N)
        {
            if(N > 1000000)
                return (N / 1000000).ToString(FLOAT_FORMAT)+" MN";
            
            if(N > 1000)
                return (N / 1000).ToString(FLOAT_FORMAT)+" kN";
            
            return N.ToString(FLOAT_FORMAT)+" N";
        }
        
        private string TorqueFormat(float N)
        {
            return N.ToString(NUMBER_FORMAT)+" NM";
        }
        
        private string PowerFormat(float MW)
        {
            float W = MW * 1000000f;
            
            if(W > 1000000)
                return MW.ToString(FLOAT_FORMAT)+" MW";
            if(W > 1000)
                return (W / 1000f).ToString(FLOAT_FORMAT)+" kW";
            
            return W.ToString(FLOAT_FORMAT)+" W";
        }
        
        private string DistanceFormat(float m)
        {
            if(m > 1000)
                return (m / 1000).ToString(FLOAT_FORMAT)+" km";
            
            if(m < 10)
                return m.ToString(FLOAT_FORMAT)+" m";
            
            return m.ToString(INT_FORMAT)+" m";
        }
        
        private string MassFormat(float kg)
        {
            return kg.ToString("#,###,###,###,###")+" kg";
        }
        
        private string TimeFormat(int seconds)
        {
            return String.Format("{0:00}:{1:00}", (seconds / 60), (seconds % 60));
        }
        
        private string RadAngleFormat(float radians)
        {
            return AngleFormat(MathHelper.ToDegrees(radians));
        }
        
        private string AngleFormat(float degrees)
        {
            return degrees.ToString(INT_FORMAT)+'°';
        }
        
        private string VectorFormat(Vector3 vec)
        {
            return vec.X+"x"+vec.Y+"x"+vec.Z;
        }
        
        private void SetText(int line, string text, MyFontEnum font = MyFontEnum.White, int aliveTime = 100)
        {
            if(hudLines == null)
                hudLines = new List<IMyHudNotification>();
            
            if(line >= hudLines.Count)
                hudLines.Add(MyAPIGateway.Utilities.CreateNotification(""));
            
            text = "• " + text;
            maxLineWidthPx = Math.Max(maxLineWidthPx, GetStringSize(text));
            
            hudLines[line].Font = font;
            hudLines[line].Text = text;
            hudLines[line].AliveTime = aliveTime;
            hudLines[line].Show();
        }
        
        private string cacheOres = null;
        private string cacheMods = null;
        private string cacheAmmo = null;
        private string cacheItems = null;
        private string cacheComps = null;
        private string cacheSuits = null;
        
        public void MessageEntered(string msg, ref bool send)
        {
            if(msg.StartsWith("/buildinfo", StringComparison.InvariantCultureIgnoreCase))
            {
                send = false;
                showBuildInfo = !showBuildInfo;
                MyAPIGateway.Utilities.ShowMessage(Log.MOD_NAME, "Display turned " + (showBuildInfo ? "on" : "off"));
            }
            else if(msg.StartsWith("/info", StringComparison.InvariantCultureIgnoreCase))
            {
                send = false;
                msg = msg.Substring("/info".Length).Trim().ToLower();
                
                bool on = msg.StartsWith("on");
                
                if(on || msg.StartsWith("off"))
                {
                    showBuildInfo = on;
                    MyAPIGateway.Utilities.ShowMessage(Log.MOD_NAME, "Display turned " + (showBuildInfo ? "on" : "off"));
                }
                else if(msg.StartsWith("mod"))
                {
                    if(cacheMods == null)
                    {
                        var mods = MyAPIGateway.Session.GetCheckpoint("null").Mods;
                        stringBuilder.Clear();
                        int n = 1;
                        
                        for(int i = (mods.Count - 1); i >= 0; i--)
                        {
                            var mod = mods[i];
                            stringBuilder.Append(n++).Append(". ").Append(mod.FriendlyName).Append(" (ID: ").Append(mod.PublishedFileId).Append(')').AppendLine();
                        }
                        
                        cacheMods = stringBuilder.ToString();
                        stringBuilder.Clear();
                    }
                    
                    MyAPIGateway.Utilities.ShowMissionScreen("Mods list", "", "", cacheMods, null, "Close");
                }
                else if(msg.StartsWith("ammo") || msg.StartsWith("mag"))
                {
                    if(cacheAmmo == null)
                    {
                        var defList = MyDefinitionManager.Static.GetAllDefinitions();
                        stringBuilder.Clear();
                        
                        foreach(var def in defList)
                        {
                            if(def is MyAmmoMagazineDefinition)
                            {
                                var mag = def as MyAmmoMagazineDefinition;
                                var ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                                var projectile = ammo as MyProjectileAmmoDefinition;
                                var missile = ammo as MyMissileAmmoDefinition;
                                
                                stringBuilder.Append(mag.DisplayNameText).AppendLine().AppendLine();
                                
                                if(!mag.Context.IsBaseGame)
                                    stringBuilder.Append("  Mod: ").Append(mag.Context.ModName).AppendLine();
                                
                                stringBuilder.Append("  Type: ").Append(ammo.AmmoType).AppendLine();
                                stringBuilder.Append("  Capacity:").Append(mag.Capacity).AppendLine();
                                
                                if(projectile != null)
                                {
                                    stringBuilder.Append("  Damage to organic: "+projectile.ProjectileHealthDamage);
                                    
                                    if(projectile.HeadShot)
                                        stringBuilder.Append(", headshot damage: ").Append(projectile.ProjectileHeadShotDamage);
                                    
                                    stringBuilder.AppendLine();
                                    stringBuilder.Append("  Damage to mechanic: ").Append(projectile.ProjectileMassDamage).AppendLine();
                                    
                                    /*
                                    projectile.ProjectileMassDamage; // for mechanical objects
                                    projectile.ProjectileHealthDamage; // for living beings
                                    projectile.HeadShot ? projectile.ProjectileHeadShotDamage;
                                    projectile.IsExplosive; // not sure if used
                                     */
                                }
                                else if(missile != null)
                                {
                                    stringBuilder.Append("  Explosion radius: ").Append(missile.MissileExplosionRadius).AppendLine();
                                    stringBuilder.Append("  Explosion damage: ").Append(missile.MissileExplosionDamage).AppendLine();
                                    
                                    /*
                                    missile.MissileExplosionDamage;
                                    missile.MissileExplosionRadius;
                                     */
                                }
                                
                                stringBuilder.AppendLine().AppendLine();
                            }
                            /*
                            else
                            {
                                stringBuilder.Append(def.Id.TypeId.ToString() + " / "+ def.Id.SubtypeName).AppendLine();
                            }
                             */
                        }
                        
                        cacheAmmo = stringBuilder.ToString();
                        stringBuilder.Clear();
                    }
                    
                    MyAPIGateway.Utilities.ShowMissionScreen("Ammo and magazines info", "", "", cacheAmmo, null, "Close");
                }
                else if(msg.StartsWith("comp") || msg.StartsWith("part"))
                {
                    if(cacheComps == null)
                    {
                        var defList = MyDefinitionManager.Static.GetAllDefinitions();
                        stringBuilder.Clear();
                        
                        foreach(var def in defList)
                        {
                            if(def is MyComponentDefinition)
                            {
                                var item = def as MyComponentDefinition;
                                
                                //comp.DropProbability; // ???
                                
                                stringBuilder.Append(item.DisplayNameText).AppendLine().AppendLine();
                                
                                if(!item.Context.IsBaseGame)
                                    stringBuilder.Append("  Mod: ").Append(item.Context.ModName).AppendLine();
                                
                                stringBuilder.Append("  Volume: ").Append((item.Volume * 1000).ToString(NUMBER_FORMAT)).Append(" L").AppendLine();
                                stringBuilder.Append("  Mass: ").Append(MassFormat(item.Mass)).AppendLine();
                                stringBuilder.Append("  Integrity: ").Append(item.MaxIntegrity).AppendLine();
                                stringBuilder.Append("  Deconstruction efficiency: ").Append(Math.Round(item.DeconstructionEfficiency * 100, 0)).Append('%').AppendLine();
                                stringBuilder.Append("  Drop probability*: ").Append(Math.Round(item.DropProbability * 100, 0)).Append('%').AppendLine();
                                
                                stringBuilder.AppendLine().AppendLine();
                            }
                        }
                        
                        stringBuilder.AppendLine().Append("* = Drop probabilty is the chance that you get scrap from the destroyed component and how many of this item will drop from a destroyed container.");
                        
                        cacheComps = stringBuilder.ToString();
                        stringBuilder.Clear();
                    }
                    
                    MyAPIGateway.Utilities.ShowMissionScreen("Components info", "", "", cacheComps, null, "Close");
                }
                else if(msg.StartsWith("ore") || msg.StartsWith("ingot"))
                {
                    if(cacheOres == null)
                    {
                        var defList = MyDefinitionManager.Static.GetAllDefinitions();
                        stringBuilder.Clear();
                        
                        foreach(var def in defList)
                        {
                            if(def is MyPhysicalItemDefinition)
                            {
                                var item = def as MyPhysicalItemDefinition;
                                
                                if(item.Id.TypeId == typeof(MyObjectBuilder_Ingot) || item.Id.TypeId == typeof(MyObjectBuilder_Ore))
                                {
                                    stringBuilder.Append(item.DisplayNameText).AppendLine().AppendLine();
                                    
                                    if(!item.Context.IsBaseGame)
                                        stringBuilder.Append("  Mod: ").Append(item.Context.ModName).AppendLine();
                                    
                                    stringBuilder.Append("  Volume: ").Append((item.Volume * 1000).ToString(NUMBER_FORMAT)).Append(" L").AppendLine();
                                    stringBuilder.Append("  Mass: ").Append(MassFormat(item.Mass)).AppendLine();
                                    
                                    stringBuilder.AppendLine().AppendLine();
                                }
                            }
                        }
                        
                        cacheOres = stringBuilder.ToString();
                        stringBuilder.Clear();
                    }
                    
                    MyAPIGateway.Utilities.ShowMissionScreen("Ores and ingots info", "", "", cacheOres, null, "Close");
                }
                else if(msg.StartsWith("item") || msg.StartsWith("weapon") || msg.StartsWith("tool"))
                {
                    if(cacheItems == null)
                    {
                        var defList = MyDefinitionManager.Static.GetAllDefinitions();
                        stringBuilder.Clear();
                        
                        foreach(var def in defList)
                        {
                            if(def is MyPhysicalItemDefinition)
                            {
                                var item = def as MyPhysicalItemDefinition;
                                
                                if(item is MyComponentDefinition || item is MyAmmoMagazineDefinition || item.Id.TypeId == typeof(MyObjectBuilder_Ingot) || item.Id.TypeId == typeof(MyObjectBuilder_Ore))
                                    continue;
                                
                                stringBuilder.Append(item.DisplayNameText).AppendLine().AppendLine();
                                
                                if(!item.Context.IsBaseGame)
                                    stringBuilder.Append("  Mod: ").Append(item.Context.ModName).AppendLine();
                                
                                stringBuilder.Append("  Volume: ").Append((item.Volume * 1000).ToString(NUMBER_FORMAT)).Append(" L").AppendLine();
                                stringBuilder.Append("  Mass: ").Append(MassFormat(item.Mass)).AppendLine();
                                
                                var weapon = item as MyWeaponItemDefinition;
                                var bottle = item as MyOxygenContainerDefinition;
                                
                                if(weapon != null)
                                {
                                    var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);
                                    
                                    stringBuilder.Append("  Ammo: ").AppendLine();
                                    
                                    for(int i = 0; i < wepDef.AmmoMagazinesId.Length; i++)
                                    {
                                        var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[i]);
                                        var ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                                        var weaponData = wepDef.WeaponAmmoDatas[(int)ammo.AmmoType];
                                        
                                        stringBuilder.Append("    - ").Append(mag.DisplayNameText).Append(" (").Append(weaponData.RateOfFire.ToString(FLOAT_FORMAT)).Append(" RPM)").AppendLine();
                                    }
                                }
                                else if(bottle != null)
                                {
                                    stringBuilder.Append("  Capacity: ").Append(bottle.Capacity.ToString(NUMBER_FORMAT)).Append(" O2").AppendLine();
                                    stringBuilder.Append("  Stored gas: ").Append(bottle.StoredGasId.SubtypeName).AppendLine();
                                }
                                
                                stringBuilder.AppendLine().AppendLine();
                            }
                        }
                        
                        if(stringBuilder.Length > 2)
                            stringBuilder.Length -= 2;
                        
                        cacheItems = stringBuilder.ToString();
                        stringBuilder.Clear();
                    }
                    
                    MyAPIGateway.Utilities.ShowMissionScreen("Items and tools info", "", "", cacheItems, null, "Close");
                }
                else if(msg.StartsWith("suit") || msg.StartsWith("char"))
                {
                    if(cacheSuits == null)
                    {
                        var defList = MyDefinitionManager.Static.Characters;
                        stringBuilder.Clear();
                        HashSet<string> helmetVariations = new HashSet<string>();
                        int listed = 0;
                        
                        foreach(var suit in defList)
                        {
                            if(helmetVariations.Contains(suit.Name))
                                continue;
                            
                            bool hasHelmet = suit.HelmetVariation != null && suit.HelmetVariation.Length > 0;
                            
                            if(hasHelmet && !helmetVariations.Contains(suit.HelmetVariation))
                                helmetVariations.Add(suit.HelmetVariation);
                            
                            stringBuilder.Append(suit.Name).AppendLine().AppendLine();
                            listed++;
                            
                            if(!suit.Context.IsBaseGame)
                                stringBuilder.Append("  Mod: ").Append(suit.Context.ModName).AppendLine();
                            
                            stringBuilder.Append("  Skeleton: ").Append(suit.Skeleton).AppendLine();
                            stringBuilder.Append("  Mass: ").Append(MassFormat(suit.Mass)).AppendLine();
                            
                            MyStatsDefinition stats = null;
                            stringBuilder.Append("  Stats: ").AppendLine();
                            
                            if(MyDefinitionManager.Static.TryGetDefinition<MyStatsDefinition>(new MyDefinitionId(typeof(MyObjectBuilder_StatsDefinition), suit.Stats), out stats))
                            {
                                MyEntityStatDefinition stat = null;
                                
                                foreach(MyDefinitionId current in stats.Stats)
                                {
                                    if(MyDefinitionManager.Static.TryGetDefinition<MyEntityStatDefinition>(current, out stat))
                                    {
                                        stringBuilder.Append("    ").Append(stat.Name).Append(": ").Append(stat.MaxValue).AppendLine();
                                        
                                        /*
                                        MyStringHash orCompute = MyStringHash.GetOrCompute(myEntityStatDefinition.Name);
                                        MyEntityStat myEntityStat = null;
                                        if (!this.m_stats.TryGetValue(current.SubtypeId, out myEntityStat) || !(myEntityStat.StatId == orCompute))
                                        {
                                            this.AddStat(orCompute, new MyObjectBuilder_EntityStat
                                                         {
                                                             SubtypeName = current.SubtypeName,
                                                             MaxValue = 1f,
                                                             Value = myEntityStatDefinition.DefaultValue / myEntityStatDefinition.MaxValue
                                                         });
                                        }
                                         */
                                    }
                                }
                            }
                            
                            /*
                            MyEntityStat health = this.StatComp.Health;
                            if (health != null)
                            {
                                if (myObjectBuilder_Character.Health.HasValue)
                                {
                                    health.Value = myObjectBuilder_Character.Health.Value;
                                }
                                health.OnStatChanged += new MyEntityStat.StatChangedDelegate(this.StatComp.OnHealthChanged);
                            }
                             */
                            
                            //stringBuilder.Append("  Height: ").Append(suit.CharacterHeight.ToString(NUMBER_FORMAT)).Append(" m").AppendLine(); // seems always 0
                            
                            stringBuilder.Append("  Inventory capacity: ").Append((suit.InventoryDefinition.InventoryVolume * 1000).ToString(NUMBER_FORMAT)).Append(" L").AppendLine();
                            stringBuilder.Append("  Oxygen:").AppendLine();
                            stringBuilder.Append("    Helmet: ").Append(hasHelmet ? "Yes" : "No").AppendLine();
                            stringBuilder.Append("    Pressurized: ").Append(suit.NeedsOxygen ? "No" : "Yes").AppendLine();
                            stringBuilder.Append("    Capacity: ").Append(suit.OxygenCapacity.ToString(NUMBER_FORMAT)).Append(" O2").AppendLine();
                            stringBuilder.Append("    Consumes: ").Append(suit.OxygenConsumption.ToString(NUMBER_FORMAT)).Append(" O2/s").AppendLine();
                            stringBuilder.Append("    Time: ").Append(TimeFormat((int)(suit.OxygenCapacity / suit.OxygenConsumption))).AppendLine();
                            
                            stringBuilder.Append("  Movement:").AppendLine();
                            stringBuilder.Append("    Walking: ").Append(suit.MaxWalkSpeed).Append(" m/s").AppendLine();
                            stringBuilder.Append("    Running: ").Append(suit.MaxRunSpeed).Append(" m/s").AppendLine();
                            stringBuilder.Append("    Sprinting: ").Append(suit.MaxSprintSpeed).Append(" m/s").AppendLine();
                            stringBuilder.Append("    Crouching: ").Append(suit.MaxCrouchWalkSpeed).Append(" m/s").AppendLine();
                            
                            if(suit.Jetpack == null)
                            {
                                stringBuilder.Append("  Jetpack: No").AppendLine();
                            }
                            else
                            {
                                stringBuilder.Append("  Jetpack:").AppendLine();
                                stringBuilder.Append("    Force: ").Append(ForceFormat(suit.Jetpack.ThrustProperties.ForceMagnitude)).AppendLine();
                                stringBuilder.Append("    Slowdown factor: ").Append(ForceFormat(suit.Jetpack.ThrustProperties.SlowdownFactor)).AppendLine();
                                stringBuilder.Append("    Power usage - idle: ").Append(PowerFormat(suit.Jetpack.ThrustProperties.MinPowerConsumption)).Append(", usage: ").Append(PowerFormat(suit.Jetpack.ThrustProperties.MaxPowerConsumption)).AppendLine();
                            }
                            
                            stringBuilder.AppendLine().AppendLine();
                        }
                        
                        if(helmetVariations.Count > 0)
                        {
                            stringBuilder.AppendLine();
                            
                            if(helmetVariations.Count == 1)
                                stringBuilder.Append("There is one other unlisted suit that acts as helmet toggle for the listed ").Append(listed == 1 ? "one" : "ones").Append('.');
                            else
                                stringBuilder.Append("There are ").Append(helmetVariations.Count).Append(" other unlisted suits that act as helmet toggles for the listed ").Append(listed == 1 ? "one" : "ones").Append('.');
                        }
                        
                        cacheSuits = stringBuilder.ToString();
                        stringBuilder.Clear();
                    }
                    
                    MyAPIGateway.Utilities.ShowMissionScreen("Items and tools info", "", "", cacheSuits, null, "Close");
                }
                else
                {
                    MyAPIGateway.Utilities.ShowMessage(Log.MOD_NAME, "Commands list:");
                    MyAPIGateway.Utilities.ShowMessage("/info on/off ", "turn the build info on or off");
                    MyAPIGateway.Utilities.ShowMessage("/info comps ", "components list");
                    MyAPIGateway.Utilities.ShowMessage("/info ammo ", "magazines and ammo list");
                    MyAPIGateway.Utilities.ShowMessage("/info ores ", "ores and ingots list");
                    MyAPIGateway.Utilities.ShowMessage("/info items ", "items and tools list");
                    MyAPIGateway.Utilities.ShowMessage("/info suits ", "character suits list");
                    MyAPIGateway.Utilities.ShowMessage("/info mods ", "mods list");
                }
            }
        }
    }
}