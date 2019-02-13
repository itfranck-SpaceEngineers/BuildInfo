﻿using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.BlockData;
using Digi.BuildInfo.Extensions;
using Digi.Input;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static Digi.BuildInfo.Pressurization;

namespace Digi.BuildInfo
{
    public partial class BuildInfo
    {
        private TriState willSplitGrid;

        // menu specific stuff
        private bool showMenu = false;
        private bool menuNeedsUpdate = true;
        private int menuSelectedItem = 0;

        // used by the textAPI view mode
        public HudAPIv2 TextAPI = null;
        private bool useLeftSide = true;
        private HudMode hudState;
        private double aspectRatio = 1;
        private float hudBackgroundOpacity = 1f;
        private int lines;
        private StringBuilder textAPIlines = null;
        private HudAPIv2.HUDMessage textObject = null;
        private HudAPIv2.BillBoardHUDMessage bgObject = null;
        private HudAPIv2.SpaceMessage[] textAPILabels;
        private HudAPIv2.SpaceMessage[] textAPIShadows;
        public readonly Dictionary<MyDefinitionId, Cache> CachedBuildInfoTextAPI = new Dictionary<MyDefinitionId, Cache>(MyDefinitionId.Comparer);
        private float TextAPIScale => Settings.textAPIScale * TEXTAPI_SCALE_BASE;
        private const float TEXTAPI_SCALE_BASE = 1.2f;
        private const int TEXTAPI_TEXT_LENGTH = 2048;

        // used by the HUD notification view mode
        private int atLine = SCROLL_FROM_LINE;
        private long lastScroll = 0;
        private int largestLineWidth = 0;
        private List<HudLine> notificationLines = new List<HudLine>();
        public readonly Dictionary<MyDefinitionId, Cache> CachedBuildInfoNotification = new Dictionary<MyDefinitionId, Cache>(MyDefinitionId.Comparer);
        public readonly List<IMyHudNotification> hudNotifLines = new List<IMyHudNotification>();

        // used in generating the block info text or menu for either view mode
        private int line = -1;
        private bool addLineCalled = false;

        // used to quickly find the format method for block types
        private delegate void TextGenerationCall(MyCubeBlockDefinition def);
        private readonly Dictionary<MyObjectBuilderType, TextGenerationCall> formatLookup
                   = new Dictionary<MyObjectBuilderType, TextGenerationCall>(MyObjectBuilderType.Comparer);

        // caches
        private readonly List<MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition>> ammoProjectiles
                   = new List<MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition>>();
        private readonly List<MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition>> ammoMissiles
                   = new List<MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition>>();

        // constants
        public readonly Color COLOR_BLOCKTITLE = new Color(50, 155, 255);
        public readonly Color COLOR_BLOCKVARIANTS = new Color(255, 233, 55);
        public readonly Color COLOR_NORMAL = Color.White;
        public readonly Color COLOR_GOOD = Color.Lime;
        public readonly Color COLOR_BAD = Color.Red;
        public readonly Color COLOR_WARNING = Color.Yellow;
        public readonly Color COLOR_UNIMPORTANT = Color.Gray;
        public readonly Color COLOR_PART = new Color(55, 255, 155);
        public readonly Color COLOR_MOD = Color.DeepSkyBlue;
        public readonly Color COLOR_MOD_TITLE = Color.GreenYellow;
        public readonly Color COLOR_OWNER = new Color(55, 255, 255);
        public readonly Color COLOR_INFO = new Color(69, 177, 227);

        public readonly Color COLOR_STAT_PROJECTILECOUNT = new Color(0, 255, 0);
        public readonly Color COLOR_STAT_SHIPDMG = new Color(0, 255, 200);
        public readonly Color COLOR_STAT_CHARACTERDMG = new Color(255, 155, 0);
        public readonly Color COLOR_STAT_HEADSHOTDMG = new Color(255, 0, 0);
        public readonly Color COLOR_STAT_SPEED = new Color(0, 200, 255);
        public readonly Color COLOR_STAT_TRAVEL = new Color(55, 80, 255);

        #region Text handling
        private void PostProcessText(MyDefinitionId id, bool useCache)
        {
            if(TextAPIEnabled)
            {
                var textSize = UpdateTextAPIvisuals(textAPIlines);

                if(useCache)
                {
                    cache = new CacheTextAPI(textAPIlines, textSize);

                    if(CachedBuildInfoTextAPI.ContainsKey(id))
                        CachedBuildInfoTextAPI[id] = cache;
                    else
                        CachedBuildInfoTextAPI.Add(id, cache);
                }
            }
            else
            {
                long now = DateTime.UtcNow.Ticks;
                lastScroll = now + TimeSpan.TicksPerSecond;
                atLine = SCROLL_FROM_LINE;

                for(int i = line; i >= 0; --i)
                {
                    var l = notificationLines[i];

                    var textWidthPx = largestLineWidth - l.lineWidthPx;

                    int fillChars = (int)Math.Floor((float)textWidthPx / (float)SPACE_SIZE);

                    if(fillChars > 0)
                    {
                        l.str.Append(' ', fillChars);
                    }
                }

                if(useCache)
                {
                    cache = new CacheNotifications(notificationLines);

                    if(CachedBuildInfoNotification.ContainsKey(id))
                        CachedBuildInfoNotification[id] = cache;
                    else
                        CachedBuildInfoNotification.Add(id, cache);
                }
            }
        }

        private Vector2D UpdateTextAPIvisuals(StringBuilder textSB, Vector2D textSize = default(Vector2D))
        {
            if(textObject == null)
            {
                textObject = new HudAPIv2.HUDMessage(new StringBuilder(TEXTAPI_TEXT_LENGTH), Vector2D.Zero, Scale: TextAPIScale, HideHud: !Settings.alwaysVisible, Blend: BLOCKINFO_FG_BLEND_TYPE);
            }

            if(bgObject == null)
            {
                bgObject = new HudAPIv2.BillBoardHUDMessage(MATERIAL_VANILLA_SQUARE, Vector2D.Zero, Color.White, HideHud: !Settings.alwaysVisible, Shadowing: true, Blend: BLOCKINFO_BG_BLEND_TYPE); // scale on bg must always remain 1
            }

            bgObject.Visible = true;

            textObject.Visible = true;

            #region Update text and count lines
            var msg = textObject.Message;
            msg.Clear().EnsureCapacity(msg.Length + textSB.Length);
            lines = 0;

            for(int i = 0; i < textSB.Length; i++)
            {
                var c = textSB[i];

                msg.Append(c);

                if(c == '\n')
                    lines++;
            }
            #endregion

            var textPos = Vector2D.Zero;
            var textOffset = Vector2D.Zero;

            // calculate text size if it wasn't inputted
            if(Math.Abs(textSize.X) <= 0.0001 && Math.Abs(textSize.Y) <= 0.0001)
                textSize = textObject.GetTextLength();

            if(showMenu) // in the menu
            {
                textOffset = new Vector2D(-textSize.X, textSize.Y / -2);
            }
#if false
            else if(selectedBlock != null) // welder/grinder info attached to the game's block info
            {
                var cam = MyAPIGateway.Session.Camera;
                var camMatrix = cam.WorldMatrix;

                var hud = GetGameHudBlockInfoPos();
                hud.Y -= (BLOCKINFO_ITEM_HEIGHT * selectedDef.Components.Length) + BLOCKINFO_Y_OFFSET; // make the position top-right

                var worldPos = HudToWorld(hud);
                var size = GetGameHudBlockInfoSize((float)Math.Abs(textSize.Y) / 0.03f);
                var offset = new Vector2D(BLOCKINFO_TEXT_PADDING, BLOCKINFO_TEXT_PADDING) * ScaleFOV;

                worldPos += camMatrix.Left * (size.X + (size.X - offset.X)) + camMatrix.Up * (size.Y + (size.Y - offset.Y));

                // using textAPI's math to convert from world to its local coords
                double localScale = 0.1 * ScaleFOV;
                var local = Vector3D.Transform(worldPos, cam.ViewMatrix);
                local.X = (local.X / (localScale * aspectRatio)) * 2;
                local.Y = (local.Y / localScale) * 2;

                textPos.X = local.X;
                textPos.Y = local.Y;

                // not using textAPI's background for this as drawing my own manually is easier for the 3-part billboard that I need
                bgObject.Visible = false;
            }
#endif
            else if(Settings.textAPIUseCustomStyling) // custom alignment and position
            {
                textPos = Settings.textAPIScreenPos;

                if(Settings.textAPIAlignRight)
                    textOffset.X = -textSize.X;

                if(Settings.textAPIAlignBottom)
                    textOffset.Y = -textSize.Y;
            }
            else if(!useLeftSide) // right side autocomputed
            {
                textPos = (aspectRatio > 5 ? TEXT_HUDPOS_RIGHT_WIDE : TEXT_HUDPOS_RIGHT);
                textOffset = new Vector2D(-textSize.X, 0);
            }
            else // left side autocomputed
            {
                textPos = (aspectRatio > 5 ? TEXT_HUDPOS_WIDE : TEXT_HUDPOS);
            }

            textObject.Origin = textPos;
            textObject.Offset = textOffset;

#if false
            if(showMenu || selectedBlock == null)
#endif
            {
                float edge = BACKGROUND_EDGE * TextAPIScale;

                bgObject.BillBoardColor = BLOCKINFO_BG_COLOR * Math.Min(BG_OPACITY_CAP, showMenu ? MENU_BG_OPACITY : (Settings.textAPIBackgroundOpacity < 0 ? hudBackgroundOpacity : Settings.textAPIBackgroundOpacity));
                bgObject.Origin = textPos;
                bgObject.Width = (float)Math.Abs(textSize.X) + edge;
                bgObject.Height = (float)Math.Abs(textSize.Y) + edge;
                bgObject.Offset = textOffset + (textSize / 2);
            }

            textShown = true;
            return textSize;
        }

        private void UpdateVisualText()
        {
            if(TextAPIEnabled)
            {
                if(MyAPIGateway.Gui.IsCursorVisible || (!Settings.showTextInfo && !showMenu))
                {
                    HideText();
                    return;
                }

                // force reset, usually needed to fix notification to textAPI transition when heartbeat returns true
                if(textObject == null || (cache == null && !(showMenu || selectedBlock != null)))
                {
                    lastDefId = default(MyDefinitionId);
                    return;
                }

                // show last generated block info message only for cubebuilder
                if(!textShown && textObject != null)
                {
                    if(showMenu || selectedBlock != null)
                    {
                        UpdateTextAPIvisuals(textAPIlines);
                    }
                    else if(cache != null)
                    {
                        var cacheTextAPI = (CacheTextAPI)cache;
                        cacheTextAPI.ResetExpiry();
                        UpdateTextAPIvisuals(cacheTextAPI.Text, cacheTextAPI.TextSize);
                    }
                }
            }
            else
            {
                if(MyAPIGateway.Gui.IsCursorVisible || (!Settings.showTextInfo && !showMenu))
                {
                    return;
                }

                List<IMyHudNotification> hudLines = null;

                if(showMenu || selectedBlock != null)
                {
                    hudLines = hudNotifLines;

                    for(int i = 0; i < notificationLines.Count; ++i)
                    {
                        var line = notificationLines[i];

                        if(line.str.Length > 0)
                        {
                            if(hudLines.Count <= i)
                            {
                                hudLines.Add(MyAPIGateway.Utilities.CreateNotification(line.str.ToString(), 16, line.font));
                            }
                            else
                            {
                                hudLines[i].Text = line.str.ToString();
                                hudLines[i].Font = line.font;
                            }
                        }
                        else if(hudLines.Count > i)
                        {
                            hudLines[i].Text = "";
                        }
                    }
                }
                else
                {
                    if(cache == null)
                    {
                        lastDefId = default(MyDefinitionId);
                        return;
                    }

                    if(!textShown)
                    {
                        textShown = true;
                        cache.ResetExpiry();
                    }

                    hudLines = ((CacheNotifications)cache).Lines;
                }

                int lines = 0;

                foreach(var hud in hudLines)
                {
                    if(hud.Text.Length > 0)
                        lines++;

                    hud.Hide();
                }

                if(showMenu)
                {
                    // HACK this must match the data from the menu
                    const int itemsStartAt = 1;
                    const int itemsEndAt = MENU_TOTAL_ITEMS;

                    var selected = itemsStartAt + menuSelectedItem;

                    for(int l = 0; l < lines; ++l)
                    {
                        if(l < itemsStartAt
                        || l > itemsEndAt
                        || (selected == itemsEndAt && l == (selected - 2))
                        || l == (selected - 1)
                        || l == selected
                        || l == (selected + 1)
                        || (selected == itemsStartAt && l == (selected + 2)))
                        {
                            var hud = hudLines[l];
                            hud.ResetAliveTime();
                            hud.Show();
                        }
                    }
                }
                else
                {
                    if(lines > MAX_LINES)
                    {
                        int l;

                        for(l = 0; l < lines; ++l)
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

                        long now = DateTime.UtcNow.Ticks;

                        if(lastScroll < now)
                        {
                            if(++atLine >= lines)
                                atLine = SCROLL_FROM_LINE;

                            lastScroll = now + (long)(TimeSpan.TicksPerSecond * 1.5f);
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
        }

        private void HideText()
        {
            if(textShown)
            {
                textShown = false;
                lastDefId = default(MyDefinitionId);

                // text API hide
                if(textObject != null)
                    textObject.Visible = false;

                if(bgObject != null)
                    bgObject.Visible = false;

                // HUD notifications don't need hiding, they expire in one frame.
            }
        }

        private void ResetLines()
        {
            if(TextAPIEnabled)
            {
                textAPIlines.Clear();
            }
            else
            {
                foreach(var l in notificationLines)
                {
                    l.str.Clear();
                }
            }

            line = -1;
            largestLineWidth = 0;
            addLineCalled = false;
        }

        private StringBuilder AddLine(string font = MyFontEnum.White)
        {
            EndAddedLines();
            addLineCalled = true;

            ++line;

            if(TextAPIEnabled)
            {
                return textAPIlines;
            }
            else
            {
                if(line >= notificationLines.Count)
                    notificationLines.Add(new HudLine());

                var nl = notificationLines[line];
                nl.font = font;

                return nl.str.Append("• ");
            }
        }

        public void EndAddedLines()
        {
            if(!addLineCalled)
                return;

            addLineCalled = false;

            if(TextAPIEnabled)
            {
                textAPIlines.ResetColor().Append('\n');
            }
            else
            {
                var px = GetStringSizeNotif(notificationLines[line].str);

                largestLineWidth = Math.Max(largestLineWidth, px);

                notificationLines[line].lineWidthPx = px;
            }
        }

        private StringBuilder GetLine()
        {
            return (TextAPIEnabled ? textAPIlines : notificationLines[line].str);
        }

        private void AddOverlaysHint(MyCubeBlockDefinition def)
        {
            if(drawLookup.ContainsKey(def.Id.TypeId))
            {
                AddLine(MyFontEnum.DarkBlue).Color(COLOR_UNIMPORTANT).Append("(Overlay available. ");
                Settings.CycleOverlaysBind.GetBinds(GetLine());
                GetLine().Append(" to cycle)").EndLine();
            }
        }

        public static int GetStringSizeNotif(StringBuilder builder)
        {
            int endLength = builder.Length;
            int len;
            int size = 0;

            for(int i = 0; i < endLength; ++i)
            {
                if(Instance.charSize.TryGetValue(builder[i], out len))
                    size += len;
                else
                    size += 15;
            }

            return size;
        }
        #endregion Text handling

        #region Menu generation
        private StringBuilder AddMenuItemLine(int item, bool enabled = true)
        {
            AddLine(font: (menuSelectedItem == item ? MyFontEnum.Green : (enabled ? MyFontEnum.White : MyFontEnum.Red)));

            if(menuSelectedItem == item)
                GetLine().Color(COLOR_GOOD).Append("  > ");
            else
                GetLine().Color(enabled ? COLOR_NORMAL : COLOR_UNIMPORTANT).Append(' ', 6);

            return GetLine();
        }

        private void GenerateMenuText()
        {
            ResetLines();

            bool canUseTextAPI = (TextAPI != null && TextAPI.Heartbeat);

            AddLine(MyFontEnum.Blue).Color(COLOR_BLOCKTITLE).Append("Build info mod").EndLine();

            int i = 0;

            // HACK this must match the data from the HandleInput() which controls the actual actions of these

            AddMenuItemLine(i++).Append("Close menu");

            GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
            if(Settings.MenuBind.IsAssigned())
            {
                Settings.MenuBind.GetBinds(GetLine());
            }
            else
            {
                GetLine().Append(CMD_BUILDINFO);
            }
            GetLine().Append(")").EndLine();

            if(TextAPIEnabled)
            {
                AddLine().EndLine();
                AddLine().Color(COLOR_BLOCKTITLE).Append("Actions:").EndLine();
            }

            AddMenuItemLine(i++).Append("Add aimed block to toolbar");
            GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
            if(Settings.BlockPickerBind.IsAssigned())
            {
                Settings.BlockPickerBind.GetBinds(GetLine());
            }
            else
            {
                GetLine().Append(CMD_GETBLOCK);
            }
            GetLine().Append(")").EndLine();

            AddMenuItemLine(i++).Append("Open block's mod workshop link").Color(COLOR_UNIMPORTANT).Append("   (").Append(CMD_MODLINK).Append(')').EndLine();

            AddMenuItemLine(i++).Append("Help topics").Color(COLOR_UNIMPORTANT).Append("   (").Append(CMD_HELP).Append(')').EndLine();

            if(TextAPIEnabled)
            {
                AddLine().EndLine();
                AddLine().Color(COLOR_BLOCKTITLE).Append("Settings:").EndLine();
            }

            AddMenuItemLine(i++).Append("Text info: ").Append(Settings.showTextInfo ? "ON" : "OFF").EndLine();

            AddMenuItemLine(i++).Append("Draw overlays: ").Append(DRAW_OVERLAY_NAME[drawOverlay]);
            if(Settings.CycleOverlaysBind.IsAssigned())
            {
                GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
                Settings.CycleOverlaysBind.GetBinds(GetLine());
                GetLine().Append(")").ResetColor();
            }
            GetLine().EndLine();

            AddMenuItemLine(i++).Append("Placement transparency: ").Append(MyCubeBuilder.Static.UseTransparency ? "ON" : "OFF");
            if(Settings.ToggleTransparencyBind.IsAssigned())
            {
                GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
                Settings.ToggleTransparencyBind.GetBinds(GetLine());
                GetLine().Append(")").ResetColor();
            }
            GetLine().EndLine();

            AddMenuItemLine(i++).Append("Freeze in position: ").Append(MyAPIGateway.CubeBuilder.FreezeGizmo ? "ON" : "OFF");
            if(Settings.FreezePlacementBind.IsAssigned())
            {
                GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
                Settings.FreezePlacementBind.GetBinds(GetLine());
                GetLine().Append(")").ResetColor();
            }
            GetLine().EndLine();

            AddMenuItemLine(i++, canUseTextAPI).Append("Use TextAPI: ");
            if(canUseTextAPI)
                GetLine().Append(useTextAPI ? "ON" : "OFF");
            else
                GetLine().Append("OFF (Mod not detected)");
            GetLine().EndLine();

            AddMenuItemLine(i++).Append("Reload settings file").Color(COLOR_UNIMPORTANT).Append("   (").Append(CMD_RELOAD).Append(')').EndLine();

            if(TextAPIEnabled)
                AddLine().EndLine();

            AddLine(MyFontEnum.Blue).Color(COLOR_INFO).Append("Navigation: Up/down = ").Append(MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE.GetAssignedInputName()).Append("/").Append(MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE.GetAssignedInputName()).Append(", change = ").Append(MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE.GetAssignedInputName()).ResetColor().Append(' ', 10).EndLine();

            EndAddedLines();
        }
        #endregion

        #region Aimed block info generation
        private void GenerateAimBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            var integrityRatio = selectedBlock.Integrity / selectedBlock.MaxIntegrity;
            var grid = selectedBlock.CubeGrid;

            var terminalBlock = selectedBlock.FatBlock as IMyTerminalBlock;
            bool hasComputer = (terminalBlock != null && def.ContainsComputer());

            #region Block name
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.BlockName) && terminalBlock != null)
            {
                const int LENGTH_LIMIT = 35;

                AddLine().Append('"').Color(COLOR_BLOCKTITLE);

                var name = terminalBlock.CustomName;
                var newLine = name.IndexOf('\n');

                if(newLine >= 0)
                    name = name.Substring(0, newLine); // strip everything past new line (incl new line char)

                GetLine().AppendMaxLength(name, LENGTH_LIMIT).ResetColor().Append('"').EndLine();
            }
            #endregion

            #region Mass, grid mass
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.Mass))
            {
                var mass = def.Mass;
                var massColor = Color.GreenYellow;

                if(selectedBlock.FatBlock != null)
                {
                    var inv = selectedBlock.FatBlock.GetInventory();

                    if(inv != null)
                    {
                        var invMass = (float)inv.CurrentMass;

                        if(invMass > 0)
                        {
                            mass += invMass;
                            massColor = COLOR_WARNING;
                        }
                    }
                }

                AddLine().Color(massColor).MassFormat(mass);

                if(grid.Physics != null)
                {
                    GetLine().ResetColor().Separator().Append(" Grid mass: ").MassFormat(selectedBlock.CubeGrid.Physics.Mass);
                }

                GetLine().EndLine();
            }
            #endregion

            #region Integrity
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.Integrity))
            {
                AddLine().ResetColor().Append("Integrity: ").Color(integrityRatio < def.CriticalIntegrityRatio ? COLOR_BAD : (integrityRatio < 1 ? COLOR_WARNING : COLOR_GOOD))
                .IntegrityFormat(selectedBlock.Integrity).ResetColor()
                .Append(" / ").IntegrityFormat(selectedBlock.MaxIntegrity);

                if(def.BlockTopology == MyBlockTopology.Cube && selectedBlock.HasDeformation)
                {
                    GetLine().Color(COLOR_BAD).Append(" (deformed)");
                }

                GetLine().EndLine();
            }
            #endregion

            #region Optional: intake damage multiplier
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.DamageMultiplier) && Math.Abs(def.GeneralDamageMultiplier - 1) >= 0.0001f)
            {
                AddLine().Color(def.GeneralDamageMultiplier > 1 ? COLOR_BAD : (def.GeneralDamageMultiplier < 1 ? COLOR_GOOD : COLOR_NORMAL)).Append("Damage multiplier: ").Number(def.GeneralDamageMultiplier).EndLine();
            }
            #endregion

            #region Optional: ownership
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.Ownership) && hasComputer)
            {
                AddLine();

                var relation = (selectedBlock.OwnerId > 0 ? MyAPIGateway.Session.Player.GetRelationTo(selectedBlock.OwnerId) : MyRelationsBetweenPlayerAndBlock.NoOwnership);
                var shareMode = GameData.GetBlockShareMode(selectedBlock.FatBlock);

                if(relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                {
                    GetLine().Color(COLOR_GOOD).Append("Access: all");
                }
                else if(shareMode == MyOwnershipShareModeEnum.All)
                {
                    if(relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies)
                        GetLine().Color(COLOR_GOOD);
                    else
                        GetLine().Color(COLOR_WARNING);

                    GetLine().Append("Access: all");
                }
                else if(shareMode == MyOwnershipShareModeEnum.Faction)
                {
                    if(relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                        GetLine().Color(COLOR_GOOD);
                    else
                        GetLine().Color(COLOR_BAD);

                    GetLine().Append("Access: faction");
                }
                else if(shareMode == MyOwnershipShareModeEnum.None)
                {
                    if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                        GetLine().Color(COLOR_WARNING);
                    else
                        GetLine().Color(COLOR_BAD);

                    GetLine().Append("Access: owner");
                }

                GetLine().ResetColor().Separator();

                if(relation == MyRelationsBetweenPlayerAndBlock.Enemies || relation == MyRelationsBetweenPlayerAndBlock.Neutral)
                    GetLine().Color(COLOR_BAD);
                else if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                    GetLine().Color(COLOR_OWNER);
                else if(relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                    GetLine().Color(COLOR_GOOD);
                else if(relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                    GetLine().Color(COLOR_WARNING);

                if(selectedBlock.OwnerId == 0)
                {
                    GetLine().Append("Not owned");
                }
                else
                {
                    GetLine().Append("Owner: ");

                    // NOTE: MyVisualScriptLogicProvider.GetPlayersName() returns local player on id 0 and id 0 is also use for "nobody" in ownership.
                    var factionTag = selectedBlock.FatBlock.GetOwnerFactionTag();

                    if(!string.IsNullOrEmpty(factionTag))
                        GetLine().Append(factionTag).Append('.');

                    GetLine().AppendMaxLength(MyVisualScriptLogicProvider.GetPlayersName(selectedBlock.FatBlock.OwnerId), PLAYER_NAME_MAX_LENGTH).EndLine();
                }
            }
            #endregion

            #region Time to complete/grind
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.ToolUseTime))
            {
                float toolMul = 1;

                if(selectedHandTool != null)
                {
                    var toolDef = MyDefinitionManager.Static.TryGetHandItemForPhysicalItem(selectedHandTool.PhysicalItemDefinition.Id) as MyEngineerToolBaseDefinition;
                    toolMul = (toolDef == null ? 1 : toolDef.SpeedMultiplier);
                }
                else // assuming ship tool
                {
                    toolMul = GameData.Hardcoded.ShipWelder_WeldPerSecond;
                }

                var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
                var grindMul = MyAPIGateway.Session.GrinderSpeedMultiplier;
                var grindRatio = def.DisassembleRatio;

                if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition)
                    grindRatio *= GameData.Hardcoded.Door_Closed_DisassembleRatioMultiplier;

                var buildTime = ((def.MaxIntegrity / def.IntegrityPointsPerSec) / weldMul) / toolMul;
                var grindTime = ((buildTime / (1f / grindRatio)) / grindMul);

                AddLine();

                if(!IsGrinder)
                {
                    GetLine().Append("Complete: ").TimeFormat(buildTime * (1 - integrityRatio));

                    if(def.CriticalIntegrityRatio < 1 && integrityRatio < def.CriticalIntegrityRatio)
                    {
                        var funcTime = buildTime * def.CriticalIntegrityRatio * (1 - (integrityRatio / def.CriticalIntegrityRatio));

                        GetLine().Separator().Append("Functional: ").TimeFormat(funcTime);
                    }
                }
                else
                {
                    bool hackable = hasComputer && selectedBlock.OwnerId != MyAPIGateway.Session.Player.IdentityId && (integrityRatio >= def.OwnershipIntegrityRatio);
                    float hackTime = 0f;

                    if(hackable)
                    {
                        var noOwnershipTime = (grindTime * def.OwnershipIntegrityRatio);
                        hackTime = (grindTime * ((1 - def.OwnershipIntegrityRatio) - (1 - integrityRatio))) / MyAPIGateway.Session.HackSpeedMultiplier;
                        grindTime = noOwnershipTime + hackTime;
                    }
                    else
                    {
                        grindTime *= integrityRatio;
                    }

                    GetLine().Append("Dismantled: ").TimeFormat(grindTime);

                    if(hackable)
                    {
                        GetLine().Separator().Append("Hacked: ").TimeFormat(hackTime);
                    }
                }

                GetLine().EndLine();
            }
            #endregion

            #region Optional: item changes on grind
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.GrindChangeWarning) && IsGrinder)
            {
                foreach(var comp in def.Components)
                {
                    if(comp.DeconstructItem != comp.Definition)
                    {
                        AddLine(MyFontEnum.Red).Color(COLOR_WARNING).Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText).EndLine();
                    }
                }
            }
            #endregion

            #region Optional: grid moving
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.GridMoving) && grid.Physics != null)
            {
                bool hasLinearVel = !Vector3.IsZero(grid.Physics.LinearVelocity, 0.01f);
                bool hasAngularVel = !Vector3.IsZero(grid.Physics.AngularVelocity, 0.01f);

                if(hasLinearVel || hasAngularVel)
                {
                    AddLine().Color(COLOR_WARNING);

                    if(hasLinearVel)
                    {
                        GetLine().Append("Moving: ").SpeedFormat(grid.Physics.LinearVelocity.Length(), 2);
                    }

                    if(hasAngularVel)
                    {
                        if(hasLinearVel)
                            GetLine().Separator();

                        GetLine().Append("Rotating: ").RotationSpeed((float)grid.Physics.AngularVelocity.Length(), 2);
                    }

                    GetLine().EndLine();
                }
            }
            #endregion

            #region Optional: ship grinder apply force
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.ShipGrinderImpulse) && selectedToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder))
            {
                var controller = MyAPIGateway.Session.ControlledObject as IMyShipController;

                if(controller != null)
                {
                    var impulse = GameData.ShipGrinderImpulseForce(controller.CubeGrid, selectedBlock);

                    if(impulse > 0.00001f)
                    {
                        var speed = impulse / selectedBlock.CubeGrid.Physics.Mass;

                        if(speed >= 0.5f)
                            AddLine(MyFontEnum.Red).Color(COLOR_BAD);
                        else
                            AddLine(MyFontEnum.Red).Color(COLOR_WARNING);

                        GetLine().Append("Grind impulse: ").SpeedFormat(speed, 5).Append(" (").ForceFormat(impulse).Append(")").EndLine();
                    }
                }
            }
            #endregion

            #region Optional: grinder makes grid split
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.GrindGridSplit) && IsGrinder)
            {
                if(willSplitGrid == TriState.None)
                    willSplitGrid = grid.WillRemoveBlockSplitGrid(selectedBlock) ? TriState.On : TriState.Off;

                if(willSplitGrid == TriState.On)
                    AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Grid will split if this block is removed!").EndLine();
            }
            #endregion

            #region Optional: added by mod
            var context = def.Context;
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.AddedByMod) && !context.IsBaseGame)
            {
                if(TextAPIEnabled)
                {
                    AddLine().Color(COLOR_MOD).Append("Mod:").Color(COLOR_MOD_TITLE).AppendMaxLength(context.ModName, MOD_NAME_MAX_LENGTH).EndLine();

                    var id = context.GetWorkshopID();

                    if(id > 0)
                        AddLine().Color(COLOR_MOD).Append("       | ").ResetColor().Append("Workshop ID: ").Append(id).EndLine();
                }
                else
                {
                    AddLine(MyFontEnum.Blue).Append("Mod: ").ModFormat(context).EndLine();
                }
            }
            #endregion

            #region Overlay hints
            if(Settings.AimInfo.IsSet(Settings.AimInfoFlags.OverlayHint))
            {
                AddOverlaysHint(def);
            }
            #endregion

            EndAddedLines();
        }
        #endregion

        #region Equipped block info generation
        private void GenerateBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            #region Block name line only for textAPI
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.BlockName) && TextAPIEnabled)
            {
                AddLine().Color(COLOR_BLOCKTITLE).Append(def.DisplayNameText);

                var stages = def.BlockStages;

                if(stages != null && stages.Length > 0)
                {
                    GetLine().Append("  ").Color(COLOR_BLOCKVARIANTS).Append("(Variant 1 of ").Append(stages.Length + 1).Append(")");
                }
                else
                {
                    stages = MyCubeBuilder.Static.ToolbarBlockDefinition.BlockStages;

                    if(stages != null && stages.Length > 0)
                    {
                        int num = 0;

                        for(int i = 0; i < stages.Length; ++i)
                        {
                            if(def.Id == stages[i])
                            {
                                num = i + 2; // +2 instead of +1 because the 1st block is not in the list, it's the list holder
                                break;
                            }
                        }

                        GetLine().Append("  ").Color(COLOR_BLOCKVARIANTS).Append("(Variant ").Append(num).Append(" of ").Append(stages.Length + 1).Append(")");
                    }
                }

                GetLine().EndLine();
            }
            #endregion

            AppendBasics(def, part: false);

            #region Optional - different item gain on grinding
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.GrindChangeWarning))
            {
                foreach(var comp in def.Components)
                {
                    if(comp.DeconstructItem != comp.Definition)
                    {
                        AddLine(MyFontEnum.Red).Color(COLOR_WARNING).Append("When grinding: ").Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText).EndLine();
                    }
                }
            }
            #endregion

            // TODO use? not sure if useful...
            //if(def.VoxelPlacement.HasValue)
            //{
            //    // Comment from definition: 
            //    // <!--Possible settings Both,InVoxel,OutsideVoxel,Volumetric. If volumetric set than MaxAllowed and MinAllowed will be used.-->
            //
            //    var vp = def.VoxelPlacement.Value;
            //
            //    AddLine().SetTextAPIColor(COLOR_WARNING).Append($"Terrain placement - Dynamic: ").Append(vp.DynamicMode.PlacementMode);
            //
            //    if(vp.DynamicMode.PlacementMode == VoxelPlacementMode.Volumetric)
            //        GetLine().Append(" (").Append(vp.DynamicMode.MinAllowed).Append(" to ").Append(vp.DynamicMode.MaxAllowed).Append(")");
            //
            //    GetLine().Separator().Append($"Static: ").Append(vp.StaticMode.PlacementMode);
            //
            //    if(vp.StaticMode.PlacementMode == VoxelPlacementMode.Volumetric)
            //        GetLine().Append(" (").Append(vp.StaticMode.MinAllowed).Append(" to ").Append(vp.StaticMode.MaxAllowed).Append(")");
            //
            //    GetLine().ResetTextAPIColor().EndLine();
            //}

            #region Optional - creative-only stuff
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Mirroring) && (MyAPIGateway.Session.CreativeMode || MyAPIGateway.Session.EnableCopyPaste)) // HACK Session.EnableCopyPaste used as spacemaster check
            {
                if(def.MirroringBlock != null)
                {
                    MyCubeBlockDefinition mirrorDef;
                    if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(new MyDefinitionId(def.Id.TypeId, def.MirroringBlock), out mirrorDef))
                        AddLine(MyFontEnum.Blue).Color(COLOR_GOOD).Append("Mirrors with: ").Append(mirrorDef.DisplayNameText).EndLine();
                    else
                        AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Mirrors with: ").Append(def.MirroringBlock).Append(" (Error: not found)").EndLine();
                }
            }
            #endregion

            #region Per-block info
            if(def.Id.TypeId != typeof(MyObjectBuilder_CubeBlock)) // anything non-decorative
            {
                TextGenerationCall action;

                if(formatLookup.TryGetValue(def.Id.TypeId, out action))
                {
                    action.Invoke(def);
                }
                // DEBUG HACK temporary until MyObjectBuilder_WindTurbine is whitelisted
                else if(def.Id.TypeId.ToString() == "MyObjectBuilder_WindTurbine")
                {
                    Format_PowerProducer(def);
                }
            }
            #endregion

            #region Added by mod
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.AddedByMod) && !def.Context.IsBaseGame)
            {
                AddLine(MyFontEnum.Blue).Color(COLOR_MOD).Append("Mod: ").ModFormat(def.Context).EndLine();
            }
            #endregion

            #region Overlay hints
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.OverlayHint))
            {
                AddOverlaysHint(def);
            }
            #endregion

            EndAddedLines();
        }
        #endregion

        #region Shared generation methods
        private void AppendBasics(MyCubeBlockDefinition def, bool part = false)
        {
            int airTightFaces = 0;
            int totalFaces = 0;
            var airTight = GetAirTightFaces(def, ref airTightFaces, ref totalFaces);
            var deformable = (def.BlockTopology == MyBlockTopology.Cube && def.UsesDeformation);
            var assembleTime = (int)(def.MaxIntegrity / def.IntegrityPointsPerSec);
            var buildModels = (def.BuildProgressModels != null && def.BuildProgressModels.Length > 0);
            var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
            var grindRatio = def.DisassembleRatio;

            if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition)
            {
                grindRatio *= GameData.Hardcoded.Door_Closed_DisassembleRatioMultiplier;
            }

            string padding = (part ? (TextAPIEnabled ? "        | " : "       | ") : "");

            if(part)
                AddLine(MyFontEnum.Blue).Color(COLOR_PART).Append("Part: ").Append(def.DisplayNameText).EndLine();

            #region Mass/size/build time/deconstruct time/no models
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Line1))
            {
                AddLine();

                if(part)
                    GetLine().Color(COLOR_PART).Append(padding);

                GetLine().Color(new Color(200, 255, 55)).MassFormat(def.Mass).ResetColor().Separator()
                    .VectorFormat(def.Size).Separator()
                    .TimeFormat(assembleTime / weldMul).Color(COLOR_UNIMPORTANT).MultiplierFormat(weldMul).ResetColor();

                if(Math.Abs(grindRatio - 1) >= 0.0001f)
                    GetLine().Separator().Color(grindRatio > 1 ? COLOR_BAD : (grindRatio < 1 ? COLOR_GOOD : COLOR_NORMAL)).Append("Deconstructs: ").ProportionToPercent(1f / grindRatio).ResetColor();

                if(!buildModels)
                    GetLine().Separator().Color(COLOR_WARNING).Append("(No construction models)").ResetColor();

                GetLine().EndLine();
            }
            #endregion

            #region Integrity, deformable, damage intake
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Line2))
            {
                AddLine();

                if(part)
                    GetLine().Color(COLOR_PART).Append(padding).ResetColor();

                GetLine().Append("Integrity: ").AppendFormat("{0:#,###,###,###,###}", def.MaxIntegrity).Separator();

                GetLine().Color(deformable ? COLOR_WARNING : COLOR_NORMAL).Append("Deformable: ");
                if(deformable)
                    GetLine().Append("Yes (").ProportionToPercent(def.DeformationRatio).Append(")");
                else
                    GetLine().Append("No");

                GetLine().ResetColor();

                if(Math.Abs(def.GeneralDamageMultiplier - 1) >= 0.0001f)
                {
                    GetLine().Separator()
                        .Color(def.GeneralDamageMultiplier > 1 ? COLOR_BAD : (def.GeneralDamageMultiplier < 1 ? COLOR_GOOD : COLOR_NORMAL))
                        .Append("Damage intake: ").ProportionToPercent(def.GeneralDamageMultiplier)
                        .ResetColor();
                }

                // DEBUG TODO add HasPhysics and IsStandAlone

                GetLine().EndLine();
            }
            #endregion

            #region Airtightness
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Airtight))
            {
                AddLine(font: (airTight == AirTightMode.SEALED ? MyFontEnum.Green : (airTight == AirTightMode.NOT_SEALED ? MyFontEnum.Red : MyFontEnum.Blue)));

                if(part)
                    GetLine().Color(COLOR_PART).Append(padding);

                GetLine().Color(airTight == AirTightMode.SEALED ? COLOR_GOOD : (airTight == AirTightMode.NOT_SEALED ? COLOR_BAD : COLOR_WARNING)).Append("Air-tight: ");

                if(airTight == AirTightMode.SEALED)
                    GetLine().Append("Sealed");
                else if(airTight == AirTightMode.NOT_SEALED)
                    GetLine().Append("Not sealed");
                else
                    GetLine().Append(airTightFaces).Append(" of ").Append(totalFaces).Append(" faces are sealed");

                GetLine().EndLine();
            }
            #endregion
        }
        #endregion

        #region Per block info
        public void InitTextGeneration()
        {
            textAPIlines = new StringBuilder(TEXTAPI_TEXT_LENGTH);

            formatLookup.Add(typeof(MyObjectBuilder_TerminalBlock), Format_TerminalBlock);

            formatLookup.Add(typeof(MyObjectBuilder_Conveyor), Format_Conveyors);
            formatLookup.Add(typeof(MyObjectBuilder_ConveyorConnector), Format_Conveyors);

            formatLookup.Add(typeof(MyObjectBuilder_ShipConnector), Format_Connector);

            formatLookup.Add(typeof(MyObjectBuilder_Collector), Format_CargoAndCollector);
            formatLookup.Add(typeof(MyObjectBuilder_CargoContainer), Format_CargoAndCollector);

            formatLookup.Add(typeof(MyObjectBuilder_ConveyorSorter), Format_ConveyorSorter);

            formatLookup.Add(typeof(MyObjectBuilder_Drill), Format_Drill);

            formatLookup.Add(typeof(MyObjectBuilder_ShipWelder), Format_WelderAndGrinder);
            formatLookup.Add(typeof(MyObjectBuilder_ShipGrinder), Format_WelderAndGrinder);

            formatLookup.Add(typeof(MyObjectBuilder_PistonBase), Format_Piston);
            formatLookup.Add(typeof(MyObjectBuilder_ExtendedPistonBase), Format_Piston);

            formatLookup.Add(typeof(MyObjectBuilder_MotorStator), Format_Rotor);
            formatLookup.Add(typeof(MyObjectBuilder_MotorAdvancedStator), Format_Rotor);
            formatLookup.Add(typeof(MyObjectBuilder_MotorSuspension), Format_Rotor);

            formatLookup.Add(typeof(MyObjectBuilder_MergeBlock), Format_MergeBlock);

            formatLookup.Add(typeof(MyObjectBuilder_LandingGear), Format_LandingGear);

            formatLookup.Add(typeof(MyObjectBuilder_ShipController), Format_ShipController);
            formatLookup.Add(typeof(MyObjectBuilder_Cockpit), Format_ShipController);
            formatLookup.Add(typeof(MyObjectBuilder_CryoChamber), Format_ShipController);
            formatLookup.Add(typeof(MyObjectBuilder_RemoteControl), Format_ShipController);

            formatLookup.Add(typeof(MyObjectBuilder_Thrust), Format_Thrust);

            formatLookup.Add(typeof(MyObjectBuilder_Gyro), Format_Gyro);

            formatLookup.Add(typeof(MyObjectBuilder_LightingBlock), Format_Light);
            formatLookup.Add(typeof(MyObjectBuilder_InteriorLight), Format_Light);
            formatLookup.Add(typeof(MyObjectBuilder_ReflectorLight), Format_Light);

            formatLookup.Add(typeof(MyObjectBuilder_OreDetector), Format_OreDetector);

            formatLookup.Add(typeof(MyObjectBuilder_ProjectorBase), Format_Projector);
            formatLookup.Add(typeof(MyObjectBuilder_Projector), Format_Projector);

            formatLookup.Add(typeof(MyObjectBuilder_Door), Format_Door);

            formatLookup.Add(typeof(MyObjectBuilder_AirtightDoorGeneric), Format_AirtightDoor);
            formatLookup.Add(typeof(MyObjectBuilder_AirtightHangarDoor), Format_AirtightDoor);
            formatLookup.Add(typeof(MyObjectBuilder_AirtightSlideDoor), Format_AirtightDoor);

            formatLookup.Add(typeof(MyObjectBuilder_AdvancedDoor), Format_AdvancedDoor);

            formatLookup.Add(typeof(MyObjectBuilder_Parachute), Format_Parachute);

            formatLookup.Add(typeof(MyObjectBuilder_MedicalRoom), Format_MedicalRoom);

            formatLookup.Add(typeof(MyObjectBuilder_ProductionBlock), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_Refinery), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_Assembler), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_GasTank), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_OxygenTank), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_OxygenGenerator), Format_Production);

            formatLookup.Add(typeof(MyObjectBuilder_OxygenFarm), Format_OxygenFarm);

            formatLookup.Add(typeof(MyObjectBuilder_AirVent), Format_AirVent);
            formatLookup.Add(typeof(MyObjectBuilder_UpgradeModule), Format_UpgradeModule);

            formatLookup.Add(typeof(MyObjectBuilder_Reactor), Format_PowerProducer);
            formatLookup.Add(typeof(MyObjectBuilder_HydrogenEngine), Format_PowerProducer);
            formatLookup.Add(typeof(MyObjectBuilder_BatteryBlock), Format_PowerProducer);
            formatLookup.Add(typeof(MyObjectBuilder_SolarPanel), Format_PowerProducer);
            //formatLookup.Add(typeof(MyObjectBuilder_WindTurbine), Format_PowerProducer);
            // DEBUG ^^^ when whitelisting is fixed

            formatLookup.Add(typeof(MyObjectBuilder_RadioAntenna), Format_RadioAntenna);

            formatLookup.Add(typeof(MyObjectBuilder_LaserAntenna), Format_LaserAntenna);

            formatLookup.Add(typeof(MyObjectBuilder_Beacon), Format_Beacon);

            formatLookup.Add(typeof(MyObjectBuilder_TimerBlock), Format_Timer);

            formatLookup.Add(typeof(MyObjectBuilder_MyProgrammableBlock), Format_ProgrammableBlock);

            formatLookup.Add(typeof(MyObjectBuilder_TextPanel), Format_LCD);

            formatLookup.Add(typeof(MyObjectBuilder_SoundBlock), Format_SoundBlock);

            formatLookup.Add(typeof(MyObjectBuilder_SensorBlock), Format_Sensor);

            formatLookup.Add(typeof(MyObjectBuilder_CameraBlock), Format_Camera);

            formatLookup.Add(typeof(MyObjectBuilder_ButtonPanel), Format_Button);

            formatLookup.Add(typeof(MyObjectBuilder_GravityGeneratorBase), Format_GravityGenerator);
            formatLookup.Add(typeof(MyObjectBuilder_GravityGenerator), Format_GravityGenerator);
            formatLookup.Add(typeof(MyObjectBuilder_GravityGeneratorSphere), Format_GravityGenerator);

            formatLookup.Add(typeof(MyObjectBuilder_VirtualMass), Format_ArtificialMass);

            formatLookup.Add(typeof(MyObjectBuilder_SpaceBall), Format_SpaceBall);

            formatLookup.Add(typeof(MyObjectBuilder_JumpDrive), Format_JumpDrive);

            formatLookup.Add(typeof(MyObjectBuilder_ConveyorTurretBase), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_UserControllableGun), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_LargeGatlingTurret), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_LargeMissileTurret), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_InteriorTurret), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_SmallGatlingGun), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_SmallMissileLauncher), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_SmallMissileLauncherReload), Format_Weapon);

            formatLookup.Add(typeof(MyObjectBuilder_Warhead), Format_Warhead);
        }

        private void Format_TerminalBlock(MyCubeBlockDefinition def)
        {
            // HACK hardcoded; control panel doesn't use power
            PowerRequired(0, null, powerHardcoded: true);
        }

        #region Conveyors
        private void Format_Conveyors(MyCubeBlockDefinition def)
        {
            PowerRequired(GameData.Hardcoded.Conveyors_PowerReq, GameData.Hardcoded.Conveyors_PowerGroup, powerHardcoded: true, groupHardcoded: true);
        }

        private void Format_Connector(MyCubeBlockDefinition def)
        {
            PowerRequired(GameData.Hardcoded.ShipConnector_PowerReq(def), GameData.Hardcoded.ShipConnector_PowerGroup, powerHardcoded: true, groupHardcoded: true);

            InventoryStats(def, 0, GameData.Hardcoded.ShipConnector_InventoryVolume(def));

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                var data = BData_Base.TryGetDataCached<BData_Connector>(def);

                if(data != null)
                {
                    if(data.Connector)
                        AddLine().Append("Connectable: Yes");
                    else
                        AddLine().Color(COLOR_WARNING).Append("Connectable: No").ResetColor();

                    GetLine().Separator().LabelHardcoded("Can throw contents").Append("Yes").EndLine();
                }
            }
        }

        private void Format_CargoAndCollector(MyCubeBlockDefinition def)
        {
            var cargo = (MyCargoContainerDefinition)def;

            var poweredCargo = def as MyPoweredCargoContainerDefinition; // collector
            if(poweredCargo != null)
            {
                PowerRequired(poweredCargo.RequiredPowerInput, poweredCargo.ResourceSinkGroup);
            }

            InventoryStats(def, cargo.InventorySize.Volume, GameData.Hardcoded.CargoContainer_InventoryVolume(def));
        }

        private void Format_ConveyorSorter(MyCubeBlockDefinition def)
        {
            var sorter = (MyConveyorSorterDefinition)def; // does not extend MyPoweredCargoContainerDefinition

            PowerRequired(sorter.PowerInput, sorter.ResourceSinkGroup);

            InventoryStats(def, sorter.InventorySize.Volume, 0);
        }
        #endregion

        private void Format_Piston(MyCubeBlockDefinition def)
        {
            var piston = (MyPistonBaseDefinition)def;

            PowerRequired(piston.RequiredPowerInput, piston.ResourceSinkGroup);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Extended length").DistanceFormat(piston.Maximum).Separator().Label("Max velocity").DistanceFormat(piston.MaxVelocity).EndLine();
            }

            Suffix_Mechanical(def, piston.TopPart);
        }

        private void Format_Rotor(MyCubeBlockDefinition def)
        {
            var motor = (MyMotorStatorDefinition)def;

            PowerRequired(motor.RequiredPowerInput, motor.ResourceSinkGroup);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                var suspension = def as MyMotorSuspensionDefinition;

                if(suspension != null)
                {
                    AddLine().Label("Max torque").TorqueFormat(suspension.PropulsionForce).Separator().Append("Axle Friction: ").TorqueFormat(suspension.AxleFriction).EndLine();
                    AddLine().Label("Steering - Max angle").AngleFormat(suspension.MaxSteer).Separator().Append("Speed base: ").RotationSpeed(suspension.SteeringSpeed * 60).EndLine();
                    AddLine().Label("Ride height").DistanceFormat(suspension.MinHeight).Append(" to ").DistanceFormat(suspension.MaxHeight).EndLine();
                }
                else
                {
                    AddLine().Label("Max torque").TorqueFormat(motor.MaxForceMagnitude).EndLine();

                    if(motor.RotorDisplacementMin < motor.RotorDisplacementMax)
                    {
                        AddLine().Label("Displacement Large Top").DistanceFormat(motor.RotorDisplacementMin).Append(" to ").DistanceFormat(motor.RotorDisplacementMax).EndLine();
                    }

                    if(motor.RotorDisplacementMinSmall < motor.RotorDisplacementMaxSmall)
                    {
                        AddLine().Label("Displacement Small Top").DistanceFormat(motor.RotorDisplacementMinSmall).Append(" to ").DistanceFormat(motor.RotorDisplacementMaxSmall).EndLine();
                    }
                }
            }

            Suffix_Mechanical(def, motor.TopPart);
        }

        private void Suffix_Mechanical(MyCubeBlockDefinition def, string topPart)
        {
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PartStats))
            {
                var group = MyDefinitionManager.Static.TryGetDefinitionGroup(topPart);

                if(group == null)
                    return;

                var partDef = (def.CubeSize == MyCubeSize.Large ? group.Large : group.Small);

                AppendBasics(partDef, part: true);
            }
        }

        private void Format_MergeBlock(MyCubeBlockDefinition def)
        {
            var merge = (MyMergeBlockDefinition)def;

            // HACK hardcoded; MergeBlock doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Pull strength").AppendFormat("{0:###,###,##0.#######}", merge.Strength).EndLine();
            }
        }

        private void Format_LandingGear(MyCubeBlockDefinition def)
        {
            var lg = (MyLandingGearDefinition)def;

            // HACK: hardcoded; LG doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max differential velocity for locking").SpeedFormat(lg.MaxLockSeparatingVelocity).EndLine();
            }
        }

        #region Ship tools
        private void Format_Drill(MyCubeBlockDefinition def)
        {
            var shipDrill = (MyShipDrillDefinition)def;

            PowerRequired(GameData.Hardcoded.ShipDrill_Power, shipDrill.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.InventoryStats))
            {
                float volume;
                if(GetInventoryFromComponent(def, out volume))
                    AddLine().Label("Inventory").InventoryFormat(volume, GameData.Hardcoded.ShipDrill_InventoryConstraint).EndLine();
                else
                    AddLine().LabelHardcoded("Inventory").InventoryFormat(GameData.Hardcoded.ShipDrill_InventoryVolume(def), GameData.Hardcoded.ShipDrill_InventoryConstraint).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Mining radius").DistanceFormat(shipDrill.SensorRadius).EndLine();
                AddLine().Label("Cutout radius").DistanceFormat(shipDrill.CutOutRadius).EndLine();
            }
        }

        private void Format_WelderAndGrinder(MyCubeBlockDefinition def)
        {
            var shipTool = (MyShipToolDefinition)def;
            var isWelder = def is MyShipWelderDefinition;

            PowerRequired(GameData.Hardcoded.ShipTool_PowerReq, GameData.Hardcoded.ShipTool_PowerGroup, powerHardcoded: true, groupHardcoded: true);

            InventoryStats(def, 0, GameData.Hardcoded.ShipTool_InventoryVolume(def));

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                if(isWelder)
                {
                    float weld = GameData.Hardcoded.ShipWelder_WeldPerSecond;
                    var mul = MyAPIGateway.Session.WelderSpeedMultiplier;
                    AddLine().LabelHardcoded("Weld speed").ProportionToPercent(weld).Append(" split accross targets").Color(COLOR_UNIMPORTANT).MultiplierFormat(mul).EndLine();

                    AddLine().Label("Welding radius").DistanceFormat(shipTool.SensorRadius).EndLine();
                }
                else
                {
                    float grind = GameData.Hardcoded.ShipGrinder_GrindPerSecond;
                    var mul = MyAPIGateway.Session.GrinderSpeedMultiplier;
                    AddLine().LabelHardcoded("Grind speed").ProportionToPercent(grind * mul).Append(" split accross targets").Color(COLOR_UNIMPORTANT).MultiplierFormat(mul).EndLine();

                    AddLine().Label("Grinding radius").DistanceFormat(shipTool.SensorRadius).EndLine();
                }
            }
        }
        #endregion

        private void Format_ShipController(MyCubeBlockDefinition def)
        {
            var shipController = (MyShipControllerDefinition)def;

            var rc = def as MyRemoteControlDefinition;
            if(rc != null)
            {
                PowerRequired(rc.RequiredPowerInput, rc.ResourceSinkGroup);
            }

            var cryo = def as MyCryoChamberDefinition;
            if(cryo != null)
            {
                PowerRequired(cryo.IdlePowerConsumption, cryo.ResourceSinkGroup);
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Append("Abilities: ");

                var preLen = GetLine().Length;

                if(shipController.EnableShipControl)
                    GetLine().Append("Ship control, ");

                if(shipController.EnableBuilderCockpit)
                    GetLine().Append("Place blocks, ");

                if(!shipController.EnableFirstPerson)
                    GetLine().Append("3rd person view only, ");

                if(preLen == GetLine().Length)
                {
                    GetLine().Append("None.");
                }
                else
                {
                    GetLine().Length -= 2; // remove last comma
                    GetLine().Append(".");
                }

                //AddLine((shipController.EnableShipControl ? MyFontEnum.Green : MyFontEnum.Red)).Append("Ship controls: ").Append(shipController.EnableShipControl ? "Yes" : "No").EndLine();
                //AddLine((shipController.EnableFirstPerson ? MyFontEnum.Green : MyFontEnum.Red)).Append("First person view: ").Append(shipController.EnableFirstPerson ? "Yes" : "No").EndLine();
                //AddLine((shipController.EnableBuilderCockpit ? MyFontEnum.Green : MyFontEnum.Red)).Append("Can build: ").Append(shipController.EnableBuilderCockpit ? "Yes" : "No").EndLine();
            }

            var cockpit = def as MyCockpitDefinition;
            if(cockpit != null)
            {
                InventoryStats(def, 0, GameData.Hardcoded.Cockpit_InventoryVolume);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
                {
                    AddLine((cockpit.IsPressurized ? MyFontEnum.Green : MyFontEnum.Red))
                       .Color(cockpit.IsPressurized ? COLOR_GOOD : COLOR_WARNING)
                       .Label("Pressurized");

                    if(cockpit.IsPressurized)
                        GetLine().Append("Yes, Oxygen capacity: ").VolumeFormat(cockpit.OxygenCapacity);
                    else
                        GetLine().Append("No");

                    if(cockpit.HUD != null)
                    {
                        MyDefinitionBase defHUD;
                        if(MyDefinitionManager.Static.TryGetDefinition(new MyDefinitionId(typeof(MyObjectBuilder_HudDefinition), cockpit.HUD), out defHUD))
                        {
                            // HACK MyHudDefinition is not whitelisted; also GetObjectBuilder() is useless because it doesn't get filled in
                            //var hudDefObj = (MyObjectBuilder_HudDefinition)defBase.GetObjectBuilder();
                            AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("Custom HUD: ").Append(cockpit.HUD).ResetColor().Separator().Color(COLOR_MOD).Append("Mod: ").ModFormat(defHUD.Context).EndLine();
                        }
                        else
                        {
                            AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Custom HUD: ").Append(cockpit.HUD).Append("  (Error: not found)").EndLine();
                        }
                    }
                }
            }
        }

        private void Format_Thrust(MyCubeBlockDefinition def)
        {
            var thrust = (MyThrustDefinition)def;

            if(!thrust.FuelConverter.FuelId.IsNull())
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
                {
                    AddLine().Append("Requires power to be controlled");

                    if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(thrust.ResourceSinkGroup);
                }

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ItemInputs))
                {
                    AddLine().Append("Requires fuel: ").Append(thrust.FuelConverter.FuelId.SubtypeId).Separator().Append("Efficiency: ").Number(thrust.FuelConverter.Efficiency * 100).Append("%").EndLine();
                }
            }
            else
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
                {
                    AddLine().Append("Power: ").PowerFormat(thrust.MaxPowerConsumption).Separator().Append("Idle: ").PowerFormat(thrust.MinPowerConsumption);

                    if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(thrust.ResourceSinkGroup);
                }
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
            {
                AddLine().Append("Force: ").ForceFormat(thrust.ForceMagnitude).Separator().Append("Dampener factor: ").RoundedNumber(thrust.SlowdownFactor, 2).EndLine();

                if(thrust.EffectivenessAtMinInfluence < 1.0f || thrust.EffectivenessAtMaxInfluence < 1.0f)
                {
                    // HACK thrust.NeedsAtmosphereForInfluence seems to be a pointless var, planetary influence is always considered atmosphere.

                    AddLine(thrust.EffectivenessAtMaxInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White).Color(thrust.EffectivenessAtMaxInfluence < 1f ? COLOR_BAD : COLOR_GOOD)
                        .ProportionToPercent(thrust.EffectivenessAtMaxInfluence).Append(" max thrust ");
                    if(thrust.MaxPlanetaryInfluence < 1f)
                        GetLine().Append("in ").ProportionToPercent(thrust.MaxPlanetaryInfluence).Append(" atmosphere");
                    else
                        GetLine().Append("in atmosphere");
                    GetLine().EndLine();

                    AddLine(thrust.EffectivenessAtMinInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White).Color(thrust.EffectivenessAtMinInfluence < 1f ? COLOR_BAD : COLOR_GOOD)
                                    .ProportionToPercent(thrust.EffectivenessAtMinInfluence).Append(" max thrust ");
                    if(thrust.MinPlanetaryInfluence > 0f)
                        GetLine().Append("below ").ProportionToPercent(thrust.MinPlanetaryInfluence).Append(" atmosphere");
                    else
                        GetLine().Append("in space");
                    GetLine().EndLine();
                }
                else
                {
                    AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("No thrust limits in space or planets").EndLine();
                }
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                if(thrust.ConsumptionFactorPerG > 0)
                    AddLine(MyFontEnum.Red).Append("Extra consumption: +").ProportionToPercent(thrust.ConsumptionFactorPerG).Append(" per natural g acceleration").EndLine();

                var data = BData_Base.TryGetDataCached<BData_Thrust>(def);

                if(data != null)
                {
                    AddLine().Append("Flames: ").Append(data.Flames.Count).Separator().Append("Max distance: ").DistanceFormat(data.HighestLength, 2).EndLine();
                    AddLine().Append("Ship damage: ").Number(data.TotalBlockDamage).Append("/s").Separator().Append("Other damage:").Number(data.TotalOtherDamage).Append("/s").EndLine();
                }
            }
        }

        private void Format_Gyro(MyCubeBlockDefinition def)
        {
            var gyro = (MyGyroDefinition)def;

            PowerRequired(gyro.RequiredPowerInput, gyro.ResourceSinkGroup);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Force").ForceFormat(gyro.ForceMagnitude).EndLine();
            }
        }

        private void Format_Light(MyCubeBlockDefinition def)
        {
            var light = (MyLightingBlockDefinition)def;

            var radius = light.LightRadius;
            var isSpotlight = (def is MyReflectorBlockDefinition);

            if(isSpotlight)
                radius = light.LightReflectorRadius;

            PowerRequired(light.RequiredPowerInput, light.ResourceSinkGroup);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Append("Radius: ").DistanceFormat(radius.Min).Append(" to ").DistanceFormat(radius.Max).Separator().Append("Default: ").DistanceFormat(radius.Default).EndLine();
                AddLine().Append("Intensity: ").RoundedNumber(light.LightIntensity.Min, 2).Append(" to ").RoundedNumber(light.LightIntensity.Max, 2).Separator().Append("Default: ").RoundedNumber(light.LightIntensity.Default, 2).EndLine();
                AddLine().Append("Falloff: ").RoundedNumber(light.LightFalloff.Min, 2).Append(" to ").RoundedNumber(light.LightFalloff.Max, 2).Separator().Append("Default: ").RoundedNumber(light.LightFalloff.Default, 2).EndLine();
            }
        }

        private void Format_OreDetector(MyCubeBlockDefinition def)
        {
            var oreDetector = (MyOreDetectorDefinition)def;

            PowerRequired(GameData.Hardcoded.OreDetector_PowerReq, oreDetector.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max range").DistanceFormat(oreDetector.MaximumRange).EndLine();
            }
        }

        private void Format_Projector(MyCubeBlockDefinition def)
        {
            var projector = (MyProjectorDefinition)def;

            PowerRequired(projector.RequiredPowerInput, projector.ResourceSinkGroup);
        }

        #region Doors
        private void Format_Door(MyCubeBlockDefinition def)
        {
            var door = (MyDoorDefinition)def;

            PowerRequired(GameData.Hardcoded.Door_PowerReq, door.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                float moveTime = GameData.Hardcoded.Door_MoveSpeed(door.OpeningSpeed);
                AddLine().Label("Move time").TimeFormat(moveTime).Separator().Label("Distance").DistanceFormat(door.MaxOpen).EndLine();
            }
        }

        private void Format_AirtightDoor(MyCubeBlockDefinition def)
        {
            var airTightDoor = (MyAirtightDoorGenericDefinition)def; // does not extend MyDoorDefinition

            // MyAirtightHangarDoorDefinition and MyAirtightSlideDoorDefinition are empty

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
            {
                AddLine().Label("Power").PowerFormat(airTightDoor.PowerConsumptionMoving).Separator().Label("Idle").PowerFormat(airTightDoor.PowerConsumptionIdle);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(airTightDoor.ResourceSinkGroup).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                float moveTime = GameData.Hardcoded.Door_MoveSpeed(airTightDoor.OpeningSpeed);
                AddLine().Label("Move time").TimeFormat(moveTime).EndLine();
            }
        }

        private void Format_AdvancedDoor(MyCubeBlockDefinition def)
        {
            var advDoor = (MyAdvancedDoorDefinition)def; // does not extend MyDoorDefinition

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Moving").PowerFormat(advDoor.PowerConsumptionMoving).Separator().Label("Idle").PowerFormat(advDoor.PowerConsumptionIdle);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(advDoor.ResourceSinkGroup).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                float openTime, closeTime;
                GameData.Hardcoded.AdvDoor_MoveSpeed(advDoor, out openTime, out closeTime);

                AddLine().Label("Move time - Opening").TimeFormat(openTime).Separator().Label("Closing").TimeFormat(closeTime).EndLine();
            }
        }
        #endregion

        private void Format_Parachute(MyCubeBlockDefinition def)
        {
            var parachute = (MyParachuteDefinition)def;

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Deploy").PowerFormat(parachute.PowerConsumptionMoving).Separator().Label("Idle").PowerFormat(parachute.PowerConsumptionIdle);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(parachute.ResourceSinkGroup).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo) || Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ItemInputs))
            {
                const float TARGET_DESCEND_VELOCITY = 10;
                float maxMass, disreefAtmosphere;
                GameData.Hardcoded.Parachute_GetDetails(parachute, TARGET_DESCEND_VELOCITY, out maxMass, out disreefAtmosphere);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ItemInputs))
                {
                    AddLine().Label("Required item to deploy").Append(parachute.MaterialDeployCost).Append("x ").IdTypeSubtypeFormat(parachute.MaterialDefinitionId).EndLine();
                }

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
                {
                    AddLine().Label("Required atmosphere - Minimum").Number(parachute.MinimumAtmosphereLevel).Separator().Label("Fully open").Number(disreefAtmosphere).EndLine();
                    AddLine().Label("Drag coefficient").AppendFormat("{0:0.0####}", parachute.DragCoefficient).EndLine();
                    AddLine().Label("Load estimate").Color(COLOR_INFO).MassFormat(maxMass).ResetColor().Append(" falling at ").SpeedFormat(TARGET_DESCEND_VELOCITY).Append(" in ").AccelerationFormat(GameData.Hardcoded.GAME_EARTH_GRAVITY).Append(" and 1.0 air density.").EndLine();
                }
            }
        }

        private void Format_MedicalRoom(MyCubeBlockDefinition def)
        {
            var medicalRoom = (MyMedicalRoomDefinition)def;

            PowerRequired(GameData.Hardcoded.MedicalRoom_PowerReq, medicalRoom.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine(medicalRoom.ForceSuitChangeOnRespawn ? MyFontEnum.Blue : (!medicalRoom.RespawnAllowed ? MyFontEnum.Red : MyFontEnum.White))
                    .Color(medicalRoom.ForceSuitChangeOnRespawn ? COLOR_WARNING : COLOR_NORMAL)
                    .Append("Respawn: ").BoolFormat(medicalRoom.RespawnAllowed).ResetColor().Separator();

                if(medicalRoom.RespawnAllowed && medicalRoom.ForceSuitChangeOnRespawn)
                {
                    GetLine().Color(COLOR_WARNING).Label("Forced suit");

                    if(string.IsNullOrEmpty(medicalRoom.RespawnSuitName))
                    {
                        GetLine().Color(COLOR_BAD).Append("(Error: empty)");
                    }
                    else
                    {
                        MyCharacterDefinition charDef;
                        if(MyDefinitionManager.Static.Characters.TryGetValue(medicalRoom.RespawnSuitName, out charDef))
                            GetLine().Append(charDef.Name);
                        else
                            GetLine().Append(medicalRoom.RespawnSuitName).Color(COLOR_BAD).Append(" (Error: not found)");
                    }
                }
                else
                    GetLine().Append("Forced suit: No");

                GetLine().EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
            {
                // DEBUG TODO make these as "abilities" like in cockpit
                // DEBUG TODO energy recharge is x5, need exact charge rate though... also heal rate
                AddLine(medicalRoom.HealingAllowed ? MyFontEnum.White : MyFontEnum.Red)
                    .Color(medicalRoom.HealingAllowed ? COLOR_NORMAL : COLOR_WARNING)
                    .Label("Healing").BoolFormat(medicalRoom.HealingAllowed).EndLine();

                AddLine(medicalRoom.RefuelAllowed ? MyFontEnum.White : MyFontEnum.Red)
                    .Color(medicalRoom.RefuelAllowed ? COLOR_NORMAL : COLOR_WARNING)
                    .Label("Recharge").BoolFormat(medicalRoom.RefuelAllowed).EndLine();

                AddLine(medicalRoom.SuitChangeAllowed ? MyFontEnum.White : MyFontEnum.Red)
                    .Color(medicalRoom.SuitChangeAllowed ? COLOR_NORMAL : COLOR_WARNING)
                    .Label("Suit change").BoolFormat(medicalRoom.SuitChangeAllowed).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                if(medicalRoom.CustomWardrobesEnabled && medicalRoom.CustomWardrobeNames != null && medicalRoom.CustomWardrobeNames.Count > 0)
                {
                    AddLine(MyFontEnum.Blue).Color(COLOR_WARNING).Append("Suits:");

                    foreach(var charName in medicalRoom.CustomWardrobeNames)
                    {
                        MyCharacterDefinition charDef;
                        if(!MyDefinitionManager.Static.Characters.TryGetValue(charName, out charDef))
                            AddLine(MyFontEnum.Red).Append("    ").Append(charName).Color(COLOR_BAD).Append(" (not found in definitions)").EndLine();
                        else
                            AddLine().Append("    ").Append(charDef.DisplayNameText).EndLine();
                    }
                }
                else
                    AddLine().Append("Usable suits: (all)").EndLine();
            }
        }

        #region Production
        private void Format_Production(MyCubeBlockDefinition def)
        {
            var production = (MyProductionBlockDefinition)def;

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
            {
                AddLine().Append("Power: ").PowerFormat(production.OperationalPowerConsumption).Separator().Append("Idle: ").PowerFormat(production.StandbyPowerConsumption);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(production.ResourceSinkGroup).EndLine();
            }

            var assembler = def as MyAssemblerDefinition;
            if(assembler != null)
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
                {
                    var mulSpeed = MyAPIGateway.Session.AssemblerSpeedMultiplier;
                    var mulEff = MyAPIGateway.Session.AssemblerEfficiencyMultiplier;

                    AddLine().Append("Assembly speed: ").ProportionToPercent(assembler.AssemblySpeed * mulSpeed).Color(COLOR_UNIMPORTANT).MultiplierFormat(mulSpeed).ResetColor().Separator().Append("Efficiency: ").ProportionToPercent(mulEff).MultiplierFormat(mulEff).EndLine();
                }

                var survivalKit = def as MySurvivalKitDefinition;

                if(survivalKit != null)
                {
                    if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
                    {
                        // DEBUG TODO energy recharge is x1, need exact charge rate though... also heal rate
                        AddLine().LabelHardcoded("Healing").BoolFormat(true).EndLine();
                        AddLine().LabelHardcoded("Recharge").BoolFormat(true).EndLine();
                    }
                }
            }

            var refinery = def as MyRefineryDefinition;
            if(refinery != null)
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
                {
                    var mul = MyAPIGateway.Session.RefinerySpeedMultiplier;

                    AddLine().Append("Refine speed: ").ProportionToPercent(refinery.RefineSpeed * mul).Color(COLOR_UNIMPORTANT).MultiplierFormat(mul).ResetColor().Separator().Append("Efficiency: ").ProportionToPercent(refinery.MaterialEfficiency).EndLine();
                }
            }

            var gasTank = def as MyGasTankDefinition;
            if(gasTank != null)
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
                {
                    AddLine().Append("Stores: ").Append(gasTank.StoredGasId.SubtypeName).Separator().Append("Capacity: ").VolumeFormat(gasTank.Capacity).EndLine();
                }
            }

            var oxygenGenerator = def as MyOxygenGeneratorDefinition;
            if(oxygenGenerator != null)
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
                {
                    AddLine().Append("Ice consumption: ").MassFormat(oxygenGenerator.IceConsumptionPerSecond).Append("/s").EndLine();

                    if(oxygenGenerator.ProducedGases.Count > 0)
                    {
                        AddLine().Append("Produces: ");

                        foreach(var gas in oxygenGenerator.ProducedGases)
                        {
                            GetLine().Append(gas.Id.SubtypeName).Append(" (").VolumeFormat(oxygenGenerator.IceConsumptionPerSecond * gas.IceToGasRatio).Append("/s), ");
                        }

                        GetLine().Length -= 2;
                        GetLine().EndLine();
                    }
                    else
                    {
                        AddLine(MyFontEnum.Red).Append("Produces: <N/A>").EndLine();
                    }
                }
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.InventoryStats))
            {
                var volume = (production.InventoryMaxVolume > 0 ? production.InventoryMaxVolume : production.InventorySize.Volume);

                if(refinery != null || assembler != null)
                {
                    AddLine().Append("In+out inventories: ").InventoryFormat(volume * 2, production.InputInventoryConstraint, production.OutputInventoryConstraint).EndLine();
                }
                else
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume, production.InputInventoryConstraint).EndLine();
                }
            }

            if(production.BlueprintClasses != null && Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
            {
                if(production.BlueprintClasses.Count == 0)
                {
                    AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Has no blueprint classes.").EndLine();
                }
                else
                {
                    AddLine();

                    if(refinery != null)
                        GetLine().Append("Refines: ");
                    else if(gasTank != null)
                        GetLine().Append("Refills: ");
                    else if(assembler != null)
                        GetLine().Append("Builds: ");
                    else if(oxygenGenerator != null)
                        GetLine().Append("Generates: ");
                    else
                        GetLine().Append("Blueprints: ");

                    foreach(var bp in production.BlueprintClasses)
                    {
                        var name = bp.DisplayNameText;
                        var newLineIndex = name.IndexOf('\n');

                        if(newLineIndex != -1) // name contains a new line, ignore everything after that
                        {
                            for(int i = 0; i < newLineIndex; ++i)
                            {
                                GetLine().Append(name[i]);
                            }

                            GetLine().TrimEndWhitespace();
                        }
                        else
                        {
                            GetLine().Append(name);
                        }

                        GetLine().Append(", ");
                    }

                    GetLine().Length -= 2;
                    GetLine().EndLine();
                }
            }
        }

        private void Format_OxygenFarm(MyCubeBlockDefinition def)
        {
            var oxygenFarm = (MyOxygenFarmDefinition)def; // does not extend MyProductionBlockDefinition

            PowerRequired(oxygenFarm.OperationalPowerConsumption, oxygenFarm.ResourceSinkGroup);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
            {
                AddLine().Label("Produces").RoundedNumber(oxygenFarm.MaxGasOutput, 2).Append(" ").Append(oxygenFarm.ProducedGas.SubtypeName).Append(" l/s");

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(oxygenFarm.ResourceSourceGroup).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine(oxygenFarm.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red).Append(oxygenFarm.IsTwoSided ? "Two-sided" : "One-sided").EndLine();
            }
        }

        private void Format_AirVent(MyCubeBlockDefinition def)
        {
            var vent = (MyAirVentDefinition)def; // does not extend MyProductionBlockDefinition

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Idle").PowerFormat(vent.StandbyPowerConsumption).Separator().Label("Operational").PowerFormat(vent.OperationalPowerConsumption);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(vent.ResourceSinkGroup).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
            {
                AddLine().Label("Output - Rate").VolumeFormat(vent.VentilationCapacityPerSecond).Append("/s");

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(vent.ResourceSourceGroup).EndLine();
            }
        }

        private void Format_UpgradeModule(MyCubeBlockDefinition def)
        {
            var upgradeModule = (MyUpgradeModuleDefinition)def;

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                if(upgradeModule.Upgrades == null || upgradeModule.Upgrades.Length == 0)
                {
                    AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Upgrades: N/A").EndLine();
                }
                else
                {
                    AddLine().Append("Upgrades per slot:").EndLine();

                    foreach(var upgrade in upgradeModule.Upgrades)
                    {
                        AddLine().Append("    - ").AppendUpgrade(upgrade).EndLine();
                    }
                }
            }
        }

        private void Format_PowerProducer(MyCubeBlockDefinition def)
        {
            var powerProducer = (MyPowerProducerDefinition)def;

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
            {
                AddLine().Append("Power output: ").PowerFormat(powerProducer.MaxPowerOutput);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(powerProducer.ResourceSourceGroup).EndLine();
            }

            var h2Engine = def as MyHydrogenEngineDefinition;
            if(h2Engine != null)
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ItemInputs))
                {
                    AddLine().Label("Needs fuel").IdTypeSubtypeFormat(h2Engine.Fuel.FuelId);
                    AddLine().Label("Consumption").VolumeFormat(h2Engine.MaxPowerOutput / h2Engine.FuelProductionToCapacityMultiplier).Append("/s");

                    if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(h2Engine.ResourceSinkGroup).EndLine();

                    AddLine().Label("Fuel capacity").VolumeFormat(h2Engine.FuelCapacity).EndLine();
                }

                return;
            }

            var reactor = def as MyReactorDefinition;
            if(reactor != null)
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ItemInputs))
                {
                    if(reactor.FuelInfos != null && reactor.FuelInfos.Length > 0)
                    {
                        bool hasOneFuel = (reactor.FuelInfos.Length == 1);

                        if(hasOneFuel)
                            AddLine().Append("Needs fuel: ");
                        else
                            AddLine().Color(COLOR_WARNING).Append("Needs combined fuels:").ResetColor().EndLine();

                        foreach(var fuel in reactor.FuelInfos)
                        {
                            if(!hasOneFuel)
                                AddLine().Append("       - ");

                            GetLine().IdTypeSubtypeFormat(fuel.FuelId).Append(" (").RoundedNumber(fuel.ConsumptionPerSecond_Items, 5).Append("/s)").EndLine();
                        }
                    }
                }

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.InventoryStats))
                {
                    var volume = (reactor.InventoryMaxVolume > 0 ? reactor.InventoryMaxVolume : reactor.InventorySize.Volume);
                    var invLimit = reactor.InventoryConstraint;

                    if(invLimit != null)
                    {
                        AddLine().Append("Inventory: ").InventoryFormat(volume, reactor.InventoryConstraint).EndLine();

                        if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.InventoryExtras))
                        {
                            AddLine(MyFontEnum.Blue).Color(COLOR_WARNING).Append("Inventory items ").Append(invLimit.IsWhitelist ? "allowed" : "NOT allowed").Append(":").EndLine();

                            foreach(var id in invLimit.ConstrainedIds)
                            {
                                AddLine().Append("       - ").IdTypeSubtypeFormat(id).EndLine();
                            }

                            foreach(var type in invLimit.ConstrainedTypes)
                            {
                                AddLine().Append("       - All of type: ").IdTypeFormat(type).EndLine();
                            }
                        }
                    }
                    else
                    {
                        AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                    }
                }

                return;
            }

            var battery = def as MyBatteryBlockDefinition;
            if(battery != null)
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
                {
                    AddLine(battery.AdaptibleInput ? MyFontEnum.White : MyFontEnum.Red).Append("Power input: ").PowerFormat(battery.RequiredPowerInput).Append(battery.AdaptibleInput ? " (adaptable)" : " (minimum required)");

                    if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(battery.ResourceSinkGroup).EndLine();
                }

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.InventoryStats))
                {
                    AddLine().Append("Power capacity: ").PowerStorageFormat(battery.MaxStoredPower).Separator().Append("Pre-charged: ").PowerStorageFormat(battery.MaxStoredPower * battery.InitialStoredPowerRatio).Append(" (").ProportionToPercent(battery.InitialStoredPowerRatio).Append(')').EndLine();
                }

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
                {
                    AddLine().Append("Discharge time: ").TimeFormat((battery.MaxStoredPower / battery.MaxPowerOutput) * 3600f).Separator().Append("Recharge time: ").TimeFormat((battery.MaxStoredPower / battery.RequiredPowerInput) * 3600f);
                }

                return;
            }

            var solarPanel = def as MySolarPanelDefinition;
            if(solarPanel != null)
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
                {
                    AddLine(solarPanel.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red).Append(solarPanel.IsTwoSided ? "Two-sided" : "One-sided").EndLine();
                }

                return;
            }

            var windTurbine = def as MyWindTurbineDefinition;
            if(windTurbine != null)
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
                {
                    AddLine().Label("Clearence - Ground").DistanceFormat(windTurbine.OptimalGroundClearance).Separator().Label("Sides").DistanceFormat(windTurbine.RaycasterSize).EndLine();
                    AddLine().Label("Optimal wind speed").RoundedNumber(windTurbine.OptimalWindSpeed, 2).EndLine();
                    // TODO wind speed unit?
                }

                return;
            }
        }
        #endregion

        #region Communication
        private void Format_RadioAntenna(MyCubeBlockDefinition def)
        {
            var radioAntenna = (MyRadioAntennaDefinition)def;

            PowerRequired(GameData.Hardcoded.RadioAntenna_PowerReq(radioAntenna.MaxBroadcastRadius), radioAntenna.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max radius").DistanceFormat(radioAntenna.MaxBroadcastRadius).EndLine();
            }
        }

        private void Format_LaserAntenna(MyCubeBlockDefinition def)
        {
            var laserAntenna = (MyLaserAntennaDefinition)def;

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Active[1]").PowerFormat(GameData.Hardcoded.LaserAntenna_PowerUsage(laserAntenna, 1000)).Append(" per km ").Color(COLOR_UNIMPORTANT).Append("(/buildinfo help)").EndLine();
                AddLine().Label("Power - Turning").PowerFormat(laserAntenna.PowerInputTurning).Separator().Label("Idle").PowerFormat(laserAntenna.PowerInputIdle);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(laserAntenna.ResourceSinkGroup).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine(laserAntenna.RequireLineOfSight ? MyFontEnum.White : MyFontEnum.Green)
                    .Color(laserAntenna.MaxRange < 0 ? COLOR_GOOD : COLOR_NORMAL).Append("Range: ");

                if(laserAntenna.MaxRange < 0)
                    GetLine().Append("Infinite");
                else
                    GetLine().DistanceFormat(laserAntenna.MaxRange);

                GetLine().ResetColor().Separator().Color(laserAntenna.RequireLineOfSight ? COLOR_WARNING : COLOR_GOOD).Append("Line-of-sight: ").Append(laserAntenna.RequireLineOfSight ? "Required" : "Not required").EndLine();

                AddLine().Label("Rotation Pitch").AngleFormatDeg(laserAntenna.MinElevationDegrees).Append(" to ").AngleFormatDeg(laserAntenna.MaxElevationDegrees).Separator().Label("Yaw").AngleFormatDeg(laserAntenna.MinAzimuthDegrees).Append(" to ").AngleFormatDeg(laserAntenna.MaxAzimuthDegrees).EndLine();
                AddLine().Label("Rotation Speed").RotationSpeed(laserAntenna.RotationRate * GameData.Hardcoded.LaserAntenna_RotationSpeedMul).EndLine();

                // TODO visualize angle limits?
            }
        }

        private void Format_Beacon(MyCubeBlockDefinition def)
        {
            var beacon = (MyBeaconDefinition)def;

            PowerRequired(GameData.Hardcoded.Beacon_PowerReq(beacon.MaxBroadcastRadius), beacon.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max radius").DistanceFormat(beacon.MaxBroadcastRadius).EndLine();
            }
        }
        #endregion

        private void Format_Timer(MyCubeBlockDefinition def)
        {
            var timer = (MyTimerBlockDefinition)def;

            PowerRequired(GameData.Hardcoded.Timer_PowerReq, timer.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Timer range").TimeFormat(timer.MinDelay / 1000f).Append(" to ").TimeFormat(timer.MaxDelay / 1000f).EndLine();
            }
        }

        private void Format_ProgrammableBlock(MyCubeBlockDefinition def)
        {
            var pb = (MyProgrammableBlockDefinition)def;

            PowerRequired(GameData.Hardcoded.ProgrammableBlock_PowerReq, pb.ResourceSinkGroup, powerHardcoded: true);
        }

        private void Format_LCD(MyCubeBlockDefinition def)
        {
            var lcd = (MyTextPanelDefinition)def;

            PowerRequired(lcd.RequiredPowerInput, lcd.ResourceSinkGroup);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Screen resolution").Append(lcd.TextureResolution * lcd.TextureAspectRadio).Append("x").Append(lcd.TextureResolution).EndLine();
                AddLine().Label("Font size limits").RoundedNumber(lcd.MinFontSize, 4).Append(" to ").RoundedNumber(lcd.MaxFontSize, 4).EndLine();
            }
        }

        private void Format_SoundBlock(MyCubeBlockDefinition def)
        {
            var sound = (MySoundBlockDefinition)def;

            PowerRequired(GameData.Hardcoded.SoundBlock_PowerReq, sound.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Range").DistanceRangeFormat(sound.MinRange, sound.MaxRange).EndLine();
                AddLine().Label("Max loop time").TimeFormat(sound.MaxLoopPeriod).EndLine();

                // EmitterNumber and LoopUpdateThreshold seem unused
            }
        }

        private void Format_Sensor(MyCubeBlockDefinition def)
        {
            var sensor = (MySensorBlockDefinition)def;

            var maxField = GameData.Hardcoded.Sensor_MaxField(sensor.MaxRange);

            PowerRequired(GameData.Hardcoded.Sensor_PowerReq(maxField), sensor.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max area").VectorFormat(maxField).EndLine();
            }
        }

        private void Format_Camera(MyCubeBlockDefinition def)
        {
            var camera = (MyCameraBlockDefinition)def;

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Normal use").PowerFormat(camera.RequiredPowerInput).Separator().Label("Raycast charging").PowerFormat(camera.RequiredChargingInput);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(camera.ResourceSinkGroup).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Field of view").AngleFormat(camera.MinFov).Append(" to ").AngleFormat(camera.MaxFov).EndLine();
                AddLine().Label("Raycast - Cone limit").AngleFormatDeg(camera.RaycastConeLimit).Separator().Label("Distance limit");

                if(camera.RaycastDistanceLimit < 0)
                    GetLine().Append("Infinite");
                else
                    GetLine().DistanceFormat((float)camera.RaycastDistanceLimit);

                GetLine().Separator().Label("Time multiplier").RoundedNumber(camera.RaycastTimeMultiplier, 2).EndLine();

                // TODO visualize angle limits?
            }
        }

        private void Format_Button(MyCubeBlockDefinition def)
        {
            var button = (MyButtonPanelDefinition)def;

            PowerRequired(GameData.Hardcoded.ButtonPanel_PowerReq, button.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Button count").Append(button.ButtonCount).EndLine();
            }
        }

        #region Magic blocks
        private void Format_GravityGenerator(MyCubeBlockDefinition def)
        {
            var gravGen = (MyGravityGeneratorBaseDefinition)def;

            var flatGravGen = def as MyGravityGeneratorDefinition;
            if(flatGravGen != null)
            {
                PowerRequired(flatGravGen.RequiredPowerInput, flatGravGen.ResourceSinkGroup);

                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
                {
                    AddLine().Label("Field size").VectorFormat(flatGravGen.MinFieldSize).Append(" to ").VectorFormat(flatGravGen.MaxFieldSize).EndLine();
                }
            }
            else
            {
                var sphereGravGen = def as MyGravityGeneratorSphereDefinition;
                if(sphereGravGen != null)
                {
                    PowerRequired(GameData.Hardcoded.SphericalGravGen_PowerReq(sphereGravGen, sphereGravGen.MaxRadius, sphereGravGen.MaxGravityAcceleration), sphereGravGen.ResourceSinkGroup, powerHardcoded: true);

                    if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
                    {
                        AddLine().Label("Radius").DistanceFormat(sphereGravGen.MinRadius).Append(" to ").DistanceFormat(sphereGravGen.MaxRadius).EndLine();
                    }
                }
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
            {
                AddLine().Label("Acceleration").ForceFormat(gravGen.MinGravityAcceleration).Append(" to ").ForceFormat(gravGen.MaxGravityAcceleration).EndLine();
            }
        }

        private void Format_ArtificialMass(MyCubeBlockDefinition def)
        {
            var artificialMass = (MyVirtualMassDefinition)def;

            PowerRequired(artificialMass.RequiredPowerInput, artificialMass.ResourceSinkGroup);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
            {
                AddLine().Label("Artificial mass").MassFormat(artificialMass.VirtualMass).EndLine();
            }
        }

        private void Format_SpaceBall(MyCubeBlockDefinition def)
        {
            var spaceBall = (MySpaceBallDefinition)def; // this doesn't extend MyVirtualMassDefinition

            // HACK: hardcoded; SpaceBall doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.Production))
            {
                AddLine().Label("Max artificial mass").MassFormat(spaceBall.MaxVirtualMass).EndLine();
            }
        }

        private void Format_JumpDrive(MyCubeBlockDefinition def)
        {
            var jumpDrive = (MyJumpDriveDefinition)def;

            PowerRequired(jumpDrive.RequiredPowerInput, jumpDrive.ResourceSinkGroup);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.InventoryStats))
            {
                AddLine().Label("Stored power for jump").PowerStorageFormat(jumpDrive.PowerNeededForJump).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Jump delay").TimeFormat(jumpDrive.JumpDelay).EndLine();
                AddLine().Label("Max distance").DistanceFormat((float)jumpDrive.MaxJumpDistance).EndLine();
                AddLine().Label("Max mass").MassFormat((float)jumpDrive.MaxJumpMass).EndLine();
            }
        }
        #endregion

        private void Format_Weapon(MyCubeBlockDefinition def)
        {
            var weapon = (MyWeaponBlockDefinition)def;
            var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);

            if(wepDef == null)
            {
                AddLine(MyFontEnum.Red).Color(Color.Red).Append("Block error: can't find weapon definition: ").Append(weapon.WeaponDefinitionId.ToString()).EndLine();
                return;
            }

            var turret = def as MyLargeTurretBaseDefinition;
            float requiredPowerInput = (turret != null ? GameData.Hardcoded.Turret_PowerReq : GameData.Hardcoded.ShipGun_PowerReq);

            PowerRequired(requiredPowerInput, weapon.ResourceSinkGroup, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.InventoryStats))
            {
                AddLine().Label("Inventory").InventoryFormat(weapon.InventoryMaxVolume, wepDef.AmmoMagazinesId).EndLine();
            }

            if(turret != null)
            {
                if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
                {
                    AddLine().Color(turret.AiEnabled ? COLOR_GOOD : COLOR_BAD).Label("Auto-target").BoolFormat(turret.AiEnabled).ResetColor().Append(turret.IdleRotation ? " (With idle rotation)" : "(No idle rotation)").Separator().Color(COLOR_WARNING).Append("Max range: ").DistanceFormat(turret.MaxRangeMeters).EndLine();
                    AddLine().Append("Rotation - ");

                    if(turret.MinElevationDegrees <= -180 && turret.MaxElevationDegrees >= 180)
                        GetLine().Color(COLOR_GOOD).Append("Pitch: ").AngleFormatDeg(360);
                    else
                        GetLine().Color(COLOR_WARNING).Append("Pitch: ").AngleFormatDeg(turret.MinElevationDegrees).Append(" to ").AngleFormatDeg(turret.MaxElevationDegrees);

                    GetLine().ResetColor().Append(" @ ").RotationSpeed(turret.ElevationSpeed * GameData.Hardcoded.Turret_RotationSpeedMul).Separator();

                    if(turret.MinAzimuthDegrees <= -180 && turret.MaxAzimuthDegrees >= 180)
                        GetLine().Color(COLOR_GOOD).Append("Yaw: ").AngleFormatDeg(360);
                    else
                        GetLine().Color(COLOR_WARNING).Append("Yaw: ").AngleFormatDeg(turret.MinAzimuthDegrees).Append(" to ").AngleFormatDeg(turret.MaxAzimuthDegrees);

                    GetLine().ResetColor().Append(" @ ").RotationSpeed(turret.RotationSpeed * GameData.Hardcoded.Turret_RotationSpeedMul).EndLine();

                    // TODO visualize angle limits?
                }
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ExtraInfo))
            {
                AddLine().Label("Accuracy").DistanceFormat((float)Math.Tan(wepDef.DeviateShotAngle) * 200).Append(" group at 100m").Separator().Append("Reload: ").TimeFormat(wepDef.ReloadTime / 1000).EndLine();
            }

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.AmmoDetails))
            {
                ammoProjectiles.Clear();
                ammoMissiles.Clear();

                for(int i = 0; i < wepDef.AmmoMagazinesId.Length; i++)
                {
                    var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[i]);
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                    int ammoType = (int)ammo.AmmoType;

                    if(wepDef.WeaponAmmoDatas[ammoType] != null)
                    {
                        switch(ammoType)
                        {
                            case 0: ammoProjectiles.Add(MyTuple.Create(mag, (MyProjectileAmmoDefinition)ammo)); break;
                            case 1: ammoMissiles.Add(MyTuple.Create(mag, (MyMissileAmmoDefinition)ammo)); break;
                        }
                    }
                }

                if(ammoProjectiles.Count > 0)
                {
                    // TODO check if wepDef.DamageMultiplier is used for weapons (right now in 1.186.5 it's not)

                    var projectilesData = wepDef.WeaponAmmoDatas[0];

                    AddLine().Label("Projectiles - Fire rate").Append(Math.Round(projectilesData.RateOfFire / 60f, 3)).Append(" rounds/s")
                        .Separator().Color(projectilesData.ShotsInBurst == 0 ? COLOR_GOOD : COLOR_WARNING).Append("Magazine: ");
                    if(projectilesData.ShotsInBurst == 0)
                        GetLine().Append("No reloading");
                    else
                        GetLine().Append(projectilesData.ShotsInBurst);
                    GetLine().EndLine();

                    AddLine().Append("Projectiles - ").Color(COLOR_PART).Append("Type").ResetColor().Append(" (")
                        .Color(COLOR_STAT_SHIPDMG).Append("ship").ResetColor().Append(", ")
                        .Color(COLOR_STAT_CHARACTERDMG).Append("character").ResetColor().Append(", ")
                        .Color(COLOR_STAT_HEADSHOTDMG).Append("headshot").ResetColor().Append(", ")
                        .Color(COLOR_STAT_SPEED).Append("speed").ResetColor().Append(", ")
                        .Color(COLOR_STAT_TRAVEL).Append("travel").ResetColor().Append(")").EndLine();

                    for(int i = 0; i < ammoProjectiles.Count; ++i)
                    {
                        var data = ammoProjectiles[i];
                        var mag = data.Item1;
                        var ammo = data.Item2;

                        AddLine().Append("      - ").Color(COLOR_PART).Append(mag.Id.SubtypeName).ResetColor().Append(" (");

                        if(ammo.ProjectileCount > 1)
                            GetLine().Color(COLOR_STAT_PROJECTILECOUNT).Append(ammo.ProjectileCount).Append("x ");

                        GetLine().Color(COLOR_STAT_SHIPDMG).Append(ammo.ProjectileMassDamage).ResetColor().Append(", ")
                            .Color(COLOR_STAT_CHARACTERDMG).Append(ammo.ProjectileHealthDamage).ResetColor().Append(", ")
                            .Color(COLOR_STAT_HEADSHOTDMG).Append(ammo.HeadShot ? ammo.ProjectileHeadShotDamage : ammo.ProjectileHealthDamage).ResetColor().Append(", ");

                        if(ammo.SpeedVar > 0)
                            GetLine().Color(COLOR_STAT_SPEED).Number(ammo.DesiredSpeed * (1f - ammo.SpeedVar)).Append("~").Number(ammo.DesiredSpeed * (1f + ammo.SpeedVar)).Append(" m/s");
                        else
                            GetLine().Color(COLOR_STAT_SPEED).SpeedFormat(ammo.DesiredSpeed);

                        GetLine().ResetColor().Append(", ")
                            .Color(COLOR_STAT_TRAVEL).DistanceRangeFormat(ammo.MaxTrajectory * GameData.Hardcoded.Projectile_RangeMultiplier_Min, ammo.MaxTrajectory * GameData.Hardcoded.Projectile_RangeMultiplier_Max).ResetColor().Append(")").EndLine();
                    }
                }

                if(ammoMissiles.Count > 0)
                {
                    var missileData = wepDef.WeaponAmmoDatas[1];

                    AddLine().Label("Missiles - Fire rate").Append(Math.Round(missileData.RateOfFire / 60f, 3)).Append(" rounds/s")
                        .Separator().Color(missileData.ShotsInBurst == 0 ? COLOR_GOOD : COLOR_WARNING).Append("Magazine: ");
                    if(missileData.ShotsInBurst == 0)
                        GetLine().Append("No reloading");
                    else
                        GetLine().Append(missileData.ShotsInBurst);
                    GetLine().EndLine();

                    AddLine().Append("Missiles - ").Color(COLOR_PART).Append("Type").ResetColor().Append(" (")
                        .Color(COLOR_STAT_SHIPDMG).Append("damage").ResetColor().Append(", ")
                        .Color(COLOR_STAT_CHARACTERDMG).Append("radius").ResetColor().Append(", ")
                        .Color(COLOR_STAT_SPEED).Append("speed").ResetColor().Append(", ")
                        .Color(COLOR_STAT_TRAVEL).Append("travel").ResetColor().Append(")").EndLine();

                    for(int i = 0; i < ammoMissiles.Count; ++i)
                    {
                        var data = ammoMissiles[i];
                        var mag = data.Item1;
                        var ammo = data.Item2;

                        AddLine().Append("      - ").Color(COLOR_PART).Append(mag.Id.SubtypeName).ResetColor().Append(" (")
                            .Color(COLOR_STAT_SHIPDMG).Append(ammo.MissileExplosionDamage).ResetColor().Append(", ")
                            .Color(COLOR_STAT_CHARACTERDMG).DistanceFormat(ammo.MissileExplosionRadius).ResetColor().Append(", ");

                        // SpeedVar is not used for missiles

                        GetLine().Color(COLOR_STAT_SPEED);

                        if(!ammo.MissileSkipAcceleration)
                            GetLine().SpeedFormat(ammo.MissileInitialSpeed).Append(" + ").AccelerationFormat(ammo.MissileAcceleration);
                        else
                            GetLine().SpeedFormat(ammo.DesiredSpeed * GameData.Hardcoded.Missile_DesiredSpeedMultiplier);

                        GetLine().ResetColor().Append(", ").Color(COLOR_STAT_TRAVEL).DistanceFormat(ammo.MaxTrajectory)
                            .ResetColor().Append(")").EndLine();
                    }
                }

                ammoProjectiles.Clear();
                ammoMissiles.Clear();
            }
        }

        private void Format_Warhead(MyCubeBlockDefinition def)
        {
            var warhead = (MyWarheadDefinition)def; // does not extend MyWeaponBlockDefinition

            // HACK: hardcoded; Warhead doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.AmmoDetails))
            {
                AddLine().Label("Radius").DistanceFormat(warhead.ExplosionRadius).EndLine();
                AddLine().Label("Damage").AppendFormat("{0:#,###,###,###,##0.##}", warhead.WarheadExplosionDamage).EndLine();
            }
        }
        #endregion

        private void PowerRequired(float mw, string groupName, bool powerHardcoded = false, bool groupHardcoded = false)
        {
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
            {
                var groupNameHash = (groupName != null ? MyStringHash.GetOrCompute(groupName) : MyStringHash.NullOrEmpty);
                PowerRequired(mw, groupNameHash, powerHardcoded);
            }
        }

        private void PowerRequired(float mw, MyStringHash groupName, bool powerHardcoded = false)
        {
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.PowerStats))
            {
                var color = (mw <= 0 ? COLOR_GOOD : COLOR_NORMAL);

                if(powerHardcoded)
                    AddLine().Color(color).Label("Power required");
                else
                    AddLine().Color(color).LabelHardcoded("Power required", color);

                if(mw <= 0)
                    GetLine().Append("No");
                else
                    GetLine().PowerFormat(mw);

                if(groupName != MyStringHash.NullOrEmpty && Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.ResourcePriorities))
                    GetLine().ResetColor().Separator().ResourcePriority(groupName);
            }
        }

        private void InventoryStats(MyCubeBlockDefinition def, float alternateVolume, float hardcodedVolume)
        {
            if(Settings.HeldInfo.IsSet(Settings.HeldInfoFlags.InventoryStats))
            {
                float volume;
                if(!GetInventoryFromComponent(def, out volume))
                    volume = alternateVolume;

                if(volume > 0)
                    AddLine().Label("Inventory").InventoryFormat(volume);
                else if(hardcodedVolume > 0)
                    AddLine().LabelHardcoded("Inventory").InventoryFormat(hardcodedVolume);
                // else unknown inventory /shrug
            }
        }
    }
}
