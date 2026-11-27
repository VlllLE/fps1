using System;
using System.Collections.Generic;
using UnityEngine;

namespace FpsMaps.Triad
{
    public enum TriadSurface
    {
        Asphalt,
        Concrete,
        ConcreteDark,
        Brick,
        BrickLight,
        BrickDark,
        Plaster,
        MetalBlue,
        MetalRust,
        MetalGrey,
        RoofTar,
        FloorTile,
        Wood,
        Crate,
        ContainerRed,
        ContainerBlue,
        ContainerGreen,
        ContainerYellow,
        Barrier,
        Hazard,
        Lamp,
        Skyline
    }

    /// <summary>The three vertical tiers of the map: street level, mid (mass roofs, upper floors, bridges) and high (building roofs).</summary>
    public enum TriadTier
    {
        Ground = 0,
        Mid = 1,
        High = 2
    }

    [Serializable]
    public sealed class BlockSpec
    {
        public string Name;
        public string Group;
        public TriadSurface Surface;
        public Vector3 Center;
        public Vector3 Size;
        public Vector3 Euler;
        public bool Solid = true;

        public bool IsAxisAligned => Euler == Vector3.zero;

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
        public TriadTier Tier;
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

    /// <summary>A player-only climb: top-surface centres from low to high, every hop within the jump height.</summary>
    [Serializable]
    public sealed class JumpRouteSpec
    {
        public string Name;
        public List<Vector3> Tops = new List<Vector3>();
    }

    public sealed class TriadLayoutData
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

        public AreaSpec FindArea(string name)
        {
            for (int i = 0; i < Areas.Count; i++)
            {
                if (Areas[i].Name == name)
                    return Areas[i];
            }

            return null;
        }
    }
}
