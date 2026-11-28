using System;
using System.Collections.Generic;
using UnityEngine;

namespace FpsMaps.Bazaar
{
    public enum BazaarSurface
    {
        Sand,
        Cobble,
        Sandstone,
        SandstoneDark,
        Whitewash,
        Terracotta,
        RoofTile,
        Limestone,
        WoodDark,
        Crate,
        PaintedBlue,
        MetalGrey,
        AwningRed,
        AwningBlue,
        AwningYellow,
        AwningGreen,
        CarWhite,
        CarOchre,
        CarGreen,
        PaintWhite,
        PaintAttack,
        PaintDefend,
        PaintPlant,
        Foliage,
        Lamp,
        Backdrop
    }

    /// <summary>Height bands of the map: the pit floor, street level and the raised "heaven" decks.</summary>
    public enum BazaarTier
    {
        Pit = -1,
        Ground = 0,
        Heaven = 1
    }

    [Serializable]
    public sealed class BlockSpec
    {
        public string Name;
        public string Group;
        public BazaarSurface Surface;
        public Vector3 Center;
        public Vector3 Size;
        public bool Solid = true;

        public Bounds Aabb => new Bounds(Center, Size);
    }

    [Serializable]
    public sealed class LightSpec
    {
        public string Name;
        public Vector3 Position;
        public Color Color;
        public float Intensity;
        public float Range;
    }

    [Serializable]
    public sealed class SpawnSpec
    {
        public string Name;
        public int Team;
        public Vector3 Position;
        public float Yaw;
    }

    [Serializable]
    public sealed class WaypointSpec
    {
        public string Name;
        public string Area;
        public Vector3 Position;
        public BazaarTier Tier;
    }

    [Serializable]
    public sealed class FlightSpec
    {
        public string Name;
        public Vector3 Foot;
        public Vector3 Top;
        public float Width;
        public float Rise;
        public float Run;
        public int Steps;
    }

    [Serializable]
    public sealed class OpeningSpec
    {
        public string Name;
        public Vector3 Center;
        public float Width;
        public float Height;
        public float BottomY;
        public bool IsDoor;
    }

    [Serializable]
    public sealed class AreaSpec
    {
        public string Name;
        public string Kind;
        public Bounds Bounds;
    }

    /// <summary>A named bot walk-through: the bot starts at <see cref="Start"/> and has to visit every point.</summary>
    [Serializable]
    public sealed class RouteSpec
    {
        public string Name;
        public Vector3 Start;
        public List<Vector3> Points = new List<Vector3>();
    }

    /// <summary>A player-only climb: top-surface centres from low to high, every hop within the jump height, last hop within the mantle height.</summary>
    [Serializable]
    public sealed class JumpRouteSpec
    {
        public string Name;
        public List<Vector3> Tops = new List<Vector3>();
    }

    /// <summary>A one-way drop: stand on <see cref="Top"/>, fall to <see cref="Landing"/> (too high to climb back).</summary>
    [Serializable]
    public sealed class DropSpec
    {
        public string Name;
        public Vector3 Top;
        public Vector3 Landing;
    }

    /// <summary>Marker volume for a future bomb mode: bomb plant zones and team buy areas.</summary>
    [Serializable]
    public sealed class ZoneSpec
    {
        public string Name;
        public BazaarZoneKind Kind;
        public string Site;
        public Bounds Bounds;
    }

    public sealed class BazaarLayoutData
    {
        public readonly List<BlockSpec> Blocks = new List<BlockSpec>();
        public readonly List<LightSpec> Lights = new List<LightSpec>();
        public readonly List<SpawnSpec> Spawns = new List<SpawnSpec>();
        public readonly List<WaypointSpec> Waypoints = new List<WaypointSpec>();
        public readonly List<FlightSpec> Flights = new List<FlightSpec>();
        public readonly List<OpeningSpec> Openings = new List<OpeningSpec>();
        public readonly List<AreaSpec> Areas = new List<AreaSpec>();
        public readonly List<RouteSpec> Routes = new List<RouteSpec>();
        public readonly List<JumpRouteSpec> JumpRoutes = new List<JumpRouteSpec>();
        public readonly List<DropSpec> Drops = new List<DropSpec>();
        public readonly List<ZoneSpec> Zones = new List<ZoneSpec>();

        public AreaSpec FindArea(string name)
        {
            for (int i = 0; i < Areas.Count; i++)
            {
                if (Areas[i].Name == name)
                    return Areas[i];
            }

            return null;
        }

        public ZoneSpec FindZone(string name)
        {
            for (int i = 0; i < Zones.Count; i++)
            {
                if (Zones[i].Name == name)
                    return Zones[i];
            }

            return null;
        }
    }
}
