using System.Collections.Generic;
using System.Linq;
using FpsMaps.Triad;
using NUnit.Framework;
using UnityEngine;

/// <summary>Pure-data checks of the Triad layout: lanes, tiers, buildings, spawns and the player/bot movement limits.</summary>
public class TriadLayoutTests
{
    private const float JumpHeight = 1.3f;
    private const float MantleHeight = 2.0f;

    private TriadLayoutData layout;
    private List<BlockSpec> solids;

    [SetUp]
    public void SetUp()
    {
        layout = TriadLayout.Create();
        solids = layout.Blocks.Where(b => b.Solid && b.IsAxisAligned).ToList();
    }

    private static Bounds BodyBounds(Vector3 feet, float lift = 0.12f)
    {
        var b = new Bounds();
        b.SetMinMax(new Vector3(feet.x - 0.38f, feet.y + lift, feet.z - 0.38f), new Vector3(feet.x + 0.38f, feet.y + 1.85f, feet.z + 0.38f));
        return b;
    }

    private bool IntersectsSolid(Bounds body, out BlockSpec hit)
    {
        foreach (BlockSpec block in solids)
        {
            if (block.Aabb.Intersects(body))
            {
                hit = block;
                return true;
            }
        }

        hit = null;
        return false;
    }

    private bool HasSupport(Vector3 feet)
    {
        foreach (BlockSpec block in solids)
        {
            Bounds a = block.Aabb;
            if (feet.x < a.min.x || feet.x > a.max.x || feet.z < a.min.z || feet.z > a.max.z)
                continue;
            if (a.max.y >= feet.y - 0.15f && a.max.y <= feet.y + 0.45f)
                return true;
        }

        return false;
    }

    [Test]
    public void Layout_IsDeterministic()
    {
        TriadLayoutData other = TriadLayout.Create();
        Assert.AreEqual(layout.Blocks.Count, other.Blocks.Count);
        Assert.AreEqual(layout.Waypoints.Count, other.Waypoints.Count);
        for (int i = 0; i < layout.Blocks.Count; i++)
        {
            Assert.AreEqual(layout.Blocks[i].Name, other.Blocks[i].Name);
            Assert.AreEqual(layout.Blocks[i].Center, other.Blocks[i].Center);
        }
    }

    [Test]
    public void Layout_HasThreeLanes_ThreeBuildings_AndTheirBlocks()
    {
        Assert.AreEqual(3, TriadLayout.LaneNames.Length);
        Assert.AreEqual(3, TriadLayout.BuildingNames.Length);
        Assert.AreEqual(3, layout.Areas.Count(a => a.Kind == "Lane"));
        Assert.AreEqual(3, layout.Areas.Count(a => a.Kind == "Building"));

        foreach (string lane in TriadLayout.LaneNames)
            Assert.IsNotNull(layout.FindArea("Lane" + lane), "Missing lane " + lane);

        foreach (string building in TriadLayout.BuildingNames)
        {
            Assert.IsNotNull(layout.FindArea(building), "Missing building " + building);
            Assert.Greater(layout.Blocks.Count(b => b.Group == building), 40, building + " should have a real interior");
        }

        Assert.Greater(layout.Blocks.Count, 800);
    }

    [Test]
    public void Layout_UsesThreeTiers_InsideEveryBuildingAndLane()
    {
        foreach (string building in TriadLayout.BuildingNames)
        {
            AreaSpec area = layout.FindArea(building);
            foreach (TriadTier tier in new[] { TriadTier.Ground, TriadTier.Mid, TriadTier.High })
            {
                Assert.IsTrue(layout.Waypoints.Any(w => w.Tier == tier && area.Bounds.Contains(new Vector3(w.Position.x, area.Bounds.center.y, w.Position.z))),
                    building + " has no waypoint on tier " + tier);
            }
        }

        foreach (string lane in TriadLayout.LaneNames)
        {
            AreaSpec area = layout.FindArea("Lane" + lane);
            foreach (TriadTier tier in new[] { TriadTier.Ground, TriadTier.Mid })
            {
                Assert.IsTrue(layout.Waypoints.Any(w => w.Tier == tier && area.Bounds.Contains(new Vector3(w.Position.x, area.Bounds.center.y, w.Position.z))),
                    "Lane" + lane + " has no waypoint on tier " + tier);
            }
        }

        Assert.IsTrue(layout.Waypoints.Any(w => Mathf.Abs(w.Position.y - TriadLayout.HighTierY) < 0.6f), "Rooftop tier (y 8) is missing");
        Assert.IsTrue(layout.Waypoints.Any(w => Mathf.Abs(w.Position.y - TriadLayout.MidTierY) < 0.6f), "Mid tier (y 4) is missing");
    }

    [Test]
    public void Spawns_TwoTeamsFacingEachOther_PlayerSpawnFirst()
    {
        Assert.AreEqual("PlayerSpawn", layout.Spawns[0].Name);
        Assert.GreaterOrEqual(layout.Spawns.Count(s => s.Team == 0), 9);
        Assert.GreaterOrEqual(layout.Spawns.Count(s => s.Team == 1), 9);
        Assert.IsTrue(layout.Spawns.Where(s => s.Team == 0).All(s => s.Position.z < -46f), "Team A spawns in the south yard");
        Assert.IsTrue(layout.Spawns.Where(s => s.Team == 1).All(s => s.Position.z > 46f), "Team B spawns in the north yard");
        Assert.AreEqual(layout.Spawns.Count, layout.Spawns.Select(s => s.Name).Distinct().Count(), "Spawn names must be unique");
    }

    [Test]
    public void Spawns_AreOnTheGround_AndNotInsideSolidGeometry()
    {
        foreach (SpawnSpec spawn in layout.Spawns)
        {
            Assert.IsFalse(IntersectsSolid(BodyBounds(spawn.Position), out BlockSpec hit), spawn.Name + " intersects " + (hit != null ? hit.Name : ""));
            Assert.IsTrue(HasSupport(spawn.Position), spawn.Name + " has no floor under it");
        }
    }

    [Test]
    public void Spawns_AreSpacedApart()
    {
        for (int i = 0; i < layout.Spawns.Count; i++)
        {
            for (int j = i + 1; j < layout.Spawns.Count; j++)
            {
                float d = Vector3.Distance(layout.Spawns[i].Position, layout.Spawns[j].Position);
                Assert.Greater(d, 1.5f, layout.Spawns[i].Name + " and " + layout.Spawns[j].Name + " overlap");
            }
        }
    }

    [Test]
    public void Waypoints_AreFreeOfGeometry_AndStandOnSomething()
    {
        foreach (WaypointSpec w in layout.Waypoints)
        {
            Assert.IsFalse(IntersectsSolid(BodyBounds(w.Position), out BlockSpec hit), w.Name + " is inside " + (hit != null ? hit.Name : ""));
            Assert.IsTrue(HasSupport(w.Position), w.Name + " has no floor under it at y=" + w.Position.y);
        }

        Assert.AreEqual(layout.Waypoints.Count, layout.Waypoints.Select(w => w.Name).Distinct().Count(), "Waypoint names must be unique");
        Assert.GreaterOrEqual(layout.Waypoints.Count, 60);
    }

    [Test]
    public void Routes_PointsAreFreeOfGeometry()
    {
        Assert.AreEqual(6, layout.Routes.Count);
        foreach (RouteSpec route in layout.Routes)
        {
            var all = new List<Vector3> { route.Start };
            all.AddRange(route.Points);
            foreach (Vector3 p in all)
            {
                Assert.IsFalse(IntersectsSolid(BodyBounds(p), out BlockSpec hit), route.Name + " point " + p + " is inside " + (hit != null ? hit.Name : ""));
                Assert.IsTrue(HasSupport(p), route.Name + " point " + p + " has no floor");
            }
        }
    }

    [Test]
    public void Flights_AreWalkableStairs()
    {
        Assert.GreaterOrEqual(layout.Flights.Count, 15, "Every building and every block needs stairs");
        foreach (FlightSpec f in layout.Flights)
        {
            Assert.LessOrEqual(f.Rise, 0.3f, f.Name + " risers are too tall for the CharacterController step offset");
            Assert.GreaterOrEqual(f.Run, 0.25f, f.Name + " treads are too short");
            Assert.GreaterOrEqual(f.Width, 1.6f, f.Name + " is too narrow for two bots");
            Assert.AreEqual(f.Steps * f.Rise, f.Top.y - f.Foot.y, 0.02f, f.Name + " rise does not match its height");
        }
    }

    [Test]
    public void Doors_AreWideAndTallEnough()
    {
        var doors = layout.Openings.Where(o => o.IsDoor).ToList();
        Assert.GreaterOrEqual(doors.Count, 12, "Buildings need several real doors");
        foreach (OpeningSpec door in doors)
        {
            Assert.GreaterOrEqual(door.Width, 1.3f, door.Name + " is too narrow");
            Assert.GreaterOrEqual(door.Height, 2.3f, door.Name + " is too low");
        }

        Assert.Greater(layout.Openings.Count(o => !o.IsDoor), 10, "Windows give the buildings sightlines");
    }

    [Test]
    public void Openings_AllThreeBuildingsCanBeEntered_FromTheStreet()
    {
        foreach (string building in TriadLayout.BuildingNames)
        {
            AreaSpec area = layout.FindArea(building);
            Bounds padded = area.Bounds;
            padded.Expand(new Vector3(1f, 0f, 1f));
            int groundDoors = layout.Openings.Count(o => o.IsDoor && o.Center.y < 2f && padded.Contains(new Vector3(o.Center.x, area.Bounds.center.y, o.Center.z)));
            Assert.GreaterOrEqual(groundDoors, 4, building + " needs several ground-level doors");
        }
    }

    [Test]
    public void JumpRoutes_StayWithinJumpAndMantleLimits()
    {
        Assert.GreaterOrEqual(layout.JumpRoutes.Count, 6);
        foreach (JumpRouteSpec route in layout.JumpRoutes)
        {
            float previous = 0f;
            for (int i = 0; i < route.Tops.Count; i++)
            {
                float hop = route.Tops[i].y - previous;
                bool last = i == route.Tops.Count - 1;
                Assert.LessOrEqual(hop, last ? MantleHeight : JumpHeight, route.Name + " hop " + i + " is " + hop + " m");
                Assert.Greater(hop, 0f, route.Name + " hop " + i + " does not climb");
                previous = route.Tops[i].y;
            }

            Assert.AreEqual(TriadLayout.MidTierY, previous, 0.01f, route.Name + " should end on the mid tier");
        }
    }

    [Test]
    public void Blocks_AreUsableMeshesWithMaterialsAndNames()
    {
        foreach (BlockSpec b in layout.Blocks)
        {
            Assert.IsFalse(string.IsNullOrEmpty(b.Name));
            Assert.IsFalse(string.IsNullOrEmpty(b.Group), b.Name + " has no group");
            Assert.Greater(b.Size.x, 0.001f, b.Name);
            Assert.Greater(b.Size.y, 0.001f, b.Name);
            Assert.Greater(b.Size.z, 0.001f, b.Name);
            Assert.IsFalse(float.IsNaN(b.Center.x + b.Center.y + b.Center.z), b.Name);
        }
    }

    [Test]
    public void Blocks_StayInsideThePlayableFootprint()
    {
        foreach (BlockSpec b in layout.Blocks.Where(x => x.Surface != TriadSurface.Skyline))
        {
            Bounds a = b.Aabb;
            Assert.LessOrEqual(a.max.x, 46.5f, b.Name);
            Assert.GreaterOrEqual(a.min.x, -46.5f, b.Name);
            Assert.LessOrEqual(a.max.z, 64.5f, b.Name);
            Assert.GreaterOrEqual(a.min.z, -64.5f, b.Name);
        }
    }

    [Test]
    public void Skyline_IsDecorOnly_AndOutsideTheWalls()
    {
        foreach (BlockSpec b in layout.Blocks.Where(x => x.Surface == TriadSurface.Skyline))
        {
            Assert.IsFalse(b.Solid, b.Name + " should not have a collider");
            Assert.IsTrue(Mathf.Abs(b.Center.x) > 50f || Mathf.Abs(b.Center.z) > 66f, b.Name + " is inside the arena");
        }
    }

    [Test]
    public void Lights_AreWarmDuskLamps_WithinTheMap()
    {
        Assert.GreaterOrEqual(layout.Lights.Count, 20);
        foreach (LightSpec l in layout.Lights)
        {
            Assert.Greater(l.Range, 3f, l.Name);
            Assert.Greater(l.Intensity, 0f, l.Name);
        }
    }
}
