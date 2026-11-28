using System.Collections.Generic;
using System.Linq;
using FpsMaps.Bazaar;
using NUnit.Framework;
using UnityEngine;

/// <summary>Pure-data checks of the Bazaar layout: two sites, three routes, sightlines, rooms, spawns, zones and the movement limits.</summary>
public class BazaarLayoutTests
{
    private const float JumpHeight = 1.3f;
    private const float MantleHeight = 2.0f;
    private const float EyeHeight = 1.6f;

    private BazaarLayoutData layout;
    private List<BlockSpec> solids;

    [SetUp]
    public void SetUp()
    {
        layout = BazaarLayout.Create();
        solids = layout.Blocks.Where(b => b.Solid).ToList();
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

    private float ClearRun(Vector3 origin, Vector3 direction, float max = 200f)
    {
        float best = max;
        var ray = new Ray(origin, direction.normalized);
        foreach (BlockSpec block in solids)
        {
            if (block.Aabb.IntersectRay(ray, out float distance) && distance < best)
                best = distance;
        }

        return best;
    }

    [Test]
    public void Layout_IsDeterministic()
    {
        BazaarLayoutData other = BazaarLayout.Create();
        Assert.AreEqual(layout.Blocks.Count, other.Blocks.Count);
        Assert.AreEqual(layout.Waypoints.Count, other.Waypoints.Count);
        for (int i = 0; i < layout.Blocks.Count; i++)
        {
            Assert.AreEqual(layout.Blocks[i].Name, other.Blocks[i].Name);
            Assert.AreEqual(layout.Blocks[i].Center, other.Blocks[i].Center);
        }
    }

    [Test]
    public void Layout_HasTwoSites_MidAndThreeAttackRoutes()
    {
        Assert.AreEqual(2, layout.Areas.Count(a => a.Kind == "Site"));
        Assert.IsNotNull(layout.FindArea("SiteA"));
        Assert.IsNotNull(layout.FindArea("SiteB"));
        foreach (string lane in new[] { "ALong", "AShort", "Mid", "BConnector", "BTunnels" })
            Assert.IsNotNull(layout.FindArea(lane), "Missing lane " + lane);

        Assert.IsNotNull(layout.FindArea("Hub"), "Defenders need a hub to rotate through");
        Assert.IsNotNull(layout.FindArea("AttackYard"));
        Assert.IsNotNull(layout.FindArea("DefendYard"));
        Assert.Greater(layout.Blocks.Count, 250);
    }

    [Test]
    public void Layout_EachSiteHasDefaultHeavenAndHellSpaces()
    {
        foreach (string site in new[] { "SiteA", "SiteB" })
        {
            AreaSpec area = layout.FindArea(site);
            var inside = layout.Waypoints.Where(w => area.Bounds.Contains(new Vector3(w.Position.x, area.Bounds.center.y, w.Position.z))).ToList();
            Assert.IsTrue(inside.Any(w => w.Tier == BazaarTier.Ground), site + " has no ground waypoint");
            Assert.IsTrue(inside.Any(w => w.Tier == BazaarTier.Heaven), site + " has no heaven waypoint");
            Assert.IsTrue(inside.Any(w => w.Name.EndsWith("_Room")), site + " has no default room waypoint");
        }

        Assert.IsTrue(layout.Waypoints.Any(w => w.Name == "ASite_Hell"), "A hell (under the deck) is missing");
        Assert.IsTrue(layout.Waypoints.Any(w => w.Name == "BSite_Pit" && w.Tier == BazaarTier.Pit), "B pit is missing");
        Assert.IsTrue(layout.Waypoints.Any(w => w.Position.y < -1f), "The pit needs a waypoint below ground");
    }

    [Test]
    public void Layout_EachSiteRoomHasTwoDoors()
    {
        Assert.AreEqual(2, layout.Openings.Count(o => o.Name.StartsWith("A_Room") && o.IsDoor));
        Assert.AreEqual(2, layout.Openings.Count(o => o.Name.StartsWith("B_Room") && o.IsDoor));
    }

    [Test]
    public void Sightlines_OnlyALongIsLong()
    {
        float aLong = ClearRun(new Vector3(-48f, EyeHeight, -29.9f), Vector3.forward);
        Assert.GreaterOrEqual(aLong, 36f, "A Long should be the one long sightline");

        var others = new Dictionary<string, float>
        {
            { "Mid", ClearRun(new Vector3(0f, EyeHeight, -29.9f), Vector3.forward) },
            { "BTunnels", ClearRun(new Vector3(38f, EyeHeight, -29.9f), Vector3.forward) },
            { "AShort", ClearRun(new Vector3(-14.1f, EyeHeight, 3f), Vector3.left) },
            { "BConnector", ClearRun(new Vector3(14.1f, EyeHeight, 3f), Vector3.right) },
            { "HubFromDefenders", ClearRun(new Vector3(0f, EyeHeight, 43.9f), Vector3.back) }
        };

        foreach (KeyValuePair<string, float> lane in others)
        {
            Assert.Less(lane.Value, 28f, lane.Key + " has a " + lane.Value + " m straight sightline");
            Assert.Less(lane.Value, aLong - 8f, lane.Key + " should be clearly shorter than A Long");
        }
    }

    [Test]
    public void Spawns_TwoTeamsFacingEachOther_PlayerSpawnFirst()
    {
        Assert.AreEqual("PlayerSpawn", layout.Spawns[0].Name);
        int attackers = layout.Spawns.Count(s => s.Team == 0);
        int defenders = layout.Spawns.Count(s => s.Team == 1);
        Assert.GreaterOrEqual(attackers, 10);
        Assert.AreEqual(attackers, defenders);
        Assert.IsTrue(layout.Spawns.Where(s => s.Team == 0).All(s => s.Position.z < -50f), "Attackers spawn in the south yard");
        Assert.IsTrue(layout.Spawns.Where(s => s.Team == 1).All(s => s.Position.z > 50f), "Defenders spawn in the north yard");
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
    public void Spawns_DefendersAreCloserToBothSites_AttackersAreEquidistant()
    {
        ZoneSpec a = layout.FindZone("PlantZone_A");
        ZoneSpec b = layout.FindZone("PlantZone_B");
        Vector3 attackers = AverageSpawn(0);
        Vector3 defenders = AverageSpawn(1);
        Assert.Less(Vector3.Distance(defenders, a.Bounds.center), Vector3.Distance(attackers, a.Bounds.center));
        Assert.Less(Vector3.Distance(defenders, b.Bounds.center), Vector3.Distance(attackers, b.Bounds.center));

        float toA = Vector3.Distance(attackers, a.Bounds.center);
        float toB = Vector3.Distance(attackers, b.Bounds.center);
        Assert.Less(Mathf.Abs(toA - toB) / Mathf.Max(toA, toB), 0.15f, "Sites should be a similar distance from the attacker spawn");
    }

    private Vector3 AverageSpawn(int team)
    {
        Vector3 sum = Vector3.zero;
        var teamSpawns = layout.Spawns.Where(s => s.Team == team).ToList();
        foreach (SpawnSpec s in teamSpawns)
            sum += s.Position;
        return sum / teamSpawns.Count;
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
        Assert.GreaterOrEqual(layout.Waypoints.Count, 55);
    }

    [Test]
    public void Routes_PointsAreFreeOfGeometry()
    {
        Assert.AreEqual(BazaarLayout.RouteNames.Length, layout.Routes.Count);
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
        Assert.GreaterOrEqual(layout.Flights.Count, 5);
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
        Assert.GreaterOrEqual(doors.Count, 10);
        foreach (OpeningSpec door in doors)
        {
            Assert.GreaterOrEqual(door.Width, 1.3f, door.Name + " is too narrow");
            Assert.GreaterOrEqual(door.Height, 2.3f, door.Name + " is too low");
        }

        Assert.IsTrue(layout.Openings.Any(o => !o.IsDoor), "The B window gives mid a sightline into B");
    }

    [Test]
    public void JumpRoutes_StayWithinJumpAndMantleLimits()
    {
        Assert.AreEqual(2, layout.JumpRoutes.Count, "One crate climb per site");
        foreach (JumpRouteSpec route in layout.JumpRoutes)
        {
            float previous = 0f;
            for (int i = 0; i < route.Tops.Count; i++)
            {
                float hop = route.Tops[i].y - previous;
                bool last = i == route.Tops.Count - 1;
                Assert.LessOrEqual(hop, last ? MantleHeight : JumpHeight, route.Name + " hop " + i + " is " + hop + " m");
                Assert.Greater(hop, 0f, route.Name + " hop " + i + " does not climb");
                Assert.IsTrue(HasSupport(route.Tops[i]), route.Name + " top " + i + " has no surface");
                previous = route.Tops[i].y;
            }

            Assert.AreEqual(BazaarLayout.HeavenY, previous, 0.01f, route.Name + " should end on the heaven deck");
        }
    }

    [Test]
    public void Drops_AreOneWay_AndLandOnFreeGround()
    {
        Assert.AreEqual(2, layout.Drops.Count);
        foreach (DropSpec drop in layout.Drops)
        {
            float height = drop.Top.y - drop.Landing.y;
            Assert.GreaterOrEqual(height, 2.5f, drop.Name + " must be too high to climb back (mantle 2.0 m)");
            Assert.LessOrEqual(height, 3.5f, drop.Name + " is a dangerous fall");
            Assert.IsTrue(HasSupport(drop.Top), drop.Name + " has no deck at the top");
            Assert.IsTrue(HasSupport(drop.Landing), drop.Name + " has no ground at the landing");
            Assert.IsFalse(IntersectsSolid(BodyBounds(drop.Landing), out BlockSpec hit), drop.Name + " lands inside " + (hit != null ? hit.Name : ""));
        }
    }

    [Test]
    public void Zones_PlantZonesSitOnTheSites_AndBuyZonesCoverTheYards()
    {
        Assert.AreEqual(4, layout.Zones.Count);
        foreach (string site in new[] { "A", "B" })
        {
            ZoneSpec zone = layout.FindZone("PlantZone_" + site);
            Assert.IsNotNull(zone);
            Assert.AreEqual(BazaarZoneKind.Plant, zone.Kind);
            AreaSpec area = layout.FindArea("Site" + site);
            Assert.GreaterOrEqual(zone.Bounds.min.x, area.Bounds.min.x);
            Assert.LessOrEqual(zone.Bounds.max.x, area.Bounds.max.x);
            Assert.GreaterOrEqual(zone.Bounds.min.z, area.Bounds.min.z);
            Assert.LessOrEqual(zone.Bounds.max.z, area.Bounds.max.z);
            Vector3 centre = zone.Bounds.center;
            centre.y = 0.05f;
            Assert.IsFalse(IntersectsSolid(BodyBounds(centre), out BlockSpec hit), zone.Name + " centre is blocked by " + (hit != null ? hit.Name : ""));
            Assert.IsTrue(HasSupport(centre), zone.Name + " centre has no floor");
        }

        ZoneSpec attack = layout.FindZone("BuyZone_Attack");
        ZoneSpec defend = layout.FindZone("BuyZone_Defend");
        Assert.AreEqual(BazaarZoneKind.Buy, attack.Kind);
        Assert.AreEqual(BazaarZoneKind.Buy, defend.Kind);
        Assert.IsTrue(layout.Spawns.Where(s => s.Team == 0).All(s => attack.Bounds.Contains(new Vector3(s.Position.x, 1f, s.Position.z))), "Attacker spawns are inside their buy zone");
        Assert.IsTrue(layout.Spawns.Where(s => s.Team == 1).All(s => defend.Bounds.Contains(new Vector3(s.Position.x, 1f, s.Position.z))), "Defender spawns are inside their buy zone");
        Assert.GreaterOrEqual(attack.Bounds.size.x * attack.Bounds.size.z, 300f, "Buy yard should be big enough for a team");
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

        Assert.AreEqual(layout.Blocks.Count, layout.Blocks.Select(b => b.Name).Distinct().Count(), "Block names must be unique");
    }

    [Test]
    public void Blocks_StayInsideThePlayableFootprint()
    {
        foreach (BlockSpec b in layout.Blocks.Where(x => x.Group != "Backdrop"))
        {
            Bounds a = b.Aabb;
            Assert.LessOrEqual(a.max.x, 58.01f, b.Name);
            Assert.GreaterOrEqual(a.min.x, -58.01f, b.Name);
            Assert.LessOrEqual(a.max.z, 66.01f, b.Name);
            Assert.GreaterOrEqual(a.min.z, -66.01f, b.Name);
        }
    }

    [Test]
    public void Backdrop_IsDecorOnly_AndOutsideTheWalls()
    {
        var backdrop = layout.Blocks.Where(x => x.Group == "Backdrop").ToList();
        Assert.Greater(backdrop.Count, 20);
        foreach (BlockSpec b in backdrop)
        {
            Assert.IsFalse(b.Solid, b.Name + " should not have a collider");
            Assert.IsTrue(Mathf.Abs(b.Center.x) > 60f || Mathf.Abs(b.Center.z) > 66f, b.Name + " is inside the arena");
        }
    }

    [Test]
    public void Painted_SiteLettersAndPlantRingsExist_AsDecals()
    {
        foreach (string name in new[] { "LetterA_L", "LetterA_R", "LetterB_L", "LetterB_Top", "PlantRing_A_S", "PlantRing_B_N", "BuyLine_Attack", "BuyLine_Defend" })
        {
            BlockSpec b = layout.Blocks.FirstOrDefault(x => x.Name == name);
            Assert.IsNotNull(b, name + " is missing");
            Assert.IsFalse(b.Solid, name + " must not be solid");
            Assert.Less(b.Size.y, 0.05f, name + " should lie flat on the ground");
        }
    }

    [Test]
    public void Solids_NeverCreateTinyBumpsOrSlivers()
    {
        foreach (BlockSpec b in solids)
        {
            float shortest = Mathf.Min(b.Size.x, Mathf.Min(b.Size.y, b.Size.z));
            Assert.GreaterOrEqual(shortest, 0.1f, b.Name + " is a " + shortest + " m sliver");
        }
    }

    [Test]
    public void Lights_AreWarmLamps_WithinTheMap()
    {
        Assert.GreaterOrEqual(layout.Lights.Count, 8);
        foreach (LightSpec l in layout.Lights)
        {
            Assert.Greater(l.Range, 3f, l.Name);
            Assert.Greater(l.Intensity, 0f, l.Name);
            Assert.GreaterOrEqual(l.Color.r, l.Color.b, l.Name + " should be a warm light");
        }
    }
}
