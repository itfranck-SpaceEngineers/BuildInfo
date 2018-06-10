﻿using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo
{
    public class TempBlockSpawn
    {
        public readonly long EntityId;
        public readonly MyCubeBlockDefinition BlockDef;
        public event Action<IMyCubeBlock> AfterSpawn;

        public TempBlockSpawn(MyCubeBlockDefinition def)
        {
            BlockDef = def;

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var spawnPos = camMatrix.Translation + camMatrix.Backward * 100;

            var blockObj = (MyObjectBuilder_CubeBlock)MyObjectBuilderSerializer.CreateNewObject(def.Id);

            var gridObj = new MyObjectBuilder_CubeGrid()
            {
                CreatePhysics = false,
                GridSizeEnum = def.CubeSize,
                PositionAndOrientation = new MyPositionAndOrientation(spawnPos, Vector3.Forward, Vector3.Up),
                PersistentFlags = MyPersistentEntityFlags2.InScene,
                IsStatic = true,
                Editable = false,
                DestructibleBlocks = false,
                IsRespawnGrid = false,
                Name = "BuildInfo_TemporaryGrid",
                CubeBlocks = new List<MyObjectBuilder_CubeBlock>(1) { blockObj },
            };

            MyAPIGateway.Entities.RemapObjectBuilder(gridObj);

            EntityId = gridObj.EntityId;

            MyAPIGateway.Entities.CreateFromObjectBuilderParallel(gridObj, true, SpawnCompleted);
        }

        private void SpawnCompleted()
        {
            IMyCubeGrid grid = null;

            try
            {
                var ent = MyEntities.GetEntityById(EntityId, true);

                if(ent == null)
                {
                    Log.Error($"Can't get spawned entity for block: {BlockDef.Id}");
                    return;
                }

                ent.IsPreview = true;
                ent.Save = false;
                ent.Flags = EntityFlags.None;
                ent.Render.Visible = false;

                grid = (IMyCubeGrid)ent;
                var block = grid.GetCubeBlock(Vector3I.Zero)?.FatBlock as IMyCubeBlock;

                if(block == null)
                {
                    Log.Error($"Can't get block from spawned entity for block: {BlockDef.Id} (mod workshopId={BlockDef.Context.GetWorkshopID()})");
                    return;
                }

                AfterSpawn?.Invoke(block);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                grid?.Close();
            }
        }
    }
}