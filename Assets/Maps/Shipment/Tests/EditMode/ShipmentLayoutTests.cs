using System.Collections.Generic;
using CodMovementShooter.Maps.Shipment;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pure-data contract tests for the Shipment layout (no scene required). They guard the properties that make
/// the map playable with the movement kit: reachable raised tops, traversable lanes, clear spawns.
/// </summary>
public class ShipmentLayoutTests
{
    private ShipmentLayoutData layout;

    [SetUp]
    public void SetUp()
    {
        layout = ShipmentLayout.Build();
    }

    private IEnumerable<BlockSpec> SolidBlocks()
    {
        foreach (var block in layout.Blocks)
        {
            if (block.Solid)
                yield return block;
        }
    }

    private static float Overlap(float minA, float maxA, float minB, float maxB)
    {
        return Mathf.Min(maxA, maxB) - Mathf.Max(minA, minB);
    }

    [Test]
    public void Layout_IsDeterministic()
    {
        var other = ShipmentLayout.Build();
        Assert.AreEqual(layout.Blocks.Count, other.Blocks.Count);
        for (int i = 0; i < layout.Blocks.Count; i++)
        {
            Assert.AreEqual(layout.Blocks[i].Name, other.Blocks[i].Name);
            Assert.AreEqual(layout.Blocks[i].Center, other.Blocks[i].Center);
        }
    }

    [Test]
    public void Layout_BlockNamesAreUnique()
    {
        var seen = new HashSet<string>();
        foreach (var block in layout.Blocks)
            Assert.IsTrue(seen.Add(block.Name), "Duplicate block name: " + block.Name);
    }

    [Test]
    public void Layout_HasContainerYardFeatures()
    {
        int containers = 0;
        int stacked = 0;
        foreach (var block in layout.Blocks)
        {
            if (block.Kind != BlockKind.Container)
                continue;
            containers++;
            if (block.Center.y > ShipmentLayout.ContainerHeight)
                stacked++;
        }

        Assert.GreaterOrEqual(containers, 20, "Shipment should be dense with containers");
        Assert.GreaterOrEqual(stacked, 2, "Need stacked containers for vertical wall surfaces");
        Assert.GreaterOrEqual(layout.JumpRoutes.Count, 2, "Need jump routes onto raised container tops");
    }

    [Test]
    public void Layout_HasCentralOpenLane()
    {
        // No container may sit inside the central lane corridor along X.
        var lane = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(40f, 1.8f, 7.0f));
        foreach (var block in SolidBlocks())
        {
            if (block.Kind != BlockKind.Container)
                continue;
            Assert.IsFalse(block.Bounds.Intersects(lane), block.Name + " blocks the central lane");
        }
    }

    [Test]
    public void Layout_SolidBlocksDoNotOverlap()
    {
        var solids = new List<BlockSpec>(SolidBlocks());
        for (int i = 0; i < solids.Count; i++)
        {
            for (int j = i + 1; j < solids.Count; j++)
            {
                var a = solids[i].Bounds;
                var b = solids[j].Bounds;
                float ox = Overlap(a.min.x, a.max.x, b.min.x, b.max.x);
                float oy = Overlap(a.min.y, a.max.y, b.min.y, b.max.y);
                float oz = Overlap(a.min.z, a.max.z, b.min.z, b.max.z);
                Assert.IsFalse(ox > 0.01f && oy > 0.01f && oz > 0.01f,
                    solids[i].Name + " overlaps " + solids[j].Name);
            }
        }
    }

    [Test]
    public void Layout_NoSliverGapsBetweenWalkableObstacles()
    {
        // Gaps narrower than the player capsule (0.8 m plus margin) would trap or snag the character.
        const float minGap = 1.2f;
        var obstacles = new List<BlockSpec>();
        foreach (var block in SolidBlocks())
        {
            if (block.Kind == BlockKind.Container || block.Kind == BlockKind.Step
                || block.Kind == BlockKind.Prop || block.Kind == BlockKind.Crane)
                obstacles.Add(block);
        }

        for (int i = 0; i < obstacles.Count; i++)
        {
            for (int j = i + 1; j < obstacles.Count; j++)
            {
                var a = obstacles[i].Bounds;
                var b = obstacles[j].Bounds;
                if (Overlap(a.min.y, a.max.y, b.min.y, b.max.y) < 0.5f)
                    continue;

                float ox = Overlap(a.min.x, a.max.x, b.min.x, b.max.x);
                float oz = Overlap(a.min.z, a.max.z, b.min.z, b.max.z);
                if (oz > 0.5f)
                {
                    float gapX = -ox;
                    Assert.IsFalse(gapX > 0.02f && gapX < minGap,
                        obstacles[i].Name + " / " + obstacles[j].Name + " leave a " + gapX + " m sliver on X");
                }

                if (ox > 0.5f)
                {
                    float gapZ = -oz;
                    Assert.IsFalse(gapZ > 0.02f && gapZ < minGap,
                        obstacles[i].Name + " / " + obstacles[j].Name + " leave a " + gapZ + " m sliver on Z");
                }
            }
        }
    }

    [Test]
    public void Layout_SpawnsAreClearOfSolidGeometry()
    {
        Assert.GreaterOrEqual(layout.Spawns.Count, 6);

        int teamA = 0;
        int teamB = 0;
        foreach (var spawn in layout.Spawns)
        {
            if (spawn.Team == SpawnTeam.TeamA) teamA++;
            if (spawn.Team == SpawnTeam.TeamB) teamB++;

            var capsule = new Bounds(spawn.Position + Vector3.up * 1.05f, new Vector3(1f, 2.1f, 1f));
            foreach (var block in SolidBlocks())
            {
                Assert.IsFalse(block.Bounds.Intersects(capsule), spawn.Name + " is inside " + block.Name);
            }
        }

        Assert.GreaterOrEqual(teamA, 2);
        Assert.GreaterOrEqual(teamB, 2);
    }

    [Test]
    public void Layout_SpawnsStandOnFloor()
    {
        foreach (var spawn in layout.Spawns)
        {
            Assert.That(spawn.Position.x, Is.InRange(ShipmentLayout.ArenaMinX, ShipmentLayout.ArenaMaxX));
            Assert.That(spawn.Position.z, Is.InRange(ShipmentLayout.ArenaMinZ, ShipmentLayout.ArenaMaxZ));
            Assert.That(spawn.Position.y, Is.InRange(0f, 0.3f));
        }
    }

    [Test]
    public void Layout_JumpRoutesAreReachableWithDefaultMovement()
    {
        var config = ScriptableObject.CreateInstance<MovementConfig>();
        try
        {
            float gravity = Physics.gravity.magnitude * config.gravityMultiplier;
            float apex = config.jumpForce * config.jumpForce / (2f * gravity);
            float maxRise = apex * 0.85f;

            foreach (var route in layout.JumpRoutes)
            {
                float previous = 0f;
                foreach (float top in route.Tops)
                {
                    Assert.LessOrEqual(top - previous, maxRise,
                        route.Name + " rise " + (top - previous) + " exceeds jump apex " + apex);
                    previous = top;
                }

                Assert.AreEqual(ShipmentLayout.ContainerHeight, previous, 0.001f,
                    route.Name + " should end on a container roof");
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Layout_AllGeometryFitsInsideBounds()
    {
        foreach (var block in SolidBlocks())
        {
            if (block.Kind == BlockKind.Floor || block.Kind == BlockKind.Wall || block.Kind == BlockKind.Bounds)
                continue;

            var bounds = block.Bounds;
            Assert.GreaterOrEqual(bounds.min.x, ShipmentLayout.ArenaMinX - 0.01f, block.Name);
            Assert.LessOrEqual(bounds.max.x, ShipmentLayout.ArenaMaxX + 0.01f, block.Name);
            Assert.GreaterOrEqual(bounds.min.z, ShipmentLayout.ArenaMinZ - 0.01f, block.Name);
            Assert.LessOrEqual(bounds.max.z, ShipmentLayout.ArenaMaxZ + 0.01f, block.Name);
        }
    }
}
