﻿using Sandbox.ModAPI;

namespace Digi.ComponentLib
{
    // HACK class rename required because otherwise it won't work properly with other mods that use this component system.
    // https://support.keenswh.com/spaceengineers/general/topic/1-192-022modapi-session-update-broken-by-namespaceclass-sharing-in-multiple-mods
    public partial class BuildInfo_GameSession
    {
        void LoadMod()
        {
            if(MyAPIGateway.Utilities.IsDedicated)
                return; // this mod does nothing server side (with no render), no reason to allocate any more memory.

            main = new BuildInfo.BuildInfoMod(this);
        }
    }
}