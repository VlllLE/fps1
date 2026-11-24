using System.Collections.Generic;
using UnityEngine;

namespace CodMovementShooter.Maps.Shipment
{
    public enum BlockKind
    {
        Floor,
        Wall,
        Bounds,
        Container,
        Step,
        Prop,
        Neon,
        Sign,
        Skyline,
        Water,
        Crane,
        Decor,
    }

    public enum SpawnTeam
    {
        TeamA,
        TeamB,
        FreeForAll,
    }

    /// <summary>One axis-aligned box in the Shipment arena (all geometry is cubes).</summary>
    public sealed class BlockSpec
    {
        public string Name;
        public string Group;
        public BlockKind Kind;
        public Vector3 Center;
        public Vector3 Size;
        public string Material;
        public bool Solid;
        public bool Renderable = true;

        public Bounds Bounds => new Bounds(Center, Size);
    }

    public sealed class LightSpec
    {
        public string Name;
        public Vector3 Position;
        public Color Color;
        public float Range;
        public float Intensity;
        public bool ForcePixel;
    }

    public sealed class SpawnSpec
    {
        public string Name;
        public Vector3 Position;
        public float Yaw;
        public SpawnTeam Team;
    }

    /// <summary>Surface heights (above the floor) a player climbs by jumping, bottom to top.</summary>
    public sealed class JumpRoute
    {
        public string Name;
        public float[] Tops;
    }

    public sealed class ShipmentLayoutData
    {
        public readonly List<BlockSpec> Blocks = new List<BlockSpec>();
        public readonly List<LightSpec> Lights = new List<LightSpec>();
        public readonly List<SpawnSpec> Spawns = new List<SpawnSpec>();
        public readonly List<JumpRoute> JumpRoutes = new List<JumpRoute>();
    }

    /// <summary>
    /// Pure-data layout of the Shipment container yard. Shared by the editor builder and tests.
    /// Origin is the arena centre; +X is the dock (east) edge, floor top is y = 0.
    /// </summary>
    public static class ShipmentLayout
    {
        public const float ContainerLength = 6f;
        public const float ContainerWidth = 2.4f;
        public const float ContainerHeight = 2.6f;
        public const float StepLow = 0.9f;
        public const float StepHigh = 1.8f;

        public const float ArenaMinX = -26f;
        public const float ArenaMaxX = 27f;
        public const float ArenaMinZ = -17f;
        public const float ArenaMaxZ = 17f;
        public const float WallHeight = 10f;
        public const float BoundsTop = 18f;
        public const float SpawnHeight = 0.05f;

        public static readonly string[] Hues = { "Cyan", "Magenta", "Amber", "Violet", "Lime" };

        public static Color HueColor(string hue)
        {
            switch (hue)
            {
                case "Cyan": return new Color(0.1f, 0.9f, 1f, 1f);
                case "Magenta": return new Color(1f, 0.15f, 0.75f, 1f);
                case "Amber": return new Color(1f, 0.62f, 0.1f, 1f);
                case "Violet": return new Color(0.55f, 0.3f, 1f, 1f);
                case "Lime": return new Color(0.5f, 1f, 0.2f, 1f);
                default: return Color.white;
            }
        }

        public static string BodyMaterial(string hue) { return "Body_" + hue; }
        public static string TrimMaterial(string hue) { return "Trim_" + hue; }

        public static ShipmentLayoutData Build()
        {
            var b = new Builder();
            b.BuildEnvironment();
            b.BuildContainers();
            b.BuildSteps();
            b.BuildProps();
            b.BuildCrane();
            b.BuildSigns();
            b.BuildLaneMarkings();
            b.BuildSkyline();
            b.BuildLights();
            b.BuildSpawns();
            return b.Data;
        }

        private sealed class Builder
        {
            public readonly ShipmentLayoutData Data = new ShipmentLayoutData();
            private int hueCounter;

            private string NextHue()
            {
                string hue = Hues[(hueCounter * 2) % Hues.Length];
                hueCounter++;
                return hue;
            }

            private BlockSpec Add(string name, string group, BlockKind kind, Vector3 center, Vector3 size,
                string material, bool solid, bool renderable = true)
            {
                var spec = new BlockSpec
                {
                    Name = name,
                    Group = group,
                    Kind = kind,
                    Center = center,
                    Size = size,
                    Material = material,
                    Solid = solid,
                    Renderable = renderable,
                };
                Data.Blocks.Add(spec);
                return spec;
            }

            private void Neon(string name, Vector3 center, Vector3 size, string hue)
            {
                Add(name, "Neon", BlockKind.Neon, center, size, TrimMaterial(hue), false);
            }

            public void BuildEnvironment()
            {
                float width = ArenaMaxX - ArenaMinX;

                Add("Floor", "Environment", BlockKind.Floor,
                    new Vector3(0.25f, -1.5f, 0f), new Vector3(54.5f, 3f, 36f), "Floor", true);

                float wallY = WallHeight * 0.5f;
                float capHeight = BoundsTop - WallHeight;
                float capY = WallHeight + capHeight * 0.5f;
                float nsCenterX = (ArenaMinX + 27.5f) * 0.5f;
                float nsLength = 27.5f - ArenaMinX;

                Add("Wall_North", "Environment", BlockKind.Wall,
                    new Vector3(nsCenterX, wallY, 17.5f), new Vector3(nsLength, WallHeight, 1f), "WallPanel", true);
                Add("Wall_South", "Environment", BlockKind.Wall,
                    new Vector3(nsCenterX, wallY, -17.5f), new Vector3(nsLength, WallHeight, 1f), "WallPanel", true);
                Add("Wall_West", "Environment", BlockKind.Wall,
                    new Vector3(-26.5f, wallY, 0f), new Vector3(1f, WallHeight, 36f), "WallPanel", true);

                Add("BoundsCap_North", "Environment", BlockKind.Bounds,
                    new Vector3(nsCenterX, capY, 17.5f), new Vector3(nsLength, capHeight, 1f), null, true, false);
                Add("BoundsCap_South", "Environment", BlockKind.Bounds,
                    new Vector3(nsCenterX, capY, -17.5f), new Vector3(nsLength, capHeight, 1f), null, true, false);
                Add("BoundsCap_West", "Environment", BlockKind.Bounds,
                    new Vector3(-26.5f, capY, 0f), new Vector3(1f, capHeight, 36f), null, true, false);

                // Dock edge: invisible full-height blocker behind a translucent energy field.
                Add("Bounds_East", "Environment", BlockKind.Bounds,
                    new Vector3(27.1f, BoundsTop * 0.5f, 0f), new Vector3(0.4f, BoundsTop, 34f), null, true, false);
                Add("DockField", "Environment", BlockKind.Decor,
                    new Vector3(26.95f, 3f, 0f), new Vector3(0.06f, 6f, 34f), "EnergyField", false);
                Add("Ceiling", "Environment", BlockKind.Bounds,
                    new Vector3(0.25f, BoundsTop + 0.5f, 0f), new Vector3(width + 3f, 1f, 38f), null, true, false);

                Add("Water", "Environment", BlockKind.Water,
                    new Vector3(102f, -2.6f, 0f), new Vector3(150f, 0.8f, 220f), "Water", false);

                Add("QuayKerb", "Neon", BlockKind.Neon,
                    new Vector3(26.4f, 0.011f, 0f), new Vector3(0.5f, 0.02f, 33.6f), "Hazard", false);
            }

            public void BuildContainers()
            {
                int index = 0;

                float[] rowAX = { -18f, -9f, 0f, 9f, 18f };
                foreach (float z in new[] { 5.6f, -5.6f })
                {
                    foreach (float x in rowAX)
                    {
                        bool stacked = (x == -18f && z > 0f) || (x == 18f && z < 0f);
                        AddContainer("Container_A" + index, x, z, 0, true, NextHue());
                        if (stacked)
                            AddContainer("Container_A" + index + "_Upper", x, z, 1, true, NextHue());
                        index++;
                    }
                }

                float[] rowBX = { -13.5f, -4.5f, 4.5f, 13.5f };
                foreach (float z in new[] { 10.4f, -10.4f })
                {
                    foreach (float x in rowBX)
                    {
                        bool stacked = (x == -4.5f && z > 0f) || (x == 4.5f && z < 0f);
                        AddContainer("Container_B" + index, x, z, 0, true, NextHue());
                        if (stacked)
                            AddContainer("Container_B" + index + "_Upper", x, z, 1, true, NextHue());
                        index++;
                    }
                }

                foreach (float x in new[] { -22.5f, 22.5f })
                {
                    foreach (float z in new[] { 12f, -12f })
                    {
                        AddContainer("Container_End" + index, x, z, 0, false, NextHue());
                        index++;
                    }
                }
            }

            private void AddContainer(string name, float x, float z, int level, bool alongX, string hue,
                bool solid = true, float baseY = 0f)
            {
                float y = baseY + ContainerHeight * (level + 0.5f);
                Vector3 size = alongX
                    ? new Vector3(ContainerLength, ContainerHeight, ContainerWidth)
                    : new Vector3(ContainerWidth, ContainerHeight, ContainerLength);
                Add(name, "Containers", BlockKind.Container, new Vector3(x, y, z), size, BodyMaterial(hue), solid);

                float topY = y + ContainerHeight * 0.5f - 0.2f;
                float lowY = y - ContainerHeight * 0.5f + 0.22f;
                float endY = y;
                float halfWidth = ContainerWidth * 0.5f + 0.025f;
                float halfLength = ContainerLength * 0.5f + 0.025f;

                if (alongX)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Neon(name + "_TopStrip" + side, new Vector3(x, topY, z + side * halfWidth),
                            new Vector3(ContainerLength - 0.4f, 0.07f, 0.05f), hue);
                        Neon(name + "_BaseStrip" + side, new Vector3(x, lowY, z + side * halfWidth),
                            new Vector3(ContainerLength - 0.4f, 0.05f, 0.05f), hue);
                        Neon(name + "_EndStrip" + side, new Vector3(x + side * halfLength, endY, z),
                            new Vector3(0.05f, ContainerHeight - 0.6f, 0.08f), hue);
                    }
                }
                else
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Neon(name + "_TopStrip" + side, new Vector3(x + side * halfWidth, topY, z),
                            new Vector3(0.05f, 0.07f, ContainerLength - 0.4f), hue);
                        Neon(name + "_BaseStrip" + side, new Vector3(x + side * halfWidth, lowY, z),
                            new Vector3(0.05f, 0.05f, ContainerLength - 0.4f), hue);
                        Neon(name + "_EndStrip" + side, new Vector3(x, endY, z + side * halfLength),
                            new Vector3(0.08f, ContainerHeight - 0.6f, 0.05f), hue);
                    }
                }
            }

            public void BuildSteps()
            {
                // Cargo steps: ground -> 0.9 -> 1.8 -> container top 2.6 (each rise within a standing jump).
                AddStepPair("Steps_NW", -14.5f, 12.4f, 14f);
                AddStepPair("Steps_SE", 14.5f, -12.4f, -14f);
                AddStepPair("Steps_CentralN", 9f, 3.6f, 2f);
                AddStepPair("Steps_CentralS", -9f, -3.6f, -2f);

                float[] route = { StepLow, StepHigh, ContainerHeight };
                Data.JumpRoutes.Add(new JumpRoute { Name = "Steps_NW", Tops = route });
                Data.JumpRoutes.Add(new JumpRoute { Name = "Steps_SE", Tops = route });
                Data.JumpRoutes.Add(new JumpRoute { Name = "Steps_CentralN", Tops = route });
                Data.JumpRoutes.Add(new JumpRoute { Name = "Steps_CentralS", Tops = route });
            }

            private void AddStepPair(string name, float x, float highZ, float lowZ)
            {
                Add(name + "_High", "Props", BlockKind.Step,
                    new Vector3(x, StepHigh * 0.5f, highZ), new Vector3(1.6f, StepHigh, 1.6f), "Hazard", true);
                Add(name + "_Low", "Props", BlockKind.Step,
                    new Vector3(x, StepLow * 0.5f, lowZ), new Vector3(1.6f, StepLow, 1.6f), "Hazard", true);
            }

            public void BuildProps()
            {
                // Bridges between container tops (flush with the 2.6 m roofs).
                Add("Bridge_South", "Props", BlockKind.Prop,
                    new Vector3(-9f, ContainerHeight - 0.1f, -10.4f), new Vector3(3f, 0.2f, 1.4f), "WallPanel", true);
                Add("Bridge_North", "Props", BlockKind.Prop,
                    new Vector3(9f, ContainerHeight - 0.1f, 10.4f), new Vector3(3f, 0.2f, 1.4f), "WallPanel", true);
                Neon("Bridge_South_Edge", new Vector3(-9f, ContainerHeight + 0.02f, -9.72f), new Vector3(3f, 0.04f, 0.05f), "Cyan");
                Neon("Bridge_North_Edge", new Vector3(9f, ContainerHeight + 0.02f, 9.72f), new Vector3(3f, 0.04f, 0.05f), "Magenta");

                // Low slide/crouch cover in the central lane.
                foreach (float x in new[] { -14f, 14f })
                {
                    Add("LowBarrier_" + x, "Props", BlockKind.Prop,
                        new Vector3(x, 0.55f, 0f), new Vector3(0.6f, 1.1f, 3f), "Barrier", true);
                    Neon("LowBarrier_" + x + "_Strip", new Vector3(x, 1.12f, 0f), new Vector3(0.64f, 0.05f, 3.04f), "Cyan");
                }
            }

            public void BuildCrane()
            {
                foreach (float z in new[] { -6f, 6f })
                {
                    Add("CraneLeg_" + z, "Crane", BlockKind.Crane,
                        new Vector3(24f, 5f, z), new Vector3(1.2f, 10f, 1.2f), "CraneSteel", true);
                    Neon("CraneLeg_" + z + "_Strip", new Vector3(23.38f, 5f, z), new Vector3(0.04f, 9.4f, 0.1f), "Amber");
                }

                Add("CraneBeam", "Crane", BlockKind.Crane,
                    new Vector3(24f, 10.6f, 0f), new Vector3(1.4f, 1.2f, 14.4f), "CraneSteel", false);
                Add("CraneBoom", "Crane", BlockKind.Crane,
                    new Vector3(36f, 10.6f, 0f), new Vector3(24f, 0.9f, 0.9f), "CraneSteel", false);
                Add("CraneCab", "Crane", BlockKind.Crane,
                    new Vector3(24f, 12.2f, 0f), new Vector3(3f, 2f, 3f), "CraneSteel", false);
                Neon("CraneCab_Window", new Vector3(25.52f, 12.4f, 0f), new Vector3(0.05f, 0.8f, 2.4f), "Cyan");
                Neon("CraneBeacon", new Vector3(24f, 13.5f, 0f), new Vector3(0.5f, 0.5f, 0.5f), "Magenta");
                Neon("CraneBoom_Strip", new Vector3(36f, 10.1f, 0.48f), new Vector3(23f, 0.06f, 0.05f), "Amber");

                Add("CraneCable_A", "Crane", BlockKind.Crane,
                    new Vector3(36.2f, 8.95f, 0f), new Vector3(0.06f, 2.3f, 0.06f), "CraneSteel", false);
                Add("CraneCable_B", "Crane", BlockKind.Crane,
                    new Vector3(39.8f, 8.95f, 0f), new Vector3(0.06f, 2.3f, 0.06f), "CraneSteel", false);
                AddContainer("Container_Hanging", 38f, 0f, 0, true, "Magenta", false, 5.2f);
            }

            public void BuildSigns()
            {
                string[] signs = { "Sign_Chevron", "Sign_Barcode", "Sign_Hazard", "Sign_Rings" };
                float[] xs = { -18f, -6f, 6f, 18f };
                int i = 0;
                foreach (float x in xs)
                {
                    Add("HoloSign_N" + i, "Signs", BlockKind.Sign,
                        new Vector3(x, 5.2f, 16.95f), new Vector3(7f, 2.4f, 0.04f), signs[i % signs.Length], false);
                    Add("HoloSign_S" + i, "Signs", BlockKind.Sign,
                        new Vector3(x, 5.2f, -16.95f), new Vector3(7f, 2.4f, 0.04f), signs[(i + 2) % signs.Length], false);
                    i++;
                }

                Add("HoloSign_W0", "Signs", BlockKind.Sign,
                    new Vector3(-25.95f, 5.2f, -8f), new Vector3(0.04f, 2.4f, 7f), "Sign_Barcode", false);
                Add("HoloSign_W1", "Signs", BlockKind.Sign,
                    new Vector3(-25.95f, 5.2f, 8f), new Vector3(0.04f, 2.4f, 7f), "Sign_Chevron", false);

                Add("HoloSign_Center", "Signs", BlockKind.Sign,
                    new Vector3(0f, 8.5f, 0f), new Vector3(10f, 3f, 0.04f), "Sign_Rings", false);
                Add("HoloSign_Dock", "Signs", BlockKind.Sign,
                    new Vector3(24f, 8.4f, 0f), new Vector3(0.04f, 2f, 9f), "Sign_Hazard", false);
            }

            public void BuildLaneMarkings()
            {
                foreach (float z in new[] { -3.9f, 3.9f })
                {
                    Neon("LaneLine_" + z, new Vector3(0.5f, 0.011f, z), new Vector3(46f, 0.02f, 0.12f), z < 0f ? "Magenta" : "Cyan");
                }
                foreach (float x in new[] { -22f, 22f })
                {
                    Neon("SpawnPad_" + x, new Vector3(x, 0.011f, 0f), new Vector3(0.12f, 0.02f, 8f), x < 0f ? "Cyan" : "Magenta");
                }
            }

            public void BuildSkyline()
            {
                var rng = new System.Random(20260);
                int index = 0;

                for (float x = -96f; x <= 96f; x += rng.Next(12, 18))
                {
                    AddBuilding(rng, index++, x, 36f + rng.Next(0, 36));
                    AddBuilding(rng, index++, x + 5f, -(36f + rng.Next(0, 36)));
                }

                for (float z = -26f; z <= 26f; z += rng.Next(12, 18))
                    AddBuilding(rng, index++, -(40f + rng.Next(0, 36)), z);

                for (float z = -90f; z <= 90f; z += rng.Next(12, 18))
                    AddBuilding(rng, index++, 70f + rng.Next(0, 50), z);
            }

            private void AddBuilding(System.Random rng, int index, float x, float z)
            {
                float w = 8f + (float)rng.NextDouble() * 8f;
                float d = 8f + (float)rng.NextDouble() * 8f;
                float h = 24f + (float)rng.NextDouble() * 70f;
                string mat = index % 2 == 0 ? "Skyline_A" : "Skyline_B";
                Add("Building_" + index, "Skyline", BlockKind.Skyline,
                    new Vector3(x, h * 0.5f - 3f, z), new Vector3(w, h, d), mat, false);
                string hue = Hues[index % Hues.Length];
                Add("Building_" + index + "_Cap", "Skyline", BlockKind.Skyline,
                    new Vector3(x, h - 3f + 0.4f, z), new Vector3(w + 0.2f, 0.8f, d + 0.2f), TrimMaterial(hue), false);
            }

            public void BuildLights()
            {
                AddLight("Light_Center_Cyan", new Vector3(-6f, 3.6f, 0f), "Cyan", 16f, 1.6f, true);
                AddLight("Light_Center_Magenta", new Vector3(6f, 3.6f, 0f), "Magenta", 16f, 1.6f, true);
                AddLight("Light_West_Amber", new Vector3(-20f, 3.6f, 0f), "Amber", 14f, 1.3f, false);
                AddLight("Light_Dock_Amber", new Vector3(21f, 3.6f, 0f), "Amber", 14f, 1.3f, false);
                AddLight("Light_LaneN_Violet", new Vector3(-9f, 3.6f, 8f), "Violet", 12f, 1.2f, false);
                AddLight("Light_LaneS_Violet", new Vector3(9f, 3.6f, -8f), "Violet", 12f, 1.2f, false);
                AddLight("Light_OuterN_Lime", new Vector3(0f, 3.6f, 14.5f), "Lime", 13f, 1.1f, false);
                AddLight("Light_OuterS_Lime", new Vector3(0f, 3.6f, -14.5f), "Lime", 13f, 1.1f, false);
                AddLight("Light_Crane_Beacon", new Vector3(24f, 13.8f, 0f), "Magenta", 18f, 1.4f, false);
            }

            private void AddLight(string name, Vector3 position, string hue, float range, float intensity, bool forcePixel)
            {
                Data.Lights.Add(new LightSpec
                {
                    Name = name,
                    Position = position,
                    Color = HueColor(hue),
                    Range = range,
                    Intensity = intensity,
                    ForcePixel = forcePixel,
                });
                Neon(name + "_Lamp", position, new Vector3(0.5f, 0.12f, 0.5f), hue);
            }

            public void BuildSpawns()
            {
                AddSpawn("Spawn_A1", -24f, -3f, 90f, SpawnTeam.TeamA);
                AddSpawn("Spawn_A2", -24f, 3f, 90f, SpawnTeam.TeamA);
                AddSpawn("Spawn_A3", -22.5f, 0f, 90f, SpawnTeam.TeamA);
                AddSpawn("Spawn_B1", 23f, -3f, 270f, SpawnTeam.TeamB);
                AddSpawn("Spawn_B2", 23f, 3f, 270f, SpawnTeam.TeamB);
                AddSpawn("Spawn_B3", 24.5f, 0f, 270f, SpawnTeam.TeamB);
                AddSpawn("Spawn_FFA_North", 0f, 14.5f, 180f, SpawnTeam.FreeForAll);
                AddSpawn("Spawn_FFA_South", 0f, -14.5f, 0f, SpawnTeam.FreeForAll);
                AddSpawn("Spawn_FFA_LaneN", -9f, 8f, 90f, SpawnTeam.FreeForAll);
                AddSpawn("Spawn_FFA_LaneS", 9f, -8f, 270f, SpawnTeam.FreeForAll);
                AddSpawn("Spawn_FFA_OuterNW", -18f, 14.5f, 90f, SpawnTeam.FreeForAll);
                AddSpawn("Spawn_FFA_OuterSE", 18f, -14.5f, 270f, SpawnTeam.FreeForAll);
            }

            private void AddSpawn(string name, float x, float z, float yaw, SpawnTeam team)
            {
                Data.Spawns.Add(new SpawnSpec
                {
                    Name = name,
                    Position = new Vector3(x, SpawnHeight, z),
                    Yaw = yaw,
                    Team = team,
                });
            }
        }
    }
}
