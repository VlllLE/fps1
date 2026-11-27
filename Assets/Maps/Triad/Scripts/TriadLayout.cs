using System.Collections.Generic;
using UnityEngine;

namespace FpsMaps.Triad
{
    /// <summary>
    /// Pure-data layout of the Triad map (metres, origin at the map centre, street level at y = 0, +Z = north).
    /// The editor builder turns it into the scene, and the tests read the same data, so the two never drift apart.
    ///
    /// Layout: two spawn yards (south = team A, north = team B) feed three 20 m wide lanes (west, middle, east).
    /// Each lane holds one enterable two-storey building (Warehouse, Office, Depot). Two city blocks of four
    /// solid masses separate the lanes. Three tiers: street (y 0), mid (mass roofs, upper floors and bridges, y 4)
    /// and high (building roofs, y 8).
    /// </summary>
    public static class TriadLayout
    {
        public const string SceneName = "Triad";
        public const string ScenePath = "Assets/Scenes/Triad.unity";
        public const string RootFolder = "Assets/Maps/Triad";

        public const float HalfWidth = 44f;
        public const float YardInnerZ = 47f;
        public const float YardOuterZ = 62f;
        public const float MidTierY = 4f;
        public const float HighTierY = 8f;
        public const float StairRise = 0.2f;
        public const float StairRun = 0.3f;
        public const int StairSteps = 20;

        public static readonly float[] LaneCenters = { -34f, 0f, 34f };
        public static readonly string[] LaneNames = { "West", "Middle", "East" };
        public static readonly string[] BuildingNames = { "Warehouse", "Office", "Depot" };

        public static TriadLayoutData Create()
        {
            var gen = new Generator();
            gen.Build();
            return gen.Data;
        }

        private struct Frame
        {
            public readonly float Cx;
            public readonly float Cz;

            public Frame(float cx, float cz)
            {
                Cx = cx;
                Cz = cz;
            }

            public float X(float lx) => Cx + lx;
            public float Z(float lz) => Cz + lz;
            public Vector3 P(float lx, float y, float lz) => new Vector3(Cx + lx, y, Cz + lz);
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

        private struct EdgeGap
        {
            public readonly char Edge;
            public readonly float Center;
            public readonly float Width;

            public EdgeGap(char edge, float center, float width)
            {
                Edge = edge;
                Center = center;
                Width = width;
            }
        }

        private sealed class Generator
        {
            public readonly TriadLayoutData Data = new TriadLayoutData();

            private string group = "Map";
            private float zs = 1f;
            private string zSuffix = "";

            public void Build()
            {
                Ground();
                Perimeter();
                Gates();
                Masses();
                Skybridges();
                Warehouse();
                Office();
                Depot();
                StreetProps();
                RoofProps();
                Lamps();
                Skyline();
                Spawns();
                Waypoints();
                Areas();
                Routes();
            }

            #region Primitive helpers

            private void G(string name)
            {
                group = name;
            }

            private BlockSpec Box(string name, TriadSurface s, float x0, float x1, float y0, float y1, float z0, float z1, bool solid = true)
            {
                float ax = Mathf.Min(x0, x1), bx = Mathf.Max(x0, x1);
                float ay = Mathf.Min(y0, y1), by = Mathf.Max(y0, y1);
                float az = Mathf.Min(z0, z1), bz = Mathf.Max(z0, z1);
                if (bx - ax < 0.001f || by - ay < 0.001f || bz - az < 0.001f)
                    return null;

                var b = new BlockSpec
                {
                    Name = name,
                    Group = group,
                    Surface = s,
                    Center = new Vector3((ax + bx) * 0.5f, (ay + by) * 0.5f, (az + bz) * 0.5f),
                    Size = new Vector3(bx - ax, by - ay, bz - az),
                    Solid = solid
                };
                Data.Blocks.Add(b);
                return b;
            }

            private BlockSpec LBox(Frame f, string name, TriadSurface s, float lx0, float lx1, float y0, float y1, float lz0, float lz1, bool solid = true)
            {
                return Box(name, s, f.X(lx0), f.X(lx1), y0, y1, f.Z(lz0), f.Z(lz1), solid);
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

            /// <summary>Wall running along local X at local z = <paramref name="lz"/>, with openings cut along X.</summary>
            private void WallX(Frame f, string name, TriadSurface s, float lz, float lx0, float lx1, float y0, float y1, float t, params Op[] ops)
            {
                var list = new List<Op>(ops);
                list.Sort((p, q) => p.A.CompareTo(q.A));
                float cur = lx0;
                int n = 0;
                foreach (Op op in list)
                {
                    if (op.A > cur + 0.001f)
                        LBox(f, name + "_P" + n, s, cur, op.A, y0, y1, lz - t * 0.5f, lz + t * 0.5f);
                    if (op.Yb > y0 + 0.001f)
                        LBox(f, name + "_Sill" + n, s, op.A, op.B, y0, op.Yb, lz - t * 0.5f, lz + t * 0.5f);
                    if (op.Yt < y1 - 0.001f)
                        LBox(f, name + "_Lintel" + n, s, op.A, op.B, op.Yt, y1, lz - t * 0.5f, lz + t * 0.5f);
                    AddOpening(name + "_Op" + n, f.P((op.A + op.B) * 0.5f, (op.Yb + op.Yt) * 0.5f, lz), op.B - op.A, op.Yt - op.Yb, op.Yb, op.Yb <= y0 + 0.05f);
                    cur = op.B;
                    n++;
                }

                if (lx1 > cur + 0.001f)
                    LBox(f, name + "_P" + n, s, cur, lx1, y0, y1, lz - t * 0.5f, lz + t * 0.5f);
            }

            /// <summary>Wall running along local Z at local x = <paramref name="lx"/>, with openings cut along Z.</summary>
            private void WallZ(Frame f, string name, TriadSurface s, float lx, float lz0, float lz1, float y0, float y1, float t, params Op[] ops)
            {
                var list = new List<Op>(ops);
                list.Sort((p, q) => p.A.CompareTo(q.A));
                float cur = lz0;
                int n = 0;
                foreach (Op op in list)
                {
                    if (op.A > cur + 0.001f)
                        LBox(f, name + "_P" + n, s, lx - t * 0.5f, lx + t * 0.5f, y0, y1, cur, op.A);
                    if (op.Yb > y0 + 0.001f)
                        LBox(f, name + "_Sill" + n, s, lx - t * 0.5f, lx + t * 0.5f, y0, op.Yb, op.A, op.B);
                    if (op.Yt < y1 - 0.001f)
                        LBox(f, name + "_Lintel" + n, s, lx - t * 0.5f, lx + t * 0.5f, op.Yt, y1, op.A, op.B);
                    AddOpening(name + "_Op" + n, f.P(lx, (op.Yb + op.Yt) * 0.5f, (op.A + op.B) * 0.5f), op.B - op.A, op.Yt - op.Yb, op.Yb, op.Yb <= y0 + 0.05f);
                    cur = op.B;
                    n++;
                }

                if (lz1 > cur + 0.001f)
                    LBox(f, name + "_P" + n, s, lx - t * 0.5f, lx + t * 0.5f, y0, y1, cur, lz1);
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

            private void Slab(Frame f, string name, TriadSurface s, float y0, float y1, Rect r, params Rect[] holes)
            {
                int n = 0;
                foreach (Rect p in SubtractAll(r, holes))
                    LBox(f, name + "_" + n++, s, p.X0, p.X1, y0, y1, p.Z0, p.Z1);
            }

            private void Flight(Frame f, string name, TriadSurface s, bool alongX, float start, int dir, float c0, float c1, float floorY, float baseY)
            {
                for (int i = 0; i < StairSteps; i++)
                {
                    float a0 = start + dir * i * StairRun;
                    float a1 = start + dir * (i + 1) * StairRun;
                    float top = baseY + (i + 1) * StairRise;
                    if (alongX)
                        LBox(f, name + "_S" + i.ToString("00"), s, a0, a1, floorY, top, c0, c1);
                    else
                        LBox(f, name + "_S" + i.ToString("00"), s, c0, c1, floorY, top, a0, a1);
                }

                float mid = (c0 + c1) * 0.5f;
                float end = start + dir * StairSteps * StairRun;
                Data.Flights.Add(new FlightSpec
                {
                    Name = name,
                    Foot = alongX ? f.P(start, baseY, mid) : f.P(mid, baseY, start),
                    Top = alongX ? f.P(end, baseY + StairSteps * StairRise, mid) : f.P(mid, baseY + StairSteps * StairRise, end),
                    Width = c1 - c0,
                    Rise = StairRise,
                    Run = StairRun,
                    Steps = StairSteps
                });
            }

            /// <summary>Sloped guard rail following a flight from step <paramref name="k0"/> to the top, on the side at <paramref name="cross"/>.</summary>
            private void SlopedRail(Frame f, string name, bool alongX, float start, int dir, float baseY, int k0, float cross)
            {
                float a0 = start + dir * k0 * StairRun;
                float a1 = start + dir * StairSteps * StairRun;
                float y0 = baseY + k0 * StairRise;
                float y1 = baseY + StairSteps * StairRise;
                Vector3 p0 = alongX ? f.P(a0, y0, cross) : f.P(cross, y0, a0);
                Vector3 p1 = alongX ? f.P(a1, y1, cross) : f.P(cross, y1, a1);
                Vector3 forward = p1 - p0;
                const float height = 1.0f;
                Quaternion rot = Quaternion.LookRotation(forward.normalized, Vector3.up);
                Vector3 center = (p0 + p1) * 0.5f + Vector3.up * (height * 0.5f);
                Data.Blocks.Add(new BlockSpec
                {
                    Name = name,
                    Group = group,
                    Surface = TriadSurface.MetalGrey,
                    Center = center,
                    Size = new Vector3(0.08f, height, forward.magnitude),
                    Euler = rot.eulerAngles,
                    Solid = true
                });
            }

            private void Rail(Frame f, string name, float lx0, float lx1, float y0, float y1, float lz0, float lz1)
            {
                LBox(f, name, TriadSurface.MetalGrey, lx0, lx1, y0, y1, lz0, lz1);
            }

            private static List<float[]> Spans(float a0, float a1, List<EdgeGap> gaps, char edge)
            {
                var edgeGaps = new List<EdgeGap>();
                foreach (EdgeGap gap in gaps)
                {
                    if (gap.Edge == edge)
                        edgeGaps.Add(gap);
                }

                edgeGaps.Sort((p, q) => p.Center.CompareTo(q.Center));
                var spans = new List<float[]>();
                float cur = a0;
                foreach (EdgeGap gap in edgeGaps)
                {
                    float g0 = gap.Center - gap.Width * 0.5f;
                    float g1 = gap.Center + gap.Width * 0.5f;
                    if (g0 > cur + 0.001f)
                        spans.Add(new[] { cur, g0 });
                    cur = Mathf.Max(cur, g1);
                }

                if (a1 > cur + 0.001f)
                    spans.Add(new[] { cur, a1 });
                return spans;
            }

            private void Parapet(string name, TriadSurface s, Rect r, float y0, float h, float t, params EdgeGap[] gaps)
            {
                var list = new List<EdgeGap>(gaps);
                int n = 0;
                foreach (float[] span in Spans(r.X0, r.X1, list, 'S'))
                    Box(name + "_S" + n++, s, span[0], span[1], y0, y0 + h, r.Z0, r.Z0 + t);
                foreach (float[] span in Spans(r.X0, r.X1, list, 'N'))
                    Box(name + "_N" + n++, s, span[0], span[1], y0, y0 + h, r.Z1 - t, r.Z1);
                foreach (float[] span in Spans(r.Z0 + t, r.Z1 - t, list, 'W'))
                    Box(name + "_W" + n++, s, r.X0, r.X0 + t, y0, y0 + h, span[0], span[1]);
                foreach (float[] span in Spans(r.Z0 + t, r.Z1 - t, list, 'E'))
                    Box(name + "_E" + n++, s, r.X1 - t, r.X1, y0, y0 + h, span[0], span[1]);
            }

            private void BridgeX(string name, float xa, float xb, float zc)
            {
                float x0 = Mathf.Min(xa, xb) - 0.1f, x1 = Mathf.Max(xa, xb) + 0.1f;
                Box(name + "_Deck", TriadSurface.ConcreteDark, x0, x1, 3.65f, 4f, zc - 1.5f, zc + 1.5f);
                Box(name + "_RailA", TriadSurface.MetalGrey, x0, x1, 4f, 5f, zc - 1.5f, zc - 1.4f);
                Box(name + "_RailB", TriadSurface.MetalGrey, x0, x1, 4f, 5f, zc + 1.4f, zc + 1.5f);
            }

            private void BridgeZ(string name, float za, float zb, float xc)
            {
                float z0 = Mathf.Min(za, zb) - 0.1f, z1 = Mathf.Max(za, zb) + 0.1f;
                Box(name + "_Deck", TriadSurface.ConcreteDark, xc - 1.5f, xc + 1.5f, 3.65f, 4f, z0, z1);
                Box(name + "_RailA", TriadSurface.MetalGrey, xc - 1.5f, xc - 1.4f, 4f, 5f, z0, z1);
                Box(name + "_RailB", TriadSurface.MetalGrey, xc + 1.4f, xc + 1.5f, 4f, 5f, z0, z1);
            }

            private void Prop(string name, TriadSurface s, float x, float z, float sx, float sy, float sz, float y0 = 0f)
            {
                float zz = z * zs;
                Box(name + zSuffix, s, x - sx * 0.5f, x + sx * 0.5f, y0, y0 + sy, zz - sz * 0.5f, zz + sz * 0.5f);
            }

            private void CrateAt(string name, float x, float z, float y0 = 0f, float size = 1.1f)
            {
                Prop(name, TriadSurface.Crate, x, z, size, size, size, y0);
            }

            private void Light(string name, Vector3 pos, Color color, float intensity, float range)
            {
                Data.Lights.Add(new LightSpec { Name = name, Position = pos, Color = color, Intensity = intensity, Range = range });
            }

            private static readonly Color InteriorWarm = new Color(1f, 0.88f, 0.66f);
            private static readonly Color LampWarm = new Color(1f, 0.72f, 0.42f);

            #endregion

            #region Ground, perimeter, gates

            private void Ground()
            {
                G("Ground");
                Box("Ground", TriadSurface.Asphalt, -46f, 46f, -1f, 0f, -64f, 64f);
            }

            private void Perimeter()
            {
                G("Perimeter");
                const float h = 9f;
                Box("Wall_West", TriadSurface.ConcreteDark, -45f, -44f, 0f, h, -63f, 63f);
                Box("Wall_East", TriadSurface.ConcreteDark, 44f, 45f, 0f, h, -63f, 63f);
                Box("Wall_South", TriadSurface.ConcreteDark, -45f, 45f, 0f, h, -63f, -62f);
                Box("Wall_North", TriadSurface.ConcreteDark, -45f, 45f, 0f, h, 62f, 63f);
                Box("Banner_TeamA", TriadSurface.ContainerBlue, -14f, 14f, 3f, 5.2f, -61.9f, -62f, false);
                Box("Banner_TeamB", TriadSurface.ContainerRed, -14f, 14f, 3f, 5.2f, 61.9f, 62f, false);
            }

            private void Gates()
            {
                G("Gates");
                float[][] pieces =
                {
                    new[] { -44f, -39f }, new[] { -29f, -5f }, new[] { 5f, 29f }, new[] { 39f, 44f }
                };
                TriadSurface[] beam = { TriadSurface.ContainerBlue, TriadSurface.ContainerYellow, TriadSurface.ContainerRed };
                for (int side = 0; side < 2; side++)
                {
                    float z0 = side == 0 ? -47f : 46f;
                    float z1 = side == 0 ? -46f : 47f;
                    string tag = side == 0 ? "S" : "N";
                    for (int i = 0; i < pieces.Length; i++)
                        Box("Gate_" + tag + "_Wall" + i, TriadSurface.ConcreteDark, pieces[i][0], pieces[i][1], 0f, 4.5f, z0, z1);
                    for (int lane = 0; lane < 3; lane++)
                    {
                        float c = LaneCenters[lane];
                        const float half = 5f;
                        Box("Gate_" + tag + "_Beam_" + LaneNames[lane], beam[lane], c - half, c + half, 3.6f, 4.5f, z0, z1);
                        AddOpening("Gate_" + tag + "_" + LaneNames[lane], new Vector3(c, 1.8f, (z0 + z1) * 0.5f), half * 2f, 3.6f, 0f, true);
                    }
                }
            }

            #endregion

            #region City blocks, slots, bridges

            private void Masses()
            {
                G("Blocks");
                for (int block = 0; block < 2; block++)
                {
                    float xs = block == 0 ? -1f : 1f;
                    TriadSurface body = block == 0 ? TriadSurface.Brick : TriadSurface.BrickLight;
                    string tag = block == 0 ? "W" : "E";
                    float xa = xs < 0f ? -24f : 10f;
                    float xb = xs < 0f ? -10f : 24f;
                    char outerEdge = xs < 0f ? 'W' : 'E';
                    char innerEdge = xs < 0f ? 'E' : 'W';
                    float slotX = 20.5f * xs;
                    float skyX = 13f * xs;
                    float jumpX = 20f * xs;

                    Mass("Block" + tag + "_M1", body, new Rect(xa, xb, -46f, -30f), 'N', slotX,
                        new EdgeGap('N', skyX, 3.8f));
                    Mass("Block" + tag + "_M2", body, new Rect(xa, xb, -22f, -4f), 'S', slotX,
                        new EdgeGap('S', skyX, 3.8f), new EdgeGap('N', skyX, 3.8f), new EdgeGap('N', jumpX, 2.4f),
                        new EdgeGap(outerEdge, -11f, 3.6f), new EdgeGap(innerEdge, -11f, 3.6f));
                    Mass("Block" + tag + "_M3", body, new Rect(xa, xb, 4f, 22f), 'N', slotX,
                        new EdgeGap('N', skyX, 3.8f), new EdgeGap('S', skyX, 3.8f), new EdgeGap('S', jumpX, 2.4f),
                        new EdgeGap(outerEdge, 11f, 3.6f), new EdgeGap(innerEdge, 11f, 3.6f));
                    Mass("Block" + tag + "_M4", body, new Rect(xa, xb, 30f, 46f), 'S', slotX,
                        new EdgeGap('S', skyX, 3.8f));

                    float dir = xs < 0f ? 1f : -1f;
                    JumpStackFromStreet("Jump" + tag + "_M2", jumpX, dir, -4f, 1f);
                    JumpStackFromStreet("Jump" + tag + "_M3", jumpX, dir, 4f, -1f);
                }
            }

            private void Mass(string name, TriadSurface body, Rect r, char slotFace, float slotX, params EdgeGap[] gaps)
            {
                const float slotLength = StairSteps * StairRun;
                Rect slot = slotFace == 'N'
                    ? new Rect(slotX - 1.2f, slotX + 1.2f, r.Z1 - slotLength, r.Z1)
                    : new Rect(slotX - 1.2f, slotX + 1.2f, r.Z0, r.Z0 + slotLength);

                int n = 0;
                foreach (Rect p in SubtractAll(r, slot))
                {
                    Box(name + "_Body" + n, body, p.X0, p.X1, 0f, 3.85f, p.Z0, p.Z1);
                    Box(name + "_Roof" + n, TriadSurface.RoofTar, p.X0, p.X1, 3.85f, 4f, p.Z0, p.Z1);
                    n++;
                }

                if (slotFace == 'N')
                    FlightAt(name + "_Stair", false, r.Z1, -1, slotX - 1.2f, slotX + 1.2f);
                else
                    FlightAt(name + "_Stair", false, r.Z0, 1, slotX - 1.2f, slotX + 1.2f);

                float zStart = slotFace == 'N' ? r.Z1 - slotLength : r.Z0;
                float zEnd = slotFace == 'N' ? r.Z1 : r.Z0 + slotLength;
                Box(name + "_SlotRailA", TriadSurface.MetalGrey, slotX - 1.3f, slotX - 1.2f, 4f, 4.9f, zStart, zEnd);
                Box(name + "_SlotRailB", TriadSurface.MetalGrey, slotX + 1.2f, slotX + 1.3f, 4f, 4.9f, zStart, zEnd);

                var all = new List<EdgeGap>(gaps) { new EdgeGap(slotFace, slotX, 2.8f) };
                Parapet(name + "_Parapet", body, r, 4f, 0.9f, 0.3f, all.ToArray());
            }

            private void FlightAt(string name, bool alongX, float start, int dir, float c0, float c1)
            {
                Flight(new Frame(0f, 0f), name, TriadSurface.Concrete, alongX, start, dir, c0, c1, 0f, 0f);
            }

            private void Skybridges()
            {
                G("Bridges");
                foreach (float xc in new[] { -13f, 13f })
                {
                    string tag = xc < 0f ? "W" : "E";
                    BridgeZ("Sky" + tag + "_South", -30f, -22f, xc);
                    BridgeZ("Sky" + tag + "_Centre", -4f, 4f, xc);
                    BridgeZ("Sky" + tag + "_North", 22f, 30f, xc);
                }

                foreach (float zc in new[] { -11f, 11f })
                {
                    string tag = zc < 0f ? "S" : "N";
                    BridgeX("BridgeWarehouse_" + tag, -28f, -24f, zc);
                    BridgeX("BridgeOfficeW_" + tag, -10f, -6f, zc);
                    BridgeX("BridgeOfficeE_" + tag, 6f, 10f, zc);
                    BridgeX("BridgeDepot_" + tag, 24f, 28f, zc);
                }
            }

            /// <summary>Three stepped crates (1.1 / 2.2 / 3.3 m) leading up to a roof ledge at 4 m.</summary>
            private void JumpStackFromStreet(string name, float c3x, float dir, float faceZ, float streetSign)
            {
                float z0 = faceZ;
                float z1 = faceZ + streetSign * 1.4f;
                float zc = (z0 + z1) * 0.5f;
                float[] heights = { 3.3f, 2.2f, 1.1f };
                var route = new JumpRouteSpec { Name = name };
                var tops = new List<Vector3>();
                for (int i = 0; i < 3; i++)
                {
                    float cx = c3x + dir * 1.5f * i;
                    Box(name + "_Crate" + i, TriadSurface.Crate, cx - 0.7f, cx + 0.7f, 0f, heights[i], Mathf.Min(z0, z1), Mathf.Max(z0, z1));
                    tops.Add(new Vector3(cx, heights[i], zc));
                }

                tops.Reverse();
                route.Tops.AddRange(tops);
                route.Tops.Add(new Vector3(c3x, MidTierY, faceZ - streetSign * 1.0f));
                Data.JumpRoutes.Add(route);
            }

            private void JumpStackFromAlley(string name, float wallX, float outward, float doorZ, float zDir)
            {
                float[] heights = { 3.3f, 2.2f, 1.1f };
                var route = new JumpRouteSpec { Name = name };
                var tops = new List<Vector3>();
                for (int i = 0; i < 3; i++)
                {
                    float zc = doorZ + zDir * 1.5f * i;
                    float x0 = wallX;
                    float x1 = wallX + outward * 1.4f;
                    Box(name + "_Crate" + i, TriadSurface.Crate, Mathf.Min(x0, x1), Mathf.Max(x0, x1), 0f, heights[i], zc - 0.7f, zc + 0.7f);
                    tops.Add(new Vector3((x0 + x1) * 0.5f, heights[i], zc));
                }

                tops.Reverse();
                route.Tops.AddRange(tops);
                route.Tops.Add(new Vector3(wallX - outward * 1.0f, MidTierY, doorZ));
                Data.JumpRoutes.Add(route);
            }

            #endregion

            #region Warehouse (west lane)

            private void Warehouse()
            {
                G("Warehouse");
                var f = new Frame(-34f, 0f);
                TriadSurface wall = TriadSurface.MetalBlue;
                TriadSurface part = TriadSurface.Plaster;

                LBox(f, "Floor", TriadSurface.FloorTile, -5.5f, 5.5f, -0.3f, 0.04f, -13.5f, 13.5f);

                WallX(f, "Wall_S0", wall, -13.75f, -6f, 6f, 0f, 4f, 0.5f, new Op(-2.5f, 2.5f, 0f, 3.4f), new Op(-4.3f, -2.7f, 1.1f, 2.4f));
                WallX(f, "Wall_S1", wall, -13.75f, -6f, 6f, 4f, 8f, 0.5f, new Op(-4f, -2.5f, 5.1f, 6.5f), new Op(2.5f, 4f, 5.1f, 6.5f));
                WallX(f, "Wall_N0", wall, 13.75f, -6f, 6f, 0f, 4f, 0.5f, new Op(-2.5f, 2.5f, 0f, 3.4f), new Op(-4.3f, -2.7f, 1.1f, 2.4f));
                WallX(f, "Wall_N1", wall, 13.75f, -6f, 6f, 4f, 8f, 0.5f, new Op(-4f, -2.5f, 5.1f, 6.5f), new Op(2.5f, 4f, 5.1f, 6.5f));
                WallZ(f, "Wall_W0", wall, -5.75f, -14f, 14f, 0f, 4f, 0.5f,
                    new Op(10.4f, 12.6f, 0f, 2.6f), new Op(-12.2f, -10.6f, 1.1f, 2.4f), new Op(-1f, 1f, 1.1f, 2.4f));
                WallZ(f, "Wall_W1", wall, -5.75f, -14f, 14f, 4f, 8f, 0.5f,
                    new Op(-12.2f, -9.8f, 4f, 6.5f), new Op(-1f, 1f, 5.5f, 7f));
                WallZ(f, "Wall_E0", wall, 5.75f, -14f, 14f, 0f, 4f, 0.5f,
                    new Op(-1.1f, 1.1f, 0f, 2.6f), new Op(-8f, -6.4f, 1.1f, 2.4f), new Op(6.4f, 8f, 1.1f, 2.4f));
                WallZ(f, "Wall_E1", wall, 5.75f, -14f, 14f, 4f, 8f, 0.5f,
                    new Op(-12.2f, -9.8f, 4f, 6.5f), new Op(9.8f, 12.2f, 4f, 6.5f), new Op(-1f, 1f, 5.5f, 7f));

                Slab(f, "MezzS", TriadSurface.ConcreteDark, 3.6f, 4f, new Rect(-5.5f, 5.5f, -13.5f, -9f));
                Slab(f, "MezzN", TriadSurface.ConcreteDark, 3.6f, 4f, new Rect(-5.5f, 5.5f, 9f, 13.5f));
                Slab(f, "MezzE", TriadSurface.ConcreteDark, 3.6f, 4f, new Rect(0.5f, 5.5f, -9f, 9f));
                Slab(f, "Roof", TriadSurface.RoofTar, 7.6f, 8f, new Rect(-5.5f, 5.5f, -13.5f, 13.5f), new Rect(3.6f, 5.5f, -7f, -1f));

                Flight(f, "StairA", TriadSurface.Concrete, false, -3f, -1, -5.4f, -3.6f, 0f, 0f);
                WallX(f, "StairA_Wall", TriadSurface.Concrete, -8.9f, -5.5f, -0.5f, 0f, 3.6f, 0.2f);
                SlopedRail(f, "StairA_Rail", false, -3f, -1, 0f, 0, -3.56f);
                Rail(f, "MezzE_Rail", 0.4f, 0.5f, 4f, 5f, -9f, 9f);
                Rail(f, "MezzN_Rail", -5.5f, 0.4f, 4f, 5f, 8.9f, 9f);

                Flight(f, "StairB", TriadSurface.Concrete, false, -7f, 1, 3.6f, 5.4f, 4f, 4f);
                SlopedRail(f, "StairB_Rail", false, -7f, 1, 4f, 0, 3.56f);
                Rail(f, "Roof_HoleRail", 3.5f, 3.6f, 8f, 9f, -7.1f, -1f);
                Rail(f, "Roof_HoleRailEnd", 3.6f, 5.5f, 8f, 9f, -7.1f, -7f);

                Parapet("Parapet", wall, new Rect(f.X(-6f), f.X(6f), f.Z(-14f), f.Z(14f)), 8f, 1f, 0.4f);

                WallZ(f, "Office_E", part, -0.5f, -13.5f, -9f, 0f, 3.6f, 0.3f, new Op(-11.6f, -10.2f, 0f, 2.4f));
                WallX(f, "Break_S", part, 9f, -5.5f, -0.5f, 0f, 3.6f, 0.3f, new Op(-4f, -2f, 1f, 2.2f));
                WallZ(f, "Break_E", part, -0.5f, 9f, 13.5f, 0f, 3.6f, 0.3f, new Op(10.2f, 11.8f, 0f, 2.4f));

                LBox(f, "RackA", TriadSurface.MetalRust, 3f, 5f, 0f, 2.2f, -2f, -1.1f);
                Rail(f, "MezzS_Rail", -3.6f, 0.5f, 4f, 5f, -9.1f, -9f);
                LBox(f, "RackB", TriadSurface.MetalRust, -4.9f, -2.5f, 0f, 2.2f, 3.6f, 4.5f);
                LBox(f, "CrateH0", TriadSurface.Crate, -1.75f, -0.65f, 0f, 1.1f, -0.55f, 0.55f);
                LBox(f, "CrateH1", TriadSurface.Crate, -1.75f, -0.65f, 1.1f, 2.2f, -0.55f, 0.55f);
                LBox(f, "CrateH2", TriadSurface.Crate, -1.75f, -0.65f, 0f, 1.1f, 0.65f, 1.75f);
                LBox(f, "CrateUnderA", TriadSurface.Crate, 3.65f, 4.75f, 0f, 1.1f, -6.05f, -4.95f);
                LBox(f, "CrateUnderB", TriadSurface.Crate, 3.65f, 4.75f, 0f, 1.1f, 4.65f, 5.75f);
                LBox(f, "CrateMezzS", TriadSurface.Crate, 2.45f, 3.55f, 4f, 5.1f, -12.9f, -11.8f);
                LBox(f, "CrateMezzN", TriadSurface.Crate, 3.2f, 4.3f, 4f, 5.1f, 11.8f, 12.9f);
                LBox(f, "DeskOffice", TriadSurface.Wood, -5.2f, -3.8f, 0f, 0.75f, -13f, -12.2f);
                LBox(f, "TableBreak", TriadSurface.Wood, -5.2f, -3.8f, 0f, 0.75f, 12.2f, 13f);

                LBox(f, "RoofAC", TriadSurface.MetalGrey, 2f, 4f, 8f, 9.2f, -9f, -7.6f);
                LBox(f, "RoofCrate", TriadSurface.Crate, -4f, -2.9f, 8f, 9.1f, -7f, -5.9f);
                LBox(f, "RoofCover", TriadSurface.Concrete, -3f, -0.5f, 8f, 9f, 4f, 4.4f);
                LBox(f, "RoofCoverB", TriadSurface.Concrete, 1.5f, 4f, 8f, 9f, 7f, 7.4f);
                LBox(f, "Chimney", TriadSurface.MetalRust, 4.2f, 5.3f, 8f, 11f, 8.6f, 10f);

                JumpStackFromAlley("JumpWarehouse", f.X(-6f), -1f, f.Z(-11f), -1f);

                Light("Warehouse_Hall", f.P(0f, 6.6f, 0f), InteriorWarm, 1.2f, 11f);
                Light("Warehouse_HallS", f.P(0f, 6.6f, -6.5f), InteriorWarm, 1.1f, 10f);
                Light("Warehouse_HallN", f.P(0f, 6.6f, 6.5f), InteriorWarm, 1.1f, 10f);
                Light("Warehouse_Under", f.P(4f, 3.1f, 0f), InteriorWarm, 1f, 7f);
                Light("Warehouse_Office", f.P(-3f, 3f, -11.2f), InteriorWarm, 1f, 6f);
                Light("Warehouse_Break", f.P(-3f, 3f, 11.2f), InteriorWarm, 1f, 6f);
            }

            #endregion

            #region Office (middle lane)

            private void Office()
            {
                G("Office");
                var f = new Frame(0f, 0f);
                TriadSurface wall = TriadSurface.Concrete;
                TriadSurface part = TriadSurface.Plaster;

                LBox(f, "Floor", TriadSurface.FloorTile, -5.5f, 5.5f, -0.3f, 0.04f, -13.5f, 13.5f);

                WallX(f, "Wall_S0", wall, -13.75f, -6f, 6f, 0f, 4f, 0.5f,
                    new Op(-1.2f, 1.2f, 0f, 2.7f), new Op(-4.8f, -2.8f, 1.1f, 2.4f), new Op(2.8f, 4.8f, 1.1f, 2.4f));
                WallX(f, "Wall_S1", wall, -13.75f, -6f, 6f, 4f, 8f, 0.5f,
                    new Op(-4.8f, -2.8f, 5.1f, 6.4f), new Op(2.8f, 4.8f, 5.1f, 6.4f));
                WallX(f, "Wall_N0", wall, 13.75f, -6f, 6f, 0f, 4f, 0.5f,
                    new Op(-1.2f, 1.2f, 0f, 2.7f), new Op(-4.8f, -2.8f, 1.1f, 2.4f), new Op(2.8f, 4.8f, 1.1f, 2.4f));
                WallX(f, "Wall_N1", wall, 13.75f, -6f, 6f, 4f, 8f, 0.5f,
                    new Op(-4.8f, -2.8f, 5.1f, 6.4f), new Op(2.8f, 4.8f, 5.1f, 6.4f));
                WallZ(f, "Wall_W0", wall, -5.75f, -14f, 14f, 0f, 4f, 0.5f,
                    new Op(-1.2f, 1.2f, 0f, 2.6f), new Op(-11.7f, -10.1f, 1.1f, 2.4f), new Op(-6.4f, -4.8f, 1.1f, 2.4f), new Op(11.2f, 12.8f, 1.1f, 2.4f));
                WallZ(f, "Wall_W1", wall, -5.75f, -14f, 14f, 4f, 8f, 0.5f,
                    new Op(-12.2f, -9.8f, 4f, 6.5f), new Op(9.8f, 12.2f, 4f, 6.5f), new Op(-6.4f, -4.8f, 5.1f, 6.4f), new Op(-1f, 1f, 5.1f, 6.4f));
                WallZ(f, "Wall_E0", wall, 5.75f, -14f, 14f, 0f, 4f, 0.5f,
                    new Op(-1.2f, 1.2f, 0f, 2.6f), new Op(-11.7f, -10.1f, 1.1f, 2.4f), new Op(-6.4f, -4.8f, 1.1f, 2.4f),
                    new Op(4.8f, 6.4f, 1.1f, 2.4f), new Op(10.1f, 11.7f, 1.1f, 2.4f));
                WallZ(f, "Wall_E1", wall, 5.75f, -14f, 14f, 4f, 8f, 0.5f,
                    new Op(-12.2f, -9.8f, 4f, 6.5f), new Op(9.8f, 12.2f, 4f, 6.5f), new Op(-6.4f, -4.8f, 5.1f, 6.4f), new Op(-1f, 1f, 5.1f, 6.4f));

                Slab(f, "Floor2", TriadSurface.ConcreteDark, 3.6f, 4f, new Rect(-5.5f, 5.5f, -13.5f, 13.5f), new Rect(-5.5f, -3.7f, 3f, 9f));
                Slab(f, "Roof", TriadSurface.RoofTar, 7.6f, 8f, new Rect(-5.5f, 5.5f, -13.5f, 13.5f), new Rect(3.7f, 5.5f, 3f, 9f));

                Flight(f, "StairA", TriadSurface.Concrete, false, 3f, 1, -5.5f, -3.7f, 0f, 0f);
                Rail(f, "Hole_RailE", -3.8f, -3.7f, 4f, 5f, 3f, 9f);
                Rail(f, "Hole_RailS", -5.5f, -3.7f, 4f, 5f, 2.9f, 3f);

                Flight(f, "StairB", TriadSurface.Concrete, false, 3f, 1, 3.7f, 5.5f, 4f, 4f);
                SlopedRail(f, "StairB_Rail", false, 3f, 1, 4f, 6, 3.66f);
                Rail(f, "Roof_HoleRailW", 3.6f, 3.7f, 8f, 9f, 3f, 9f);
                Rail(f, "Roof_HoleRailS", 3.7f, 5.5f, 8f, 9f, 2.9f, 3f);

                Parapet("Parapet", wall, new Rect(f.X(-6f), f.X(6f), f.Z(-14f), f.Z(14f)), 8f, 1f, 0.4f);

                Op[] southDoors = { new Op(-11.6f, -10.2f, 0f, 2.4f), new Op(-6.3f, -4.9f, 0f, 2.4f) };
                WallZ(f, "Spine_WS", part, -1.5f, -13.5f, -3f, 0f, 3.6f, 0.3f, southDoors);
                WallZ(f, "Spine_WN", part, -1.5f, 3f, 13.5f, 0f, 3.6f, 0.3f);
                WallZ(f, "Spine_ES", part, 1.5f, -13.5f, -3f, 0f, 3.6f, 0.3f, southDoors);
                WallZ(f, "Spine_EN", part, 1.5f, 3f, 13.5f, 0f, 3.6f, 0.3f, new Op(4.9f, 6.3f, 0f, 2.4f), new Op(10.2f, 11.6f, 0f, 2.4f));
                WallX(f, "Cross_WS", part, -8.25f, -5.5f, -1.5f, 0f, 3.6f, 0.3f);
                WallX(f, "Cross_ES", part, -8.25f, 1.5f, 5.5f, 0f, 3.6f, 0.3f);
                WallX(f, "Cross_EN", part, 8.25f, 1.5f, 5.5f, 0f, 3.6f, 0.3f);

                Op[] southDoors2 = { new Op(-11.6f, -10.2f, 4f, 6.4f), new Op(-6.3f, -4.9f, 4f, 6.4f) };
                WallZ(f, "Spine2_WS", part, -1.5f, -13.5f, -3f, 4f, 7.6f, 0.3f, southDoors2);
                WallZ(f, "Spine2_WN", part, -1.5f, 3f, 13.5f, 4f, 7.6f, 0.3f, new Op(10.4f, 11.8f, 4f, 6.4f));
                WallZ(f, "Spine2_ES", part, 1.5f, -13.5f, -3f, 4f, 7.6f, 0.3f, southDoors2);
                WallZ(f, "Spine2_EN", part, 1.5f, 3f, 13.5f, 4f, 7.6f, 0.3f, new Op(10.3f, 11.7f, 4f, 6.4f));
                WallX(f, "Cross2_WS", part, -8.25f, -5.5f, -1.5f, 4f, 7.6f, 0.3f);
                WallX(f, "Cross2_ES", part, -8.25f, 1.5f, 5.5f, 4f, 7.6f, 0.3f);

                LBox(f, "Reception", TriadSurface.Wood, -1f, 1f, 0f, 1.1f, -0.4f, 0.4f);
                LBox(f, "Reception2", TriadSurface.Wood, -1f, 1f, 4f, 5.1f, -0.4f, 0.4f);
                LBox(f, "DeskSW", TriadSurface.Wood, -5.2f, -3.8f, 0f, 0.75f, -13.1f, -12.3f);
                LBox(f, "DeskSE", TriadSurface.Wood, 3.8f, 5.2f, 0f, 0.75f, -13.1f, -12.3f);
                LBox(f, "DeskNE", TriadSurface.Wood, 3.8f, 5.2f, 0f, 0.75f, 12.3f, 13.1f);
                LBox(f, "DeskSW2", TriadSurface.Wood, -5.2f, -3.8f, 4f, 4.75f, -13.1f, -12.3f);
                LBox(f, "DeskSE2", TriadSurface.Wood, 3.8f, 5.2f, 4f, 4.75f, -13.1f, -12.3f);
                LBox(f, "Cabinet", TriadSurface.MetalGrey, 2f, 2.9f, 4f, 5.6f, 12.3f, 13.3f);

                LBox(f, "RoofAC", TriadSurface.MetalGrey, -4.5f, -2.5f, 8f, 9.2f, -9f, -7.6f);
                LBox(f, "RoofAC2", TriadSurface.MetalGrey, 2.5f, 4.5f, 8f, 9.2f, -9f, -7.6f);
                LBox(f, "RoofCover", TriadSurface.Concrete, -4f, -1.5f, 8f, 9f, 5f, 5.4f);
                LBox(f, "RoofCrate", TriadSurface.Crate, -4f, -2.9f, 8f, 9.1f, 9f, 10.1f);
                LBox(f, "Antenna", TriadSurface.MetalGrey, 0f, 0.3f, 8f, 13f, -12f, -11.7f);

                Light("Office_Lobby", f.P(0f, 3.2f, 0f), InteriorWarm, 1.2f, 8f);
                Light("Office_CorridorS", f.P(0f, 3.2f, -8f), InteriorWarm, 1f, 7f);
                Light("Office_CorridorN", f.P(0f, 3.2f, 8f), InteriorWarm, 1f, 7f);
                Light("Office_Lobby2", f.P(0f, 7.2f, 0f), InteriorWarm, 1.2f, 8f);
                Light("Office_Corridor2S", f.P(0f, 7.2f, -8f), InteriorWarm, 1f, 7f);
                Light("Office_Corridor2N", f.P(0f, 7.2f, 8f), InteriorWarm, 1f, 7f);
            }

            #endregion

            #region Depot (east lane)

            private void Depot()
            {
                G("Depot");
                var f = new Frame(34f, 0f);
                TriadSurface wall = TriadSurface.BrickDark;
                TriadSurface part = TriadSurface.Concrete;

                LBox(f, "Floor", TriadSurface.FloorTile, -5.5f, 5.5f, -0.3f, 0.04f, -13.5f, 13.5f);

                WallX(f, "Wall_S0", wall, -13.75f, -6f, 6f, 0f, 4f, 0.5f,
                    new Op(-3f, 3f, 0f, 3.4f), new Op(-5.3f, -4.1f, 1.1f, 2.4f), new Op(4.1f, 5.3f, 1.1f, 2.4f));
                WallX(f, "Wall_S1", wall, -13.75f, -6f, 6f, 4f, 8f, 0.5f,
                    new Op(-4f, -2.4f, 5.1f, 6.5f), new Op(2.4f, 4f, 5.1f, 6.5f));
                WallX(f, "Wall_N0", wall, 13.75f, -6f, 6f, 0f, 4f, 0.5f,
                    new Op(-1f, 1f, 0f, 2.6f), new Op(-4.45f, -2.85f, 1.1f, 2.4f), new Op(2.85f, 4.45f, 1.1f, 2.4f));
                WallX(f, "Wall_N1", wall, 13.75f, -6f, 6f, 4f, 8f, 0.5f,
                    new Op(-4.5f, -2.9f, 5.1f, 6.5f), new Op(2.9f, 4.5f, 5.1f, 6.5f));
                WallZ(f, "Wall_W0", wall, -5.75f, -14f, 14f, 0f, 4f, 0.5f,
                    new Op(-1.1f, 1.1f, 0f, 2.6f), new Op(-10.3f, -8.7f, 1.1f, 2.4f), new Op(8.7f, 10.3f, 1.1f, 2.4f));
                WallZ(f, "Wall_W1", wall, -5.75f, -14f, 14f, 4f, 8f, 0.5f,
                    new Op(-12.2f, -9.8f, 4f, 6.5f), new Op(9.8f, 12.2f, 4f, 6.5f));
                WallZ(f, "Wall_E0", wall, 5.75f, -14f, 14f, 0f, 4f, 0.5f,
                    new Op(-1.1f, 1.1f, 0f, 2.6f), new Op(-10.3f, -8.7f, 1.1f, 2.4f));
                WallZ(f, "Wall_E1", wall, 5.75f, -14f, 14f, 4f, 8f, 0.5f,
                    new Op(-12.2f, -9.8f, 4f, 6.5f), new Op(9.8f, 12.2f, 4f, 6.5f), new Op(-1f, 1f, 5.1f, 6.5f));

                Slab(f, "Floor2", TriadSurface.ConcreteDark, 3.6f, 4f, new Rect(-5.5f, 5.5f, -13.5f, 13.5f),
                    new Rect(3.6f, 5.5f, -5f, 1.3f), new Rect(-2f, 2f, -3f, 3f));
                Slab(f, "Roof", TriadSurface.RoofTar, 7.6f, 8f, new Rect(-5.5f, 5.5f, -13.5f, 13.5f), new Rect(-5.5f, -3.7f, -3f, 3f));

                Flight(f, "StairA", TriadSurface.Concrete, false, -4.7f, 1, 3.6f, 5.4f, 0f, 0f);
                Rail(f, "Hole_RailW", 3.5f, 3.6f, 4f, 5f, -5f, 1.3f);
                Rail(f, "Hole_RailS", 3.6f, 5.5f, 4f, 5f, -5.1f, -5f);
                Rail(f, "Void_RailW", -2.1f, -2f, 4f, 5f, -3f, 3f);
                Rail(f, "Void_RailE", 2f, 2.1f, 4f, 5f, -3f, 3f);
                Rail(f, "Void_RailS", -2f, 2f, 4f, 5f, -3.1f, -3f);
                Rail(f, "Void_RailN", -2f, 2f, 4f, 5f, 3f, 3.1f);

                Flight(f, "StairB", TriadSurface.Concrete, false, -3f, 1, -5.5f, -3.7f, 4f, 4f);
                SlopedRail(f, "StairB_Rail", false, -3f, 1, 4f, 6, -3.66f);
                Rail(f, "Roof_HoleRailE", -3.8f, -3.7f, 8f, 9f, -3f, 3f);
                Rail(f, "Roof_HoleRailS", -5.5f, -3.7f, 8f, 9f, -3.1f, -3f);

                Flight(f, "StairExt", TriadSurface.Concrete, false, 3.4f, 1, 6f, 7.8f, 0f, 0f);
                LBox(f, "StairExt_Landing", TriadSurface.Concrete, 6f, 7.8f, 0f, 4f, 9.4f, 12.6f);
                SlopedRail(f, "StairExt_Rail", false, 3.4f, 1, 0f, 6, 7.76f);
                Rail(f, "StairExt_LandingRail", 7.7f, 7.8f, 4f, 5f, 9.4f, 12.6f);
                Rail(f, "StairExt_EndRail", 6f, 7.8f, 4f, 5f, 12.6f, 12.7f);

                Parapet("Parapet", wall, new Rect(f.X(-6f), f.X(6f), f.Z(-14f), f.Z(14f)), 8f, 1f, 0.4f);

                Op[] bayDoors = { new Op(-4.7f, -3.1f, 0f, 2.4f), new Op(3.1f, 4.7f, 0f, 2.4f) };
                WallX(f, "Part_S", part, -5f, -5.5f, 5.5f, 0f, 3.6f, 0.3f, bayDoors);
                WallX(f, "Part_N", part, 5f, -5.5f, 5.5f, 0f, 3.6f, 0.3f, bayDoors);
                WallZ(f, "Room_W", part, -1.8f, 5f, 13.5f, 0f, 3.6f, 0.3f, new Op(8.3f, 9.7f, 0f, 2.4f));
                WallZ(f, "Room_E", part, 1.8f, 5f, 13.5f, 0f, 3.6f, 0.3f, new Op(8.3f, 9.7f, 0f, 2.4f));

                WallX(f, "Control_N", part, -8f, -5.5f, -1.5f, 4f, 7.6f, 0.3f, new Op(-3.4f, -2f, 4f, 6.4f));
                WallZ(f, "Control_E", part, -1.5f, -13.5f, -8f, 4f, 7.6f, 0.3f, new Op(-11.7f, -10.3f, 4f, 6.4f));
                WallZ(f, "Office2_E", part, 1.8f, 5f, 13.5f, 4f, 7.6f, 0.3f, new Op(8.3f, 9.7f, 4f, 6.4f));

                LBox(f, "PillarA", TriadSurface.ConcreteDark, -3.4f, -2.6f, 0f, 3.6f, -2.6f, -1.8f);
                LBox(f, "PillarB", TriadSurface.ConcreteDark, -3.4f, -2.6f, 0f, 3.6f, 1.8f, 2.6f);
                LBox(f, "PillarC", TriadSurface.ConcreteDark, 2.6f, 3.4f, 0f, 3.6f, -2.6f, -1.8f);
                LBox(f, "PillarD", TriadSurface.ConcreteDark, 2.6f, 3.4f, 0f, 3.6f, 1.8f, 2.6f);
                LBox(f, "Cage", TriadSurface.MetalGrey, -1.5f, 1.5f, 0f, 2.2f, -0.2f, 0.2f);
                LBox(f, "CrateBayA", TriadSurface.Crate, -3.7f, -2.6f, 0f, 1.1f, -10.6f, -9.5f);
                LBox(f, "CrateBayB", TriadSurface.Crate, -3.7f, -2.6f, 1.1f, 2.2f, -10.6f, -9.5f);
                LBox(f, "CrateBayC", TriadSurface.Crate, 2f, 3.1f, 0f, 1.1f, -8f, -6.9f);
                LBox(f, "DeskNorth", TriadSurface.Wood, -0.8f, 0.8f, 0f, 0.75f, 12.4f, 13.2f);
                LBox(f, "DeskWest", TriadSurface.Wood, -5.2f, -3.8f, 0f, 0.75f, 12.4f, 13.2f);
                LBox(f, "Crate2F_A", TriadSurface.Crate, -4.7f, -3.6f, 4f, 5.1f, 6f, 7.1f);
                LBox(f, "Crate2F_B", TriadSurface.Crate, 3f, 4.1f, 4f, 5.1f, -12.9f, -11.8f);

                LBox(f, "RoofAC", TriadSurface.MetalGrey, 2f, 4f, 8f, 9.2f, -9f, -7.6f);
                LBox(f, "RoofAC2", TriadSurface.MetalGrey, -4.5f, -2.5f, 8f, 9.2f, 8f, 9.4f);
                LBox(f, "RoofCover", TriadSurface.Concrete, -2f, 1f, 8f, 9f, -5f, -4.6f);
                LBox(f, "RoofCrate", TriadSurface.Crate, 3.5f, 4.6f, 8f, 9.1f, 5f, 6.1f);
                LBox(f, "WaterTank", TriadSurface.MetalRust, 1f, 3.2f, 8f, 10.6f, 9.5f, 11.7f);

                JumpStackFromAlley("JumpDepot", f.X(6f), 1f, f.Z(-11f), -1f);

                Light("Depot_Bay", f.P(0f, 3.1f, -9.5f), InteriorWarm, 1.1f, 8f);
                Light("Depot_Hall", f.P(0f, 3.1f, 0f), InteriorWarm, 1.1f, 8f);
                Light("Depot_RoomsN", f.P(0f, 3.1f, 10f), InteriorWarm, 1f, 8f);
                Light("Depot_Void", f.P(0f, 6.8f, 0f), InteriorWarm, 1.2f, 10f);
                Light("Depot_Upper_S", f.P(0f, 7f, -10f), InteriorWarm, 1f, 8f);
                Light("Depot_Upper_N", f.P(-2.5f, 7f, 10f), InteriorWarm, 1f, 8f);
            }

            #endregion

            #region Props, lamps, skyline

            private void StreetProps()
            {
                G("Props");
                foreach (float sign in new[] { 1f, -1f })
                {
                    zs = sign;
                    zSuffix = sign > 0f ? "_S" : "_N";

                    Prop("Barrier_YardW", TriadSurface.Barrier, -34f, -49.8f, 4f, 0.9f, 0.6f);
                    Prop("Barrier_YardM", TriadSurface.Barrier, 0f, -49.8f, 4f, 0.9f, 0.6f);
                    Prop("Barrier_YardE", TriadSurface.Barrier, 34f, -49.8f, 4f, 0.9f, 0.6f);
                    Prop("Container_Yard1", TriadSurface.ContainerBlue, -14f, -57f, 6f, 2.4f, 2.4f);
                    Prop("Container_Yard2", TriadSurface.ContainerRed, 14f, -57f, 6f, 2.4f, 2.4f);
                    CrateAt("Crate_Yard1", -22f, -53f);
                    CrateAt("Crate_Yard2", -22f, -53f, 1.1f);
                    CrateAt("Crate_Yard3", 22f, -53f);
                    CrateAt("Crate_Yard4", 23.2f, -53f);

                    Prop("Container_W1", TriadSurface.ContainerGreen, -38f, -40f, 2.4f, 2.4f, 6f);
                    CrateAt("Crate_W1", -30f, -33f);
                    CrateAt("Crate_W2", -28.8f, -33f);
                    CrateAt("Crate_W3", -29.4f, -33f, 1.1f);
                    Prop("Barrier_W1", TriadSurface.Barrier, -33f, -26f, 3f, 0.9f, 0.6f);
                    Prop("Barrier_W2", TriadSurface.Barrier, -27f, -42f, 0.6f, 0.9f, 3f);
                    Prop("Van_W", TriadSurface.ContainerYellow, -42f, -20f, 2f, 1.9f, 4.5f);

                    Prop("Planter_M1", TriadSurface.Concrete, -6f, -38f, 2.5f, 1f, 2.5f);
                    Prop("Planter_M2", TriadSurface.Concrete, 6f, -38f, 2.5f, 1f, 2.5f);
                    Prop("Truck_M", TriadSurface.MetalGrey, -6.5f, -30f, 2.6f, 2.8f, 7f);
                    Prop("Barrier_M", TriadSurface.Barrier, 0f, -30f, 4f, 0.9f, 0.6f);
                    CrateAt("Crate_M1", 4f, -22f);
                    CrateAt("Crate_M2", 5.2f, -22f);

                    Prop("Container_E1", TriadSurface.ContainerRed, 38f, -38f, 2.4f, 2.4f, 6f);
                    Prop("Container_E2", TriadSurface.ContainerBlue, 30f, -28f, 6f, 2.4f, 2.4f);
                    CrateAt("Crate_E1", 41f, -22f);
                    CrateAt("Crate_E2", 41f, -23.2f);
                    Prop("Barrier_E1", TriadSurface.Barrier, 35f, -44f, 3f, 0.9f, 0.6f);
                    Prop("Hazard_E", TriadSurface.Hazard, 29f, -18f, 0.6f, 0.9f, 3f);

                    Prop("Barrier_CentreW", TriadSurface.Barrier, -14f, 0f, 0.6f, 0.9f, 3.4f);
                    Prop("Barrier_CentreE", TriadSurface.Barrier, 14f, 0f, 0.6f, 0.9f, 3.4f);
                    CrateAt("Crate_CrossW", -11f, -24f);
                    CrateAt("Crate_CrossE", 11f, -24f);
                }

                zs = 1f;
                zSuffix = "";
            }

            private void RoofProps()
            {
                G("RoofProps");
                foreach (float sign in new[] { 1f, -1f })
                {
                    zs = sign;
                    zSuffix = sign > 0f ? "_S" : "_N";
                    foreach (float xs in new[] { -1f, 1f })
                    {
                        string t = xs < 0f ? "W" : "E";
                        Prop("RoofAC_M1" + t, TriadSurface.MetalGrey, 16f * xs, -40f, 2f, 1.2f, 1.4f, 4f);
                        Prop("RoofCover_M1" + t, TriadSurface.Concrete, 13f * xs, -35f, 4f, 0.9f, 0.4f, 4f);
                        Prop("RoofCrate_M1" + t, TriadSurface.Crate, 21f * xs, -43f, 1.1f, 1.1f, 1.1f, 4f);
                        Prop("RoofAC_M2" + t, TriadSurface.MetalGrey, 18f * xs, -18f, 2f, 1.2f, 1.4f, 4f);
                        Prop("RoofCover_M2" + t, TriadSurface.Concrete, 17f * xs, -10f, 4f, 0.9f, 0.4f, 4f);
                        Prop("RoofCrate_M2" + t, TriadSurface.Crate, 21f * xs, -7f, 1.1f, 1.1f, 1.1f, 4f);
                        Prop("RoofTank_M2" + t, TriadSurface.MetalRust, 22.7f * xs, -14f, 1.8f, 1.8f, 1.8f, 4f);
                    }
                }

                zs = 1f;
                zSuffix = "";
            }

            private void Lamps()
            {
                G("Lamps");
                float[] xs = { -25f, -9f, 9f, 25f };
                float[] zsList = { -40f, -20f };
                int n = 0;
                foreach (float sign in new[] { 1f, -1f })
                {
                    foreach (float x in xs)
                    {
                        foreach (float z in zsList)
                        {
                            float zz = z * sign;
                            Box("LampPost" + n, TriadSurface.MetalGrey, x - 0.12f, x + 0.12f, 0f, 5f, zz - 0.12f, zz + 0.12f);
                            Box("LampHead" + n, TriadSurface.Lamp, x - 0.45f, x + 0.45f, 5f, 5.2f, zz - 0.25f, zz + 0.25f, false);
                            Light("Lamp" + n, new Vector3(x, 4.8f, zz), LampWarm, 1.5f, 14f);
                            n++;
                        }
                    }
                }

                foreach (float sign in new[] { 1f, -1f })
                {
                    foreach (float c in LaneCenters)
                    {
                        float zz = 49f * sign;
                        Light("GateLight" + n++, new Vector3(c, 5.2f, zz), LampWarm, 1.6f, 16f);
                    }

                    Light("YardLight" + n++, new Vector3(-18f, 5f, 56f * sign), LampWarm, 1.3f, 16f);
                    Light("YardLight" + n++, new Vector3(18f, 5f, 56f * sign), LampWarm, 1.3f, 16f);
                }
            }

            private void Skyline()
            {
                G("Skyline");
                var rng = new System.Random(7);
                int n = 0;
                for (float z = -112f; z <= 112f; z += 15f)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float x = side * (58f + (float)rng.NextDouble() * 16f);
                        float w = 9f + (float)rng.NextDouble() * 8f;
                        float h = 16f + (float)rng.NextDouble() * 34f;
                        float d = 10f + (float)rng.NextDouble() * 6f;
                        Box("Skyline" + n++, TriadSurface.Skyline, x - w * 0.5f, x + w * 0.5f, 0f, h, z - d * 0.5f, z + d * 0.5f, false);
                    }
                }

                for (float x = -52f; x <= 52f; x += 17f)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float z = side * (84f + (float)rng.NextDouble() * 18f);
                        float w = 10f + (float)rng.NextDouble() * 8f;
                        float h = 18f + (float)rng.NextDouble() * 38f;
                        float d = 9f + (float)rng.NextDouble() * 6f;
                        Box("Skyline" + n++, TriadSurface.Skyline, x - w * 0.5f, x + w * 0.5f, 0f, h, z - d * 0.5f, z + d * 0.5f, false);
                    }
                }
            }

            #endregion

            #region Spawns, waypoints, areas, routes

            private void Spawns()
            {
                float[] xs = { -37f, -34f, -31f, -3f, 0f, 3f, 31f, 34f, 37f };
                float[] zsA = { -55f, -53f, -55f, -55f, -53f, -55f, -55f, -53f, -55f };
                for (int team = 0; team < 2; team++)
                {
                    float sign = team == 0 ? 1f : -1f;
                    for (int i = 0; i < xs.Length; i++)
                    {
                        string name = team == 0 && i == 4 ? "PlayerSpawn" : "Spawn_" + (team == 0 ? "A" : "B") + (i + 1).ToString("00");
                        Data.Spawns.Add(new SpawnSpec
                        {
                            Name = name,
                            Team = team,
                            Position = new Vector3(xs[i], 0.05f, zsA[i] * sign),
                            Yaw = team == 0 ? 0f : 180f
                        });
                    }
                }

                int playerIndex = Data.Spawns.FindIndex(sp => sp.Name == "PlayerSpawn");
                SpawnSpec player = Data.Spawns[playerIndex];
                Data.Spawns.RemoveAt(playerIndex);
                Data.Spawns.Insert(0, player);
            }

            private void Wp(string name, string area, float x, float y, float z, TriadTier tier)
            {
                Data.Waypoints.Add(new WaypointSpec { Name = name, Area = area, Position = new Vector3(x, y, z), Tier = tier });
            }

            private void Waypoints()
            {
                foreach (float sign in new[] { 1f, -1f })
                {
                    string h = sign > 0f ? "S" : "N";
                    Wp("Yard_" + h + "_W", "Yard", -34f, 0.05f, -54f * sign, TriadTier.Ground);
                    Wp("Yard_" + h + "_M", "Yard", 0f, 0.05f, -56f * sign, TriadTier.Ground);
                    Wp("Yard_" + h + "_E", "Yard", 34f, 0.05f, -54f * sign, TriadTier.Ground);
                    Wp("Street_W_" + h + "1", "LaneWest", -34f, 0.05f, -38f * sign, TriadTier.Ground);
                    Wp("Street_W_" + h + "2", "LaneWest", -34f, 0.05f, -22f * sign, TriadTier.Ground);
                    Wp("Street_M_" + h + "1", "LaneMiddle", 0f, 0.05f, -38f * sign, TriadTier.Ground);
                    Wp("Street_M_" + h + "2", "LaneMiddle", 0f, 0.05f, -20f * sign, TriadTier.Ground);
                    Wp("Street_E_" + h + "1", "LaneEast", 34f, 0.05f, -38f * sign, TriadTier.Ground);
                    Wp("Street_E_" + h + "2", "LaneEast", 34f, 0.05f, -20f * sign, TriadTier.Ground);
                    Wp("Cross_W_" + h, "Cross", -17f, 0.05f, -26f * sign, TriadTier.Ground);
                    Wp("Cross_E_" + h, "Cross", 17f, 0.05f, -26f * sign, TriadTier.Ground);
                    Wp("Alley_W_" + h, "LaneWest", -26f, 0.05f, -10f * sign, TriadTier.Ground);
                    Wp("Alley_ME_W_" + h, "LaneMiddle", -8f, 0.05f, -10f * sign, TriadTier.Ground);
                    Wp("Alley_MW_E_" + h, "LaneMiddle", 8f, 0.05f, -10f * sign, TriadTier.Ground);
                    Wp("Alley_E_" + h, "LaneEast", 26f, 0.05f, -10f * sign, TriadTier.Ground);
                    Wp("Outer_W_" + h, "LaneWest", -43f, 0.05f, -8f * sign, TriadTier.Ground);
                    Wp("Outer_E_" + h, "LaneEast", 43f, 0.05f, -8f * sign, TriadTier.Ground);
                    foreach (float xs in new[] { -1f, 1f })
                    {
                        string t = xs < 0f ? "W" : "E";
                        Wp("Roof_M1" + t + "_" + h, "Roof", 12f * xs, 4f, -38f * sign, TriadTier.Mid);
                        Wp("Roof_M2" + t + "_" + h, "Roof", 16f * xs, 4f, -15f * sign, TriadTier.Mid);
                    }
                }

                Wp("Cross_Centre_W", "Cross", -17f, 0.05f, 0f, TriadTier.Ground);
                Wp("Cross_Centre_E", "Cross", 17f, 0.05f, 0f, TriadTier.Ground);

                Wp("Warehouse_Hall", "Warehouse", -33.5f, 0.05f, -2.5f, TriadTier.Ground);
                Wp("Warehouse_Office", "Warehouse", -37f, 0.05f, -11.2f, TriadTier.Ground);
                Wp("Warehouse_Break", "Warehouse", -37f, 0.05f, 11.2f, TriadTier.Ground);
                Wp("Warehouse_Mezzanine", "Warehouse", -30f, 4f, 0f, TriadTier.Mid);
                Wp("Warehouse_BarS", "Warehouse", -36f, 4f, -11.5f, TriadTier.Mid);
                Wp("Warehouse_Roof", "Warehouse", -32f, 8f, 0f, TriadTier.High);

                Wp("Office_Lobby", "Office", 3f, 0.05f, 1.5f, TriadTier.Ground);
                Wp("Office_CorridorS", "Office", 0f, 0.05f, -8.5f, TriadTier.Ground);
                Wp("Office_CorridorN", "Office", 0f, 0.05f, 8.5f, TriadTier.Ground);
                Wp("Office_RoomSW", "Office", -3.5f, 0.05f, -11.2f, TriadTier.Ground);
                Wp("Office_RoomSE", "Office", 3.5f, 0.05f, -11.2f, TriadTier.Ground);
                Wp("Office_RoomNE", "Office", 3.5f, 0.05f, 5.6f, TriadTier.Ground);
                Wp("Office_StairHall", "Office", -2.6f, 0.05f, 6f, TriadTier.Ground);
                Wp("Office_Lobby2", "Office", 3f, 4f, 1.5f, TriadTier.Mid);
                Wp("Office_Corridor2S", "Office", 0f, 4f, -8.5f, TriadTier.Mid);
                Wp("Office_RoomSE2", "Office", 3.5f, 4f, -11.2f, TriadTier.Mid);
                Wp("Office_RoofStairHall", "Office", 2.6f, 4f, 6f, TriadTier.Mid);
                Wp("Office_Roof", "Office", 0f, 8f, 0f, TriadTier.High);

                Wp("Depot_Bay", "Depot", 34f, 0.05f, -9.5f, TriadTier.Ground);
                Wp("Depot_Hall", "Depot", 34f, 0.05f, 3.2f, TriadTier.Ground);
                Wp("Depot_RoomCentre", "Depot", 34f, 0.05f, 10f, TriadTier.Ground);
                Wp("Depot_RoomWest", "Depot", 30.35f, 0.05f, 10.5f, TriadTier.Ground);
                Wp("Depot_Upper_S", "Depot", 36f, 4f, -10.5f, TriadTier.Mid);
                Wp("Depot_Upper_N", "Depot", 30.5f, 4f, 10f, TriadTier.Mid);
                Wp("Depot_Upper_NE", "Depot", 37.6f, 4f, 11f, TriadTier.Mid);
                Wp("Depot_Roof", "Depot", 34f, 8f, 6f, TriadTier.High);
            }

            private void Areas()
            {
                for (int i = 0; i < 3; i++)
                {
                    float c = LaneCenters[i];
                    const float half = 10f;
                    Data.Areas.Add(new AreaSpec
                    {
                        Name = "Lane" + LaneNames[i],
                        Kind = "Lane",
                        Bounds = BoundsFromMinMax(new Vector3(c - half, 0f, -46f), new Vector3(c + half, 9f, 46f))
                    });
                    Data.Areas.Add(new AreaSpec
                    {
                        Name = BuildingNames[i],
                        Kind = "Building",
                        Bounds = BoundsFromMinMax(new Vector3(c - 6f, 0f, -14f), new Vector3(c + 6f, 9f, 14f))
                    });
                }

                Data.Areas.Add(new AreaSpec { Name = "YardSouth", Kind = "Yard", Bounds = BoundsFromMinMax(new Vector3(-44f, 0f, -62f), new Vector3(44f, 5f, -47f)) });
                Data.Areas.Add(new AreaSpec { Name = "YardNorth", Kind = "Yard", Bounds = BoundsFromMinMax(new Vector3(-44f, 0f, 47f), new Vector3(44f, 5f, 62f)) });
                Data.Areas.Add(new AreaSpec { Name = "BlockWest", Kind = "Block", Bounds = BoundsFromMinMax(new Vector3(-24f, 0f, -46f), new Vector3(-10f, 5f, 46f)) });
                Data.Areas.Add(new AreaSpec { Name = "BlockEast", Kind = "Block", Bounds = BoundsFromMinMax(new Vector3(10f, 0f, -46f), new Vector3(24f, 5f, 46f)) });
            }

            private static Bounds BoundsFromMinMax(Vector3 min, Vector3 max)
            {
                var b = new Bounds();
                b.SetMinMax(min, max);
                return b;
            }

            private void Routes()
            {
                AddRoute("LaneWest", new Vector3(-26f, 0.05f, 0f),
                    new Vector3(-34f, 0.05f, -38f), new Vector3(-26f, 0.05f, -14f), new Vector3(-26f, 0.05f, 14f), new Vector3(-34f, 0.05f, 38f));
                AddRoute("LaneMiddle", new Vector3(-8f, 0.05f, 0f),
                    new Vector3(0f, 0.05f, -38f), new Vector3(-8f, 0.05f, -12f), new Vector3(-8f, 0.05f, 12f), new Vector3(0f, 0.05f, 38f));
                AddRoute("LaneEast", new Vector3(26f, 0.05f, 0f),
                    new Vector3(34f, 0.05f, -38f), new Vector3(26f, 0.05f, -14f), new Vector3(26f, 0.05f, 14f), new Vector3(34f, 0.05f, 38f));
                AddRoute("Warehouse", new Vector3(-34f, 0.05f, -20f),
                    WaypointPosition("Warehouse_Hall"), WaypointPosition("Warehouse_Mezzanine"), WaypointPosition("Warehouse_Roof"),
                    WaypointPosition("Warehouse_Break"), WaypointPosition("Warehouse_BarS"));
                AddRoute("Office", new Vector3(-8f, 0.05f, -20f),
                    WaypointPosition("Office_Lobby"), WaypointPosition("Office_RoomSW"), WaypointPosition("Office_Lobby2"),
                    WaypointPosition("Office_Roof"), WaypointPosition("Office_StairHall"));
                AddRoute("Depot", new Vector3(34f, 0.05f, -20f),
                    WaypointPosition("Depot_Bay"), WaypointPosition("Depot_Hall"), WaypointPosition("Depot_Upper_S"),
                    WaypointPosition("Depot_Roof"), WaypointPosition("Depot_RoomCentre"));
            }

            private Vector3 WaypointPosition(string name)
            {
                foreach (WaypointSpec w in Data.Waypoints)
                {
                    if (w.Name == name)
                        return w.Position;
                }

                throw new KeyNotFoundException("Triad waypoint not found: " + name);
            }

            private void AddRoute(string name, Vector3 start, params Vector3[] points)
            {
                var route = new RouteSpec { Name = name, Start = start };
                route.Points.AddRange(points);
                Data.Routes.Add(route);
            }

            #endregion
        }
    }
}
