using System.Collections.Generic;
using UnityEngine;

namespace FpsMaps.Bazaar
{
    /// <summary>
    /// Pure-data layout of the Bazaar map: a sunlit desert market town built for a Counter-Strike / Valorant style
    /// bomb mode (metres, origin at the map centre, street level y = 0, +Z = north, attackers in the south).
    ///
    /// Three ways in: A Long (west lane, the only long sightline), Mid (street, courtyard, A Short and the B
    /// connector) and B Tunnels (covered, east). Two sites, A (north-west) and B (north-east), each with a default room,
    /// an elevated "heaven" deck, and a "hell" (a room under A's deck, a sunken pit at B). Defenders rotate through the
    /// central hub or through their spawn yard. Everything is axis aligned boxes: the editor builder turns the data into a
    /// scene, the tests read the same data.
    /// </summary>
    public static class BazaarLayout
    {
        public const string SceneName = "Bazaar";
        public const string ScenePath = "Assets/Scenes/Bazaar.unity";
        public const string RootFolder = "Assets/Maps/Bazaar";

        public const float XW = -55f;
        public const float XE = 55f;
        public const float ZS = -62f;
        public const float ZN = 62f;
        public const float WallHeight = 9f;
        public const float MassHeight = 5f;
        public const float HeavenY = 3f;
        public const float PitDepth = 1.5f;
        public const float StairRise = 0.2f;
        public const float StairRun = 0.3f;
        public const int StairSteps = 15;
        public const int PitSteps = 8;

        public static readonly string[] RouteNames =
        {
            "ALong", "AShort", "Mid", "BTunnels", "BConnector", "ASite", "BSite", "Rotate"
        };

        public static BazaarLayoutData Create()
        {
            var gen = new Generator();
            gen.Build();
            return gen.Data;
        }

        private struct Rect
        {
            public readonly float X0;
            public readonly float X1;
            public readonly float Z0;
            public readonly float Z1;

            public Rect(float x0, float x1, float z0, float z1)
            {
                X0 = x0;
                X1 = x1;
                Z0 = z0;
                Z1 = z1;
            }
        }

        private struct Op
        {
            public readonly float A;
            public readonly float B;
            public readonly float Yb;
            public readonly float Yt;

            public Op(float a, float b, float yb, float yt)
            {
                A = a;
                B = b;
                Yb = yb;
                Yt = yt;
            }
        }

        private static readonly Rect PitRect = new Rect(28f, 38f, 24f, 34f);

        private sealed class Generator
        {
            public readonly BazaarLayoutData Data = new BazaarLayoutData();

            private readonly Dictionary<string, int> nameCounts = new Dictionary<string, int>();
            private string group = "Map";

            public void Build()
            {
                Ground();
                Paving();
                Perimeter();
                Masses();
                Walls();
                ASite();
                BSite();
                Forecourt();
                MidStreetAndCourtyard();
                Hub();
                Yards();
                Palms();
                Decor();
                Markers();
                Lights();
                Backdrop();
                Spawns();
                Waypoints();
                Areas();
                Zones();
                Routes();
            }

            #region Primitive helpers

            private void G(string name)
            {
                group = name;
            }

            private string Unique(string name)
            {
                if (!nameCounts.TryGetValue(name, out int count))
                {
                    nameCounts[name] = 1;
                    return name;
                }

                nameCounts[name] = count + 1;
                return name + "_" + count;
            }

            private BlockSpec Box(string name, BazaarSurface s, float x0, float x1, float y0, float y1, float z0, float z1, bool solid = true)
            {
                float ax = Mathf.Min(x0, x1), bx = Mathf.Max(x0, x1);
                float ay = Mathf.Min(y0, y1), by = Mathf.Max(y0, y1);
                float az = Mathf.Min(z0, z1), bz = Mathf.Max(z0, z1);
                if (bx - ax < 0.001f || by - ay < 0.001f || bz - az < 0.001f)
                    return null;

                var b = new BlockSpec
                {
                    Name = Unique(name),
                    Group = group,
                    Surface = s,
                    Center = new Vector3((ax + bx) * 0.5f, (ay + by) * 0.5f, (az + bz) * 0.5f),
                    Size = new Vector3(bx - ax, by - ay, bz - az),
                    Solid = solid
                };
                Data.Blocks.Add(b);
                return b;
            }

            private void AddOpening(string name, Vector3 center, float width, float height, float bottomY, bool isDoor)
            {
                Data.Openings.Add(new OpeningSpec
                {
                    Name = name,
                    Center = center,
                    Width = width,
                    Height = height,
                    BottomY = bottomY,
                    IsDoor = isDoor
                });
            }

            /// <summary>Wall along X at z with openings; an opening that reaches the wall top leaves no lintel.</summary>
            private void WallX(string name, BazaarSurface s, float z, float x0, float x1, float y0, float y1, float t, params Op[] ops)
            {
                var list = new List<Op>(ops);
                list.Sort((p, q) => p.A.CompareTo(q.A));
                float cur = x0;
                int n = 0;
                foreach (Op op in list)
                {
                    if (op.A > cur + 0.001f)
                        Box(name + "_P" + n, s, cur, op.A, y0, y1, z - t * 0.5f, z + t * 0.5f);
                    if (op.Yb > y0 + 0.001f)
                        Box(name + "_Sill" + n, s, op.A, op.B, y0, op.Yb, z - t * 0.5f, z + t * 0.5f);
                    if (op.Yt < y1 - 0.001f)
                        Box(name + "_Lintel" + n, s, op.A, op.B, op.Yt, y1, z - t * 0.5f, z + t * 0.5f);
                    AddOpening(name + "_Op" + n, new Vector3((op.A + op.B) * 0.5f, (op.Yb + op.Yt) * 0.5f, z), op.B - op.A, op.Yt - op.Yb, op.Yb, op.Yb <= y0 + 0.05f);
                    cur = op.B;
                    n++;
                }

                if (x1 > cur + 0.001f)
                    Box(name + "_P" + n, s, cur, x1, y0, y1, z - t * 0.5f, z + t * 0.5f);
            }

            /// <summary>Wall along Z at x with openings.</summary>
            private void WallZ(string name, BazaarSurface s, float x, float z0, float z1, float y0, float y1, float t, params Op[] ops)
            {
                var list = new List<Op>(ops);
                list.Sort((p, q) => p.A.CompareTo(q.A));
                float cur = z0;
                int n = 0;
                foreach (Op op in list)
                {
                    if (op.A > cur + 0.001f)
                        Box(name + "_P" + n, s, x - t * 0.5f, x + t * 0.5f, y0, y1, cur, op.A);
                    if (op.Yb > y0 + 0.001f)
                        Box(name + "_Sill" + n, s, x - t * 0.5f, x + t * 0.5f, y0, op.Yb, op.A, op.B);
                    if (op.Yt < y1 - 0.001f)
                        Box(name + "_Lintel" + n, s, x - t * 0.5f, x + t * 0.5f, op.Yt, y1, op.A, op.B);
                    AddOpening(name + "_Op" + n, new Vector3(x, (op.Yb + op.Yt) * 0.5f, (op.A + op.B) * 0.5f), op.B - op.A, op.Yt - op.Yb, op.Yb, op.Yb <= y0 + 0.05f);
                    cur = op.B;
                    n++;
                }

                if (z1 > cur + 0.001f)
                    Box(name + "_P" + n, s, x - t * 0.5f, x + t * 0.5f, y0, y1, cur, z1);
            }

            private static List<Rect> Subtract(Rect r, Rect h)
            {
                var res = new List<Rect>();
                if (h.X1 <= r.X0 || h.X0 >= r.X1 || h.Z1 <= r.Z0 || h.Z0 >= r.Z1)
                {
                    res.Add(r);
                    return res;
                }

                if (h.X0 > r.X0)
                    res.Add(new Rect(r.X0, h.X0, r.Z0, r.Z1));
                if (h.X1 < r.X1)
                    res.Add(new Rect(h.X1, r.X1, r.Z0, r.Z1));
                float mx0 = Mathf.Max(r.X0, h.X0), mx1 = Mathf.Min(r.X1, h.X1);
                if (h.Z0 > r.Z0)
                    res.Add(new Rect(mx0, mx1, r.Z0, h.Z0));
                if (h.Z1 < r.Z1)
                    res.Add(new Rect(mx0, mx1, h.Z1, r.Z1));
                return res;
            }

            private static List<Rect> SubtractAll(Rect r, params Rect[] holes)
            {
                var pieces = new List<Rect> { r };
                foreach (Rect hole in holes)
                {
                    var next = new List<Rect>();
                    foreach (Rect piece in pieces)
                        next.AddRange(Subtract(piece, hole));
                    pieces = next;
                }

                return pieces;
            }

            /// <summary>Solid building mass with a flat roof (top at <see cref="MassHeight"/>), minus the given holes.</summary>
            private void Mass(string name, BazaarSurface body, Rect r, params Rect[] holes)
            {
                int n = 0;
                foreach (Rect p in SubtractAll(r, holes))
                {
                    Box(name + "_Body" + n, body, p.X0, p.X1, 0f, MassHeight - 0.25f, p.Z0, p.Z1);
                    Box(name + "_Roof" + n, BazaarSurface.RoofTile, p.X0, p.X1, MassHeight - 0.25f, MassHeight, p.Z0, p.Z1);
                    n++;
                }
            }

            private void Paint(string name, BazaarSurface s, float x0, float x1, float z0, float z1, float y0 = 0f, float h = 0.025f)
            {
                Box(name, s, x0, x1, y0, y0 + h, z0, z1, false);
            }

            /// <summary>Stairs: step i occupies [start + dir*i*run, ...] and tops out at baseY + (i + 1) * rise; the foot is at <paramref name="start"/>.</summary>
            private void Flight(string name, BazaarSurface s, bool alongX, float start, int dir, float c0, float c1, float floorY, float baseY, float rise, int steps)
            {
                for (int i = 0; i < steps; i++)
                {
                    float a0 = start + dir * i * StairRun;
                    float a1 = start + dir * (i + 1) * StairRun;
                    float top = baseY + (i + 1) * rise;
                    if (alongX)
                        Box(name + "_S" + i.ToString("00"), s, a0, a1, floorY, top, c0, c1);
                    else
                        Box(name + "_S" + i.ToString("00"), s, c0, c1, floorY, top, a0, a1);
                }

                float mid = (c0 + c1) * 0.5f;
                float end = start + dir * steps * StairRun;
                Data.Flights.Add(new FlightSpec
                {
                    Name = name,
                    Foot = alongX ? new Vector3(start, baseY, mid) : new Vector3(mid, baseY, start),
                    Top = alongX ? new Vector3(end, baseY + steps * rise, mid) : new Vector3(mid, baseY + steps * rise, end),
                    Width = c1 - c0,
                    Rise = rise,
                    Run = StairRun,
                    Steps = steps
                });
            }

            private void Crate(string name, float x, float z, float size = 1.1f, float y0 = 0f)
            {
                Box(name, BazaarSurface.Crate, x - size * 0.5f, x + size * 0.5f, y0, y0 + size, z - size * 0.5f, z + size * 0.5f);
            }

            private void CrateStack(string name, float x, float z)
            {
                Box(name + "_Low", BazaarSurface.Crate, x - 1f, x + 1f, 0f, 1.1f, z - 1f, z + 1f);
                Box(name + "_High", BazaarSurface.Crate, x - 0.6f, x + 0.6f, 1.1f, 2.2f, z - 0.6f, z + 0.6f);
            }

            private void Pillar(string name, float x, float z, float size = 1.4f, float height = 3.2f, BazaarSurface s = BazaarSurface.Limestone)
            {
                Box(name, s, x - size * 0.5f, x + size * 0.5f, 0f, height, z - size * 0.5f, z + size * 0.5f);
            }

            /// <summary>Car along X (alongX) or Z: body 1.5 m, cabin to 2.2 m.</summary>
            private void Car(string name, BazaarSurface paint, float cx, float cz, bool alongX)
            {
                float lx = alongX ? 2.2f : 0.9f;
                float lz = alongX ? 0.9f : 2.2f;
                Box(name + "_Body", paint, cx - lx, cx + lx, 0f, 1.5f, cz - lz, cz + lz);
                float cabX = alongX ? 1.0f : 0.8f;
                float cabZ = alongX ? 0.8f : 1.0f;
                Box(name + "_Cabin", paint, cx - cabX, cx + cabX, 1.5f, 2.2f, cz - cabZ, cz + cabZ);
            }

            /// <summary>Market stall: table, two posts and a striped awning above head height.</summary>
            private void Stall(string name, BazaarSurface awning, float cx, float cz, bool faceSouth)
            {
                float dir = faceSouth ? -1f : 1f;
                Box(name + "_Table", BazaarSurface.WoodDark, cx - 1.5f, cx + 1.5f, 0f, 1.0f, cz - 0.5f, cz + 0.5f);
                float zb = cz - dir * 0.9f;
                Box(name + "_PostA", BazaarSurface.WoodDark, cx - 1.6f, cx - 1.4f, 0f, 2.6f, zb - 0.1f, zb + 0.1f);
                Box(name + "_PostB", BazaarSurface.WoodDark, cx + 1.4f, cx + 1.6f, 0f, 2.6f, zb - 0.1f, zb + 0.1f);
                float zFar = cz + dir * 1.6f;
                Box(name + "_Awning", awning, cx - 1.9f, cx + 1.9f, 2.6f, 2.78f, Mathf.Min(zb, zFar), Mathf.Max(zb, zFar));
            }

            private void Light(string name, Vector3 pos, Color color, float intensity, float range)
            {
                Data.Lights.Add(new LightSpec { Name = name, Position = pos, Color = color, Intensity = intensity, Range = range });
            }

            private static readonly Color Warm = new Color(1f, 0.86f, 0.62f);

            #endregion

            #region Ground, perimeter, masses, walls

            private void Ground()
            {
                G("Ground");
                int n = 0;
                foreach (Rect p in SubtractAll(new Rect(-58f, 58f, -66f, 66f), PitRect))
                    Box("Ground_" + n++, BazaarSurface.Sand, p.X0, p.X1, -3f, 0f, p.Z0, p.Z1);
                Box("PitFloor", BazaarSurface.Limestone, PitRect.X0, PitRect.X1, -3f, -PitDepth, PitRect.Z0, PitRect.Z1);
            }

            private void Paving()
            {
                G("Paving");
                Paint("Paving_Forecourt", BazaarSurface.Cobble, XW, XE, -44f, -30f, 0f, 0.02f);
                Paint("Paving_AttackYard", BazaarSurface.Cobble, -20f, 20f, ZS, -44f, 0f, 0.02f);
                Paint("Paving_DefendYard", BazaarSurface.Cobble, -20f, 20f, 44f, ZN, 0f, 0.02f);
                Paint("Paving_MidStreet", BazaarSurface.Cobble, -6f, 6f, -30f, -12f, 0f, 0.02f);
                Paint("Paving_MidCourtyard", BazaarSurface.Limestone, -14f, 14f, -12f, 15.5f, 0f, 0.02f);
                Paint("Paving_Hub", BazaarSurface.Cobble, -5.5f, 5.5f, 16.5f, 44f, 0f, 0.02f);
                Paint("Paving_Tunnel", BazaarSurface.Cobble, 34f, 42f, -30f, 16f, 0f, 0.02f);
            }

            private void Perimeter()
            {
                G("Perimeter");
                Box("Wall_West", BazaarSurface.SandstoneDark, XW - 1f, XW, 0f, WallHeight, ZS - 1f, ZN + 1f);
                Box("Wall_East", BazaarSurface.SandstoneDark, XE, XE + 1f, 0f, WallHeight, ZS - 1f, ZN + 1f);
                Box("Wall_South", BazaarSurface.SandstoneDark, XW, XE, 0f, WallHeight, ZS - 1f, ZS);
                Box("Wall_North", BazaarSurface.SandstoneDark, XW, XE, 0f, WallHeight, ZN, ZN + 1f);
                Box("Banner_Attack", BazaarSurface.PaintAttack, -8f, 8f, 3f, 5.6f, ZS, ZS + 0.1f, false);
                Box("Banner_Defend", BazaarSurface.PaintDefend, -8f, 8f, 3f, 5.6f, ZN - 0.1f, ZN, false);
            }

            private void Masses()
            {
                G("Buildings");
                Mass("YardCornerSW", BazaarSurface.Sandstone, new Rect(XW, -20f, ZS, -44f));
                Mass("YardCornerSE", BazaarSurface.Sandstone, new Rect(20f, XE, ZS, -44f));
                Mass("YardCornerNW", BazaarSurface.Sandstone, new Rect(XW, -20f, 44f, ZN));
                Mass("YardCornerNE", BazaarSurface.Sandstone, new Rect(20f, XE, 44f, ZN));

                Mass("ShortBlock", BazaarSurface.Whitewash, new Rect(-41f, -14f, -30f, 16f),
                    new Rect(-30f, -14f, 0f, 6f), new Rect(-30f, -24f, 6f, 16f));
                Mass("MidBlockW", BazaarSurface.Terracotta, new Rect(-14f, -6f, -30f, -12f));
                Mass("MidBlockE", BazaarSurface.Terracotta, new Rect(6f, 14f, -30f, -12f));
                Mass("ConnectorBlock", BazaarSurface.Whitewash, new Rect(14f, 34f, -30f, 16f),
                    new Rect(14f, 34f, 0f, 6f));
                Mass("TunnelEastBlock", BazaarSurface.Sandstone, new Rect(42f, XE, -30f, 16f));

                Box("Tunnel_Roof", BazaarSurface.Sandstone, 34f, 42f, 4.4f, MassHeight, -24f, 8f);
            }

            private void Walls()
            {
                G("Walls");
                WallX("MidCourtyardNorth", BazaarSurface.Whitewash, 16f, -14f, 14f, 0f, MassHeight, 1f,
                    new Op(-6f, 6f, 0f, MassHeight), new Op(8f, 12f, 1.1f, 2.5f));
                WallZ("HubWest", BazaarSurface.Whitewash, -6f, 16f, 44f, 0f, MassHeight, 1f, new Op(26f, 34f, 0f, MassHeight));
                WallZ("HubEast", BazaarSurface.Whitewash, 6f, 16f, 44f, 0f, MassHeight, 1f, new Op(26f, 34f, 0f, MassHeight));
                WallX("DefenderYardSouth", BazaarSurface.Sandstone, 44f, -20f, 20f, 0f, MassHeight, 1f,
                    new Op(-16f, -10f, 0f, 3.8f), new Op(-5.5f, 5.5f, 0f, MassHeight), new Op(10f, 16f, 0f, 3.8f));

                WallX("ADoors", BazaarSurface.Sandstone, 10f, XW, -41f, 0f, MassHeight, 1f, new Op(-49f, -43f, 0f, 3.6f));

                Box("MidDoors_PillarW", BazaarSurface.Limestone, -6f, -4f, 0f, MassHeight, -30f, -27.4f);
                Box("MidDoors_PillarE", BazaarSurface.Limestone, 4f, 6f, 0f, MassHeight, -30f, -27.4f);
                Box("MidDoors_Lintel", BazaarSurface.Limestone, -4f, 4f, 3.6f, MassHeight, -30f, -27.4f);
                AddOpening("MidDoors", new Vector3(0f, 1.8f, -28.7f), 8f, 3.6f, 0f, true);
            }

            #endregion

            #region Sites

            private void ASite()
            {
                G("SiteA");

                // Heaven deck over the hell room, reached by A Short and by the stairs from the site.
                Box("A_HeavenDeck", BazaarSurface.Limestone, -34f, -18f, 2.65f, HeavenY, 16f, 30f);
                Box("A_HellWallEast", BazaarSurface.Whitewash, -18.3f, -18f, 0f, 2.65f, 16f, 30f);
                Box("A_HellWallNorth", BazaarSurface.Whitewash, -34f, -18f, 0f, 2.65f, 29.7f, 30f);
                Box("A_HellPillarS", BazaarSurface.Limestone, -34f, -33.4f, 0f, 2.65f, 19f, 19.6f);
                Box("A_HellPillarN", BazaarSurface.Limestone, -34f, -33.4f, 0f, 2.65f, 26.4f, 27f);
                Crate("A_HellCrate", -26f, 22.5f, 1.5f);

                Box("A_ShortLanding", BazaarSurface.Limestone, -30f, -24f, 0f, HeavenY, 10.5f, 16f);
                Flight("A_ShortStairs", BazaarSurface.Limestone, false, 6f, 1, -30f, -24f, 0f, 0f, StairRise, StairSteps);
                Flight("A_HeavenStairs", BazaarSurface.Limestone, false, 34.5f, -1, -26f, -20f, 0f, 0f, StairRise, StairSteps);

                Box("A_Rail_W0", BazaarSurface.Whitewash, -34.3f, -34f, HeavenY, 4f, 16f, 17.2f);
                Box("A_Rail_W1", BazaarSurface.Whitewash, -34.3f, -34f, HeavenY, 4f, 19.8f, 21f);
                Box("A_Rail_W2", BazaarSurface.Whitewash, -34.3f, -34f, HeavenY, 4f, 25f, 30f);
                Box("A_Rail_N0", BazaarSurface.Whitewash, -34f, -26f, HeavenY, 4f, 29.7f, 30f);
                Box("A_Rail_N1", BazaarSurface.Whitewash, -20f, -18f, HeavenY, 4f, 29.7f, 30f);
                Box("A_Rail_E", BazaarSurface.Whitewash, -18.3f, -18f, HeavenY, 4f, 16f, 29.7f);

                Box("A_HeavenCrate_Low", BazaarSurface.Crate, -37.6f, -36f, 0f, 1.1f, 17.4f, 19f);
                Box("A_HeavenCrate_High", BazaarSurface.Crate, -36f, -34.2f, 0f, 2.2f, 17.4f, 19f);
                var route = new JumpRouteSpec { Name = "A_HeavenCrates" };
                route.Tops.Add(new Vector3(-36.8f, 1.1f, 18.2f));
                route.Tops.Add(new Vector3(-35.1f, 2.2f, 18.2f));
                route.Tops.Add(new Vector3(-33f, HeavenY, 18.2f));
                Data.JumpRoutes.Add(route);
                Data.Drops.Add(new DropSpec { Name = "A_HeavenDrop", Top = new Vector3(-33f, HeavenY, 23f), Landing = new Vector3(-36.5f, 0.05f, 23f) });

                // Default room, north-west corner.
                WallX("A_RoomSouth", BazaarSurface.Whitewash, 34f, XW, -45f, 0f, 3.2f, 0.4f, new Op(-52f, -49f, 0f, 2.6f));
                WallZ("A_RoomEast", BazaarSurface.Whitewash, -45f, 34f, 44f, 0f, 3.2f, 0.4f, new Op(37f, 40f, 0f, 2.6f));
                Box("A_RoomRoof", BazaarSurface.Terracotta, XW, -44.8f, 3.2f, 3.5f, 33.8f, 44f);
                Crate("A_RoomCrate", -52.5f, 41.5f, 1.2f);

                // Cover.
                CrateStack("A_Boxes", -37f, 21f);
                CrateStack("A_BackBoxes", -41f, 40f);
                Crate("A_LaneCrate", -50.5f, 20f, 1.4f);
                Pillar("A_Pillar", -30f, 40f);
                Pillar("A_PillarEast", -12f, 21f);
                Car("A_Car", BazaarSurface.CarOchre, -35f, 36.5f, true);
                Crate("A_Rotate_Crate", -9.5f, 40f, 1.4f);

                // A Long: cover off the lane centre so the long sightline stays open.
                G("ALong");
                CrateStack("ALong_Boxes_Low", -52.8f, -21f);
                Box("ALong_Wall_Mid", BazaarSurface.Whitewash, -44.2f, -41f, 0f, 2.2f, -9f, -8f);
                Box("ALong_Wall_Far", BazaarSurface.Whitewash, -55f, -52f, 0f, 2.2f, 0.5f, 1.5f);
            }

            private void BSite()
            {
                G("SiteB");

                // Sunken pit ("hell") with a long stair on the west side and a short one on the south side.
                Flight("B_PitStairsWest", BazaarSurface.Limestone, true, PitRect.X0 + PitSteps * StairRun, -1, 27f, 31f, -PitDepth, -PitDepth, PitDepth / PitSteps, PitSteps);
                Flight("B_PitStairsSouth", BazaarSurface.Limestone, false, PitRect.Z0 + PitSteps * StairRun, -1, 32f, 36f, -PitDepth, -PitDepth, PitDepth / PitSteps, PitSteps);
                Box("B_PitCrate", BazaarSurface.Crate, 34.5f, 36.1f, -PitDepth, -PitDepth + 1.6f, 29.5f, 31.1f);

                // Default room under the balcony, north-east corner.
                Box("B_Balcony", BazaarSurface.Limestone, 41f, XE, 2.65f, HeavenY, 34f, 44f);
                WallX("B_RoomSouth", BazaarSurface.Whitewash, 34f, 41f, XE, 0f, 2.65f, 0.4f, new Op(43f, 46f, 0f, 2.65f));
                WallZ("B_RoomWest", BazaarSurface.Whitewash, 41f, 34f, 44f, 0f, 2.65f, 0.4f, new Op(40.5f, 43.5f, 0f, 2.65f));
                Crate("B_RoomCrate", 52f, 41f, 1.2f);
                Flight("B_BalconyStairs", BazaarSurface.Limestone, false, 29.5f, 1, 48f, 52f, 0f, 0f, StairRise, StairSteps);

                Box("B_Rail_S0", BazaarSurface.Whitewash, 41f, 48f, HeavenY, 4f, 34f, 34.3f);
                Box("B_Rail_S1", BazaarSurface.Whitewash, 52f, XE, HeavenY, 4f, 34f, 34.3f);
                Box("B_Rail_W0", BazaarSurface.Whitewash, 41f, 41.3f, HeavenY, 4f, 34.3f, 36.2f);
                Box("B_Rail_W1", BazaarSurface.Whitewash, 41f, 41.3f, HeavenY, 4f, 38.8f, 40f);
                Box("B_Rail_W2", BazaarSurface.Whitewash, 41f, 41.3f, HeavenY, 4f, 43f, 44f);

                Box("B_BalconyCrate_Low", BazaarSurface.Crate, 37.8f, 39.4f, 0f, 1.1f, 36.8f, 38.4f);
                Box("B_BalconyCrate_High", BazaarSurface.Crate, 39.4f, 41f, 0f, 2.2f, 36.8f, 38.4f);
                var route = new JumpRouteSpec { Name = "B_BalconyCrates" };
                route.Tops.Add(new Vector3(38.6f, 1.1f, 37.6f));
                route.Tops.Add(new Vector3(40.2f, 2.2f, 37.6f));
                route.Tops.Add(new Vector3(42f, HeavenY, 37.6f));
                Data.JumpRoutes.Add(route);
                Data.Drops.Add(new DropSpec { Name = "B_BalconyDrop", Top = new Vector3(41.8f, HeavenY, 41.5f), Landing = new Vector3(39f, 0.05f, 41.5f) });

                // Cover.
                CrateStack("B_Boxes", 22f, 36f);
                Crate("B_TunnelCrate", 44.5f, 20f, 1.4f);
                Box("B_LowWall", BazaarSurface.Whitewash, 30f, 33f, 0f, 1.2f, 19f, 19.6f);
                Pillar("B_Pillar", 17.3f, 40.7f);
                Car("B_Car", BazaarSurface.CarGreen, 25f, 19f, true);
                Crate("B_Rotate_Crate", 9.5f, 40f, 1.4f);
                Crate("B_PitRim_Crate", 25f, 29f, 1.5f);

                // B Tunnels: two pillars stagger the covered corridor so there is no long straight sightline.
                G("BTunnels");
                Box("BTunnel_PillarA", BazaarSurface.Limestone, 36.6f, 38.6f, 0f, 4.4f, -16f, -14.6f);
                Box("BTunnel_PillarB", BazaarSurface.Limestone, 37.4f, 39.4f, 0f, 4.4f, -8f, -6.6f);
                Crate("BTunnel_Crate", 41f, -3f, 1.3f);
                Crate("BTunnel_CrateB", 35f, 3.5f, 1.3f);
            }

            #endregion

            #region Neutral ground

            private void Forecourt()
            {
                G("Forecourt");
                Stall("Stall_W", BazaarSurface.AwningRed, -46f, -41f, false);
                Stall("Stall_E", BazaarSurface.AwningBlue, 46f, -41f, false);
                Stall("Stall_C", BazaarSurface.AwningYellow, -26f, -33f, false);
                Stall("Stall_D", BazaarSurface.AwningGreen, 27f, -33f, false);
                Box("Kiosk_W", BazaarSurface.Whitewash, -34f, -31f, 0f, 2.8f, -40f, -37f);
                Box("Kiosk_E", BazaarSurface.Whitewash, 31f, 34f, 0f, 2.8f, -40f, -37f);
                CrateStack("Forecourt_ALongMouth", -51f, -34f);
                CrateStack("Forecourt_TunnelMouth", 44f, -34f);
                Car("Forecourt_Car_A", BazaarSurface.CarWhite, -10f, -34f, true);
                Car("Forecourt_Car_B", BazaarSurface.CarGreen, 12f, -40f, false);
            }

            private void MidStreetAndCourtyard()
            {
                G("Mid");
                Box("Mid_Street_Crate", BazaarSurface.Crate, -5.2f, -3.8f, 0f, 1.4f, -20f, -18.6f);
                Box("Mid_Street_CrateE", BazaarSurface.Crate, 3.8f, 5.2f, 0f, 1.4f, -15f, -13.6f);

                Box("Fountain_Base", BazaarSurface.Limestone, -1.6f, 1.6f, 0f, 0.9f, -5.6f, -2.4f);
                Box("Fountain_Column", BazaarSurface.Limestone, -0.4f, 0.4f, 0.9f, 3f, -4.4f, -3.6f);
                Car("Mid_Car", BazaarSurface.CarOchre, -11.8f, -6.5f, false);
                CrateStack("Mid_Boxes", 10.2f, -8f);
                Box("Mid_Wall_A", BazaarSurface.Whitewash, -9f, -6f, 0f, 1.8f, 9f, 10f);
                Box("Mid_Wall_B", BazaarSurface.Whitewash, 6f, 9f, 0f, 1.8f, 5.5f, 6.5f);
            }

            private void Hub()
            {
                G("Hub");
                Pillar("Hub_Pillar", 0f, 30f, 1.6f, 3.2f);
                Crate("Hub_CrateA", -3.5f, 22f, 1.4f);
                Crate("Hub_CrateB", 3.5f, 38f, 1.4f);
            }

            private void Yards()
            {
                G("Yards");
                Crate("AttackYard_CrateW", -17f, -49f, 1.4f);
                Crate("AttackYard_CrateE", 17f, -49f, 1.4f);
                Crate("DefendYard_CrateW", -17f, 49f, 1.4f);
                Crate("DefendYard_CrateE", 17f, 49f, 1.4f);
                Stall("AttackYard_Stall", BazaarSurface.AwningRed, -16.5f, -59.5f, true);
                Stall("DefendYard_Stall", BazaarSurface.AwningBlue, 16.5f, 59.5f, false);
            }

            private void Palms()
            {
                G("Palms");
                Palm("Palm_MidNW", -12f, 13f);
                Palm("Palm_MidSE", 12f, -10.5f);
                Palm("Palm_ForecourtW", -38f, -42f);
                Palm("Palm_ForecourtE", 38f, -42f);
                Palm("Palm_AttackYardW", -18.5f, -53f);
                Palm("Palm_DefendYardE", 18.5f, 53f);
                Palm("Palm_ASite", -50f, 18.5f);
                Palm("Palm_BSite", 50f, 20f);
            }

            private void Palm(string name, float x, float z)
            {
                Box(name + "_Trunk", BazaarSurface.WoodDark, x - 0.3f, x + 0.3f, 0f, 5.2f, z - 0.3f, z + 0.3f);
                Box(name + "_LeavesA", BazaarSurface.Foliage, x - 2.2f, x + 2.2f, 5.2f, 5.45f, z - 0.35f, z + 0.35f, false);
                Box(name + "_LeavesB", BazaarSurface.Foliage, x - 0.35f, x + 0.35f, 5.2f, 5.45f, z - 2.2f, z + 2.2f, false);
                Box(name + "_LeavesC", BazaarSurface.Foliage, x - 1.6f, x + 1.6f, 5.45f, 5.7f, z - 1.6f, z + 1.6f, false);
            }

            private void Decor()
            {
                G("Decor");
                int n = 0;
                for (float z = -25f; z <= 11f; z += 12f)
                {
                    Box("Shutter_ALong_" + n, BazaarSurface.PaintedBlue, -41.12f, -41f, 1.4f, 3.4f, z - 0.6f, z + 0.6f, false);
                    Box("Shutter_Tunnel_" + n, BazaarSurface.PaintedBlue, 41.88f, 42f, 1.4f, 3.4f, z - 0.6f, z + 0.6f, false);
                    n++;
                }

                for (float x = -38f; x <= 38f; x += 19f)
                    Box("Shutter_Forecourt_" + (int)x, BazaarSurface.PaintedBlue, x - 0.6f, x + 0.6f, 1.4f, 3.4f, -30.12f, -30f, false);

                for (float z = 20f; z <= 40f; z += 10f)
                {
                    Box("Lamp_Hub_W" + (int)z, BazaarSurface.Lamp, -5.5f, -5.3f, 3.6f, 4.1f, z - 0.2f, z + 0.2f, false);
                    Box("Lamp_Hub_E" + (int)z, BazaarSurface.Lamp, 5.3f, 5.5f, 3.6f, 4.1f, z - 0.2f, z + 0.2f, false);
                }

                Box("Lamp_Mid_W", BazaarSurface.Lamp, -5.9f, -5.7f, 3.4f, 3.9f, -22.2f, -21.8f, false);
                Box("Lamp_Mid_E", BazaarSurface.Lamp, 5.7f, 5.9f, 3.4f, 3.9f, -22.2f, -21.8f, false);
                Box("Roof_Tower_A", BazaarSurface.Terracotta, -22f, -18f, MassHeight, 8f, -4f, 0f);
                Box("Roof_Tower_B", BazaarSurface.Terracotta, 18f, 22f, MassHeight, 8f, 8f, 12f);
            }

            private void Markers()
            {
                G("Markers");
                Letter('A', -45f, 26f);
                Letter('B', 18f, 30f);
                PlantRing("A", -45f, 26f);
                PlantRing("B", 18f, 30f);
                Paint("BuyLine_Attack", BazaarSurface.PaintAttack, -20f, 20f, -44.6f, -44f, 0.02f);
                Paint("BuyLine_Defend", BazaarSurface.PaintDefend, -20f, 20f, 44f, 44.6f, 0.02f);
                Paint("SpawnStripe_Attack", BazaarSurface.PaintAttack, -12f, 12f, -61.4f, -60.8f, 0.02f);
                Paint("SpawnStripe_Defend", BazaarSurface.PaintDefend, -12f, 12f, 60.8f, 61.4f, 0.02f);
                // Painted direction arrows on the forecourt: A Long, Mid, B.
                Paint("Arrow_ALong", BazaarSurface.PaintWhite, -49f, -47f, -41f, -36f, 0.02f);
                Paint("Arrow_Mid", BazaarSurface.PaintWhite, -0.5f, 0.5f, -41f, -35f, 0.02f);
                Paint("Arrow_BTunnels", BazaarSurface.PaintWhite, 37f, 39f, -41f, -36f, 0.02f);
            }

            private void Letter(char c, float cx, float cz)
            {
                const float w = 3.6f;
                const float h = 5.4f;
                const float t = 0.7f;
                float x0 = cx - w * 0.5f, x1 = cx + w * 0.5f, z0 = cz - h * 0.5f, z1 = cz + h * 0.5f;
                string n = "Letter" + c;
                Paint(n + "_L", BazaarSurface.PaintWhite, x0, x0 + t, z0, z1, 0.025f);
                if (c == 'A')
                {
                    Paint(n + "_R", BazaarSurface.PaintWhite, x1 - t, x1, z0, z1, 0.025f);
                    Paint(n + "_Top", BazaarSurface.PaintWhite, x0, x1, z1 - t, z1, 0.025f);
                    Paint(n + "_Mid", BazaarSurface.PaintWhite, x0, x1, cz - t * 0.5f, cz + t * 0.5f, 0.025f);
                }
                else
                {
                    Paint(n + "_Top", BazaarSurface.PaintWhite, x0, x1 - t * 0.5f, z1 - t, z1, 0.025f);
                    Paint(n + "_Mid", BazaarSurface.PaintWhite, x0, x1 - t * 0.5f, cz - t * 0.5f, cz + t * 0.5f, 0.025f);
                    Paint(n + "_Bot", BazaarSurface.PaintWhite, x0, x1 - t * 0.5f, z0, z0 + t, 0.025f);
                    Paint(n + "_RU", BazaarSurface.PaintWhite, x1 - t, x1, cz, z1 - t * 0.5f, 0.025f);
                    Paint(n + "_RD", BazaarSurface.PaintWhite, x1 - t, x1, z0 + t * 0.5f, cz, 0.025f);
                }
            }

            private void PlantRing(string site, float cx, float cz)
            {
                const float half = 7f;
                const float t = 0.3f;
                string n = "PlantRing_" + site;
                Paint(n + "_S", BazaarSurface.PaintPlant, cx - half, cx + half, cz - half, cz - half + t, 0.02f);
                Paint(n + "_N", BazaarSurface.PaintPlant, cx - half, cx + half, cz + half - t, cz + half, 0.02f);
                Paint(n + "_W", BazaarSurface.PaintPlant, cx - half, cx - half + t, cz - half + t, cz + half - t, 0.02f);
                Paint(n + "_E", BazaarSurface.PaintPlant, cx + half - t, cx + half, cz - half + t, cz + half - t, 0.02f);
            }

            private void Lights()
            {
                Light("TunnelLight_1", new Vector3(38f, 3.6f, -20f), Warm, 1.3f, 12f);
                Light("TunnelLight_2", new Vector3(38f, 3.6f, -10f), Warm, 1.3f, 12f);
                Light("TunnelLight_3", new Vector3(38f, 3.6f, 0f), Warm, 1.3f, 12f);
                Light("TunnelLight_4", new Vector3(38f, 3.6f, 6f), Warm, 1.1f, 10f);
                Light("A_HellLight", new Vector3(-26f, 2.2f, 23f), Warm, 1.4f, 12f);
                Light("A_RoomLight", new Vector3(-50f, 2.8f, 39f), Warm, 1.2f, 10f);
                Light("B_RoomLight", new Vector3(48f, 2.4f, 39f), Warm, 1.2f, 10f);
                Light("B_PitLight", new Vector3(33f, 0.5f, 29f), Warm, 1.0f, 9f);
                Light("MidDoorsLight", new Vector3(0f, 4f, -28f), Warm, 0.9f, 10f);
                Light("ShortLight_1", new Vector3(-20f, 3.2f, 3f), Warm, 0.8f, 9f);
                Light("ConnectorLight_1", new Vector3(24f, 3.2f, 3f), Warm, 0.8f, 9f);
            }

            private void Backdrop()
            {
                G("Backdrop");
                var rng = new System.Random(11);
                int n = 0;
                for (float z = -110f; z <= 110f; z += 14f)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float x = side * (66f + (float)rng.NextDouble() * 18f);
                        float w = 9f + (float)rng.NextDouble() * 8f;
                        float h = 8f + (float)rng.NextDouble() * 14f;
                        float d = 9f + (float)rng.NextDouble() * 6f;
                        Box("Backdrop" + n++, BazaarSurface.Backdrop, x - w * 0.5f, x + w * 0.5f, 0f, h, z - d * 0.5f, z + d * 0.5f, false);
                    }
                }

                for (float x = -60f; x <= 60f; x += 16f)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float z = side * (78f + (float)rng.NextDouble() * 16f);
                        float w = 10f + (float)rng.NextDouble() * 8f;
                        float h = 9f + (float)rng.NextDouble() * 16f;
                        float d = 8f + (float)rng.NextDouble() * 6f;
                        Box("Backdrop" + n++, BazaarSurface.Backdrop, x - w * 0.5f, x + w * 0.5f, 0f, h, z - d * 0.5f, z + d * 0.5f, false);
                    }
                }

                Box("Minaret_W", BazaarSurface.Whitewash, -73f, -69f, 0f, 30f, -20f, -16f, false);
                Box("Minaret_W_Cap", BazaarSurface.Terracotta, -73.5f, -68.5f, 30f, 32f, -20.5f, -15.5f, false);
                Box("Minaret_E", BazaarSurface.Whitewash, 69f, 73f, 0f, 26f, 28f, 32f, false);
                Box("Minaret_E_Cap", BazaarSurface.Terracotta, 68.5f, 73.5f, 26f, 28f, 27.5f, 32.5f, false);
            }

            #endregion

            #region Spawns, waypoints, areas, zones, routes

            private void Spawns()
            {
                float[] xs = { -12f, -6f, 0f, 6f, 12f };
                float[] zs = { -58f, -54f };
                for (int team = 0; team < 2; team++)
                {
                    float sign = team == 0 ? 1f : -1f;
                    for (int row = 0; row < zs.Length; row++)
                    {
                        for (int i = 0; i < xs.Length; i++)
                        {
                            bool isPlayer = team == 0 && row == 1 && i == 2;
                            string name = isPlayer ? "PlayerSpawn" : "Spawn_" + (team == 0 ? "A" : "D") + (row * xs.Length + i + 1).ToString("00");
                            Data.Spawns.Add(new SpawnSpec
                            {
                                Name = name,
                                Team = team,
                                Position = new Vector3(xs[i], 0.05f, zs[row] * sign),
                                Yaw = team == 0 ? 0f : 180f
                            });
                        }
                    }
                }

                int playerIndex = Data.Spawns.FindIndex(sp => sp.Name == "PlayerSpawn");
                SpawnSpec player = Data.Spawns[playerIndex];
                Data.Spawns.RemoveAt(playerIndex);
                Data.Spawns.Insert(0, player);
            }

            private void Wp(string name, string area, float x, float y, float z, BazaarTier tier)
            {
                Data.Waypoints.Add(new WaypointSpec { Name = name, Area = area, Position = new Vector3(x, y, z), Tier = tier });
            }

            private void Waypoints()
            {
                const BazaarTier g = BazaarTier.Ground;
                const BazaarTier h = BazaarTier.Heaven;
                const BazaarTier p = BazaarTier.Pit;
                const float y = 0.05f;
                const float hy = HeavenY + 0.05f;

                Wp("AttackYard_W", "AttackYard", -14f, y, -56f, g);
                Wp("AttackYard_C", "AttackYard", 0f, y, -57f, g);
                Wp("AttackYard_E", "AttackYard", 14f, y, -56f, g);
                Wp("Forecourt_W", "Forecourt", -44f, y, -37f, g);
                Wp("Forecourt_WC", "Forecourt", -22f, y, -37f, g);
                Wp("Forecourt_C", "Forecourt", 0f, y, -38f, g);
                Wp("Forecourt_EC", "Forecourt", 22f, y, -37f, g);
                Wp("Forecourt_E", "Forecourt", 38f, y, -37f, g);

                Wp("ALong_1", "ALong", -48f, y, -24f, g);
                Wp("ALong_2", "ALong", -48f, y, -14f, g);
                Wp("ALong_3", "ALong", -48f, y, -4f, g);
                Wp("ALong_4", "ALong", -48f, y, 6f, g);
                Wp("ALong_Doors", "ALong", -46f, y, 12.5f, g);
                Wp("ASite_LaneMouth", "SiteA", -46f, y, 19f, g);
                Wp("ASite_Plant", "SiteA", -45f, y, 26f, g);
                Wp("ASite_East", "SiteA", -16f, y, 27f, g);
                Wp("ASite_Back", "SiteA", -36f, y, 33f, g);
                Wp("ASite_Entrance", "SiteA", -13f, y, 40f, g);
                Wp("ASite_Room", "SiteA", -50f, y, 38.5f, g);
                Wp("ASite_HellOut", "SiteA", -36f, y, 24f, g);
                Wp("ASite_Hell", "SiteA", -29f, y, 25f, g);
                Wp("ASite_HeavenStairsFoot", "SiteA", -23f, y, 36.5f, g);
                Wp("ASite_Heaven", "SiteA", -26f, hy, 24f, h);
                Wp("ASite_HeavenEdge", "SiteA", -31f, hy, 22.5f, h);

                Wp("AShort_1", "AShort", -19f, y, 3f, g);
                Wp("AShort_2", "AShort", -27f, y, 3f, g);
                Wp("AShort_StairFoot", "AShort", -27f, y, 4.8f, g);
                Wp("AShort_Landing", "AShort", -27f, hy, 13f, h);

                Wp("MidStreet_1", "Mid", 0f, y, -25f, g);
                Wp("MidStreet_2", "Mid", 2.5f, y, -17f, g);
                Wp("MidCourt_S", "Mid", 0f, y, -9f, g);
                Wp("MidCourt_W", "Mid", -9f, y, 2f, g);
                Wp("MidCourt_E", "Mid", 9f, y, 2f, g);
                Wp("MidCourt_C", "Mid", 0f, y, 4f, g);
                Wp("MidCourt_N", "Mid", 0f, y, 12f, g);
                Wp("MidCourt_Window", "Mid", 10f, y, 13.5f, g);

                Wp("Hub_S", "Hub", 0f, y, 20f, g);
                Wp("Hub_W", "Hub", -3.5f, y, 30f, g);
                Wp("Hub_E", "Hub", 3.5f, y, 30f, g);
                Wp("Hub_N", "Hub", 0f, y, 41f, g);

                Wp("BConnector_1", "BConnector", 20f, y, 3f, g);
                Wp("BConnector_2", "BConnector", 29f, y, 3f, g);
                Wp("BTunnel_1", "BTunnels", 38f, y, -26f, g);
                Wp("BTunnel_2", "BTunnels", 34.8f, y, -14f, g);
                Wp("BTunnel_3", "BTunnels", 40.8f, y, -9f, g);
                Wp("BTunnel_4", "BTunnels", 38f, y, -1f, g);
                Wp("BTunnel_5", "BTunnels", 38f, y, 5f, g);
                Wp("BTunnel_Exit", "BTunnels", 38f, y, 13f, g);

                Wp("BSite_Rotate", "SiteB", 10f, y, 30f, g);
                Wp("BSite_Plant", "SiteB", 18f, y, 30f, g);
                Wp("BSite_Back", "SiteB", 28f, y, 40f, g);
                Wp("BSite_Entrance", "SiteB", 13f, y, 40f, g);
                Wp("BSite_TunnelMouth", "SiteB", 38f, y, 20f, g);
                Wp("BSite_PitRim", "SiteB", 33f, y, 21f, g);
                Wp("BSite_Pit", "SiteB", 33f, -PitDepth + 0.05f, 28f, p);
                Wp("BSite_Room", "SiteB", 48f, y, 39f, g);
                Wp("BSite_BalconyStairsFoot", "SiteB", 50f, y, 27.5f, g);
                Wp("BSite_Balcony", "SiteB", 50f, hy, 38f, h);
                Wp("BSite_BalconyEdge", "SiteB", 45f, hy, 39f, h);

                Wp("DefendYard_W", "DefendYard", -14f, y, 56f, g);
                Wp("DefendYard_C", "DefendYard", 0f, y, 57f, g);
                Wp("DefendYard_E", "DefendYard", 14f, y, 56f, g);
                Wp("DefendYard_AEntrance", "DefendYard", -13f, y, 47f, g);
                Wp("DefendYard_BEntrance", "DefendYard", 13f, y, 47f, g);
            }

            private static Bounds BoundsFromMinMax(Vector3 min, Vector3 max)
            {
                var b = new Bounds();
                b.SetMinMax(min, max);
                return b;
            }

            private void Area(string name, string kind, float x0, float x1, float z0, float z1, float y1 = 9f)
            {
                Data.Areas.Add(new AreaSpec { Name = name, Kind = kind, Bounds = BoundsFromMinMax(new Vector3(x0, -PitDepth, z0), new Vector3(x1, y1, z1)) });
            }

            private void Areas()
            {
                Area("AttackYard", "Yard", -20f, 20f, ZS, -44f);
                Area("DefendYard", "Yard", -20f, 20f, 44f, ZN);
                Area("Forecourt", "Street", XW, XE, -44f, -30f);
                Area("ALong", "Lane", -55f, -41f, -30f, 16f);
                Area("AShort", "Lane", -30f, -14f, 0f, 16f);
                Area("Mid", "Lane", -14f, 14f, -30f, 16f);
                Area("BConnector", "Lane", 14f, 34f, 0f, 6f);
                Area("BTunnels", "Lane", 34f, 42f, -30f, 16f);
                Area("Hub", "Hub", -6f, 6f, 16f, 44f);
                Area("SiteA", "Site", -55f, -6f, 16f, 44f);
                Area("SiteB", "Site", 6f, XE, 16f, 44f);
            }

            private void Zones()
            {
                Data.Zones.Add(new ZoneSpec { Name = "PlantZone_A", Kind = BazaarZoneKind.Plant, Site = "A", Bounds = new Bounds(new Vector3(-45f, 1.5f, 26f), new Vector3(14f, 3f, 14f)) });
                Data.Zones.Add(new ZoneSpec { Name = "PlantZone_B", Kind = BazaarZoneKind.Plant, Site = "B", Bounds = new Bounds(new Vector3(18f, 1.5f, 30f), new Vector3(14f, 3f, 14f)) });
                Data.Zones.Add(new ZoneSpec { Name = "BuyZone_Attack", Kind = BazaarZoneKind.Buy, Site = "Attack", Bounds = BoundsFromMinMax(new Vector3(-20f, 0f, ZS), new Vector3(20f, 3f, -44f)) });
                Data.Zones.Add(new ZoneSpec { Name = "BuyZone_Defend", Kind = BazaarZoneKind.Buy, Site = "Defend", Bounds = BoundsFromMinMax(new Vector3(-20f, 0f, 44f), new Vector3(20f, 3f, ZN)) });
            }

            private Vector3 WaypointPosition(string name)
            {
                foreach (WaypointSpec w in Data.Waypoints)
                {
                    if (w.Name == name)
                        return w.Position;
                }

                throw new KeyNotFoundException("Bazaar waypoint not found: " + name);
            }

            private void AddRoute(string name, string start, params string[] points)
            {
                var route = new RouteSpec { Name = name, Start = WaypointPosition(start) };
                foreach (string point in points)
                    route.Points.Add(WaypointPosition(point));
                Data.Routes.Add(route);
            }

            private void Routes()
            {
                AddRoute("ALong", "Forecourt_W", "ALong_1", "ALong_3", "ALong_Doors", "ASite_LaneMouth", "ASite_Plant");
                AddRoute("AShort", "MidCourt_S", "MidCourt_W", "AShort_1", "AShort_2", "AShort_StairFoot", "AShort_Landing", "ASite_Heaven", "ASite_HeavenStairsFoot");
                AddRoute("Mid", "Forecourt_C", "MidStreet_1", "MidStreet_2", "MidCourt_S", "MidCourt_W", "MidCourt_E", "MidCourt_N", "Hub_S", "Hub_N");
                AddRoute("BTunnels", "Forecourt_E", "BTunnel_1", "BTunnel_2", "BTunnel_3", "BTunnel_4", "BTunnel_5", "BTunnel_Exit", "BSite_TunnelMouth", "BSite_Plant");
                AddRoute("BConnector", "MidCourt_S", "MidCourt_E", "BConnector_1", "BConnector_2", "BTunnel_4", "BTunnel_Exit");
                AddRoute("ASite", "ASite_Plant", "ASite_HellOut", "ASite_Hell", "ASite_HeavenStairsFoot", "ASite_Heaven", "ASite_HeavenEdge", "ASite_Back", "ASite_Room");
                AddRoute("BSite", "BSite_Plant", "BSite_PitRim", "BSite_Pit", "BSite_BalconyStairsFoot", "BSite_Balcony", "BSite_BalconyEdge", "BSite_Back", "BSite_Room");
                AddRoute("Rotate", "DefendYard_AEntrance", "ASite_Entrance", "ASite_East", "Hub_W", "Hub_E", "BSite_Rotate", "BSite_Entrance", "DefendYard_BEntrance");
            }

            #endregion
        }
    }
}
