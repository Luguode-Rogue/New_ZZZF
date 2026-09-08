using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using IoPath = System.IO.Path;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    public sealed class TerrainPhotographer_Tableau
    {
        private readonly TerrainPhotographer _inner = new TerrainPhotographer();
        public bool IsCompleted { get { return _inner.IsCompleted; } }
        public bool IsActive { get { return _inner.IsActive; } }
        public bool Failed { get { return _inner.Failed; } }
        public void Start(Mission mission, TerrainCache cache) { _inner.Start(mission, cache); }
        public bool Tick() { return _inner.Tick(); }
        public void OnMissionEnd() { _inner.OnMissionEnd(); }
    }
}
