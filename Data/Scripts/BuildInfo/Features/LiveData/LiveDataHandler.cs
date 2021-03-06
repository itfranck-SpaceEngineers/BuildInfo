﻿using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.LiveData
{
    /// <summary>
    /// Handles grabbing and catching live block data which requires spawning a temporary grid with block to get that data from the block, because it's unavailable from definitions.
    /// </summary>
    public class LiveDataHandler : ModComponent
    {
        public BData_Base BlockDataCache;
        public bool BlockDataCacheValid = true;
        public readonly Dictionary<MyDefinitionId, BData_Base> BlockData = new Dictionary<MyDefinitionId, BData_Base>(MyDefinitionId.Comparer);
        public readonly HashSet<MyDefinitionId> BlockSpawnInProgress = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        public LiveDataHandler(BuildInfoMod main) : base(main)
        {
            AddType<BData_Collector>(typeof(MyObjectBuilder_Collector));

            AddType<BData_LandingGear>(typeof(MyObjectBuilder_LandingGear));

            AddType<BData_Connector>(typeof(MyObjectBuilder_ShipConnector));

            AddType<BData_ShipTool>(typeof(MyObjectBuilder_ShipWelder));
            AddType<BData_ShipTool>(typeof(MyObjectBuilder_ShipGrinder));

            AddType<BData_Thrust>(typeof(MyObjectBuilder_Thrust));

            AddType<BData_Weapon>(typeof(MyObjectBuilder_SmallGatlingGun));
            AddType<BData_Weapon>(typeof(MyObjectBuilder_SmallMissileLauncher));
            AddType<BData_Weapon>(typeof(MyObjectBuilder_SmallMissileLauncherReload));
            AddType<BData_Weapon>(typeof(MyObjectBuilder_LargeGatlingTurret));
            AddType<BData_Weapon>(typeof(MyObjectBuilder_LargeMissileTurret));
            AddType<BData_Weapon>(typeof(MyObjectBuilder_InteriorTurret));
        }

        protected override void RegisterComponent()
        {
            EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
        }

        protected override void UnregisterComponent()
        {
            EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
        }

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            BlockDataCache = null;
            BlockDataCacheValid = true;
        }

        private void AddType<T>(MyObjectBuilderType blockType) where T : BData_Base, new()
        {
            BlockMonitor.MonitorType(blockType, BlockAdded<T>);
        }

        private void BlockAdded<T>(IMySlimBlock slimBlock) where T : BData_Base, new()
        {
            if(slimBlock.FatBlock == null)
                return;

            var success = BData_Base.TrySetData<T>(slimBlock.FatBlock);

            if(success && TextGeneration != null)
            {
                // reset caches and force block text recalc
                TextGeneration.CachedBuildInfoTextAPI.Remove(slimBlock.BlockDefinition.Id);
                TextGeneration.CachedBuildInfoNotification.Remove(slimBlock.BlockDefinition.Id);
                TextGeneration.LastDefId = default(MyDefinitionId);
            }
        }
    }
}
