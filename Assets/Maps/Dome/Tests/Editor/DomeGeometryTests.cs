using System.Collections.Generic;
using FpsMaps.Dome;
using NUnit.Framework;
using UnityEngine;

public class DomeGeometryTests
{
    private static List<DomePanel> BuildDomePanels()
    {
        return DomeGeometry.BuildPanels(
            DomeLayout.GeodesicFrequency,
            DomeLayout.DomeRadius,
            DomeLayout.DomeHeightScale,
            0.6f,
            DomeLayout.DoorAzimuthsDeg,
            DomeLayout.DoorClearHeight,
            DomeLayout.DoorHalfAngleDeg);
    }

    private static void AssertValidMesh(Mesh mesh, string label)
    {
        Assert.IsNotNull(mesh, label + " should exist");
        Assert.Greater(mesh.vertexCount, 0, label + " should have vertices");
        int[] tris = mesh.triangles;
        Assert.Greater(tris.Length, 0, label + " should have triangles");
        Assert.AreEqual(0, tris.Length % 3, label + " index count should be a multiple of 3");
        foreach (int index in tris)
        {
            Assert.GreaterOrEqual(index, 0);
            Assert.Less(index, mesh.vertexCount);
        }
    }

    [Test]
    public void Panels_FormHexLatticeWithFewPentagons()
    {
        List<DomePanel> panels = BuildDomePanels();
        Assert.Greater(panels.Count, 40, "Dome should be built from dozens of panels");

        int hexes = 0;
        foreach (DomePanel panel in panels)
        {
            Assert.IsTrue(panel.ring.Length == 5 || panel.ring.Length == 6, "Panels are hexagons or pentagons");
            Assert.AreEqual(panel.ring.Length, panel.ringIds.Length);
            if (panel.ring.Length == 6)
                hexes++;
        }

        Assert.Greater(hexes, panels.Count / 2, "Most panels should be hexagons");
    }

    [Test]
    public void Panels_SitOnScaledSphereAboveFloor()
    {
        foreach (DomePanel panel in BuildDomePanels())
        {
            foreach (Vector3 v in panel.ring)
            {
                Assert.GreaterOrEqual(v.y, -0.01f);
                Assert.LessOrEqual(v.y, DomeLayout.DomeRadius * DomeLayout.DomeHeightScale + 0.01f);
                float horizontal = new Vector2(v.x, v.z).magnitude;
                Assert.LessOrEqual(horizontal, DomeLayout.DomeRadius + 0.01f);
            }
        }
    }

    [Test]
    public void Doorways_AreFreeOfPanels()
    {
        foreach (DomePanel panel in BuildDomePanels())
        {
            foreach (float door in DomeLayout.DoorAzimuthsDeg)
            {
                float azimuth = DomeLayout.AzimuthOf(panel.center);
                float delta = Mathf.Abs(Mathf.DeltaAngle(azimuth, door));
                bool inDoorway = delta < DomeLayout.DoorHalfAngleDeg * 0.5f && panel.center.y < DomeLayout.DoorClearHeight;
                Assert.IsFalse(inDoorway, "A panel blocks the doorway at azimuth " + door);
            }
        }
    }

    [Test]
    public void Doors_RemovePanels()
    {
        int withDoors = BuildDomePanels().Count;
        int withoutDoors = DomeGeometry.BuildPanels(
            DomeLayout.GeodesicFrequency,
            DomeLayout.DomeRadius,
            DomeLayout.DomeHeightScale,
            0.6f,
            new float[0],
            DomeLayout.DoorClearHeight,
            DomeLayout.DoorHalfAngleDeg).Count;
        Assert.Less(withDoors, withoutDoors, "Door cut-outs should remove panels");
    }

    [Test]
    public void SharedEdges_AreDeduplicated()
    {
        List<DomePanel> panels = BuildDomePanels();
        int rawEdges = 0;
        foreach (DomePanel panel in panels)
            rawEdges += panel.ring.Length;

        int unique = DomeGeometry.CountUniqueEdges(panels);
        Assert.Greater(unique, panels.Count);
        Assert.Less(unique, rawEdges, "Neighbouring panels should share struts");
    }

    [Test]
    public void GeneratedMeshes_AreValid()
    {
        List<DomePanel> panels = BuildDomePanels();
        Mesh glass = DomeGeometry.BuildGlassMesh(panels, 0.94f);
        Mesh struts = DomeGeometry.BuildStrutMesh(panels, 0.22f);
        Mesh shell = DomeGeometry.BuildShellColliderMesh(panels);
        Mesh prism = DomeGeometry.BuildPrism(6, 0f);
        try
        {
            AssertValidMesh(glass, "Glass mesh");
            AssertValidMesh(struts, "Strut mesh");
            AssertValidMesh(shell, "Shell collider mesh");
            AssertValidMesh(prism, "Hex prism mesh");
            Assert.AreEqual(0f, prism.bounds.min.y, 0.001f, "Prism base should sit on y = 0");
            Assert.AreEqual(1f, prism.bounds.max.y, 0.001f, "Prism should be one unit tall");
        }
        finally
        {
            Object.DestroyImmediate(glass);
            Object.DestroyImmediate(struts);
            Object.DestroyImmediate(shell);
            Object.DestroyImmediate(prism);
        }
    }

    [Test]
    public void Layout_DoorAzimuthsAreCardinal()
    {
        Assert.AreEqual(4, DomeLayout.DoorAzimuthsDeg.Length);
        Assert.AreEqual(0f, Vector3.Distance(DomeLayout.Direction(0f), Vector3.forward), 0.001f);
        Assert.AreEqual(0f, Vector3.Distance(DomeLayout.Direction(90f), Vector3.right), 0.001f);
    }
}
