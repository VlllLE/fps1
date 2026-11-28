using System.Collections.Generic;
using FpsTactical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

public class TacticalRoutePlannerTests
{
    private static readonly Vector3 Start = new Vector3(0f, 0f, 0f);
    private static readonly Vector3 Goal = new Vector3(0f, 0f, 60f);

    private static float Straight(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

    [Test]
    public void DistanceToSegment_MeasuresAlongTheSegmentAndPastItsEnds()
    {
        Assert.AreEqual(5f, TacticalRoutePlanner.DistanceToSegment(new Vector3(5f, 0f, 30f), Start, Goal), 0.001f);
        Assert.AreEqual(10f, TacticalRoutePlanner.DistanceToSegment(new Vector3(0f, 0f, 70f), Start, Goal), 0.001f);
        Assert.AreEqual(5f, TacticalRoutePlanner.DistanceToSegment(new Vector3(0f, 0f, -5f), Start, Goal), 0.001f);
        Assert.AreEqual(3f, TacticalRoutePlanner.DistanceToSegment(new Vector3(0f, 9f, 3f), Start, Start), 0.001f);
    }

    [Test]
    public void Side_SeparatesLeftFromRight()
    {
        Assert.AreEqual(1, TacticalRoutePlanner.Side(new Vector3(-10f, 0f, 30f), Start, Goal));
        Assert.AreEqual(-1, TacticalRoutePlanner.Side(new Vector3(10f, 0f, 30f), Start, Goal));
        Assert.AreEqual(0, TacticalRoutePlanner.Side(new Vector3(0f, 0f, 30f), Start, Goal));
    }

    [Test]
    public void PathLength_SumsTheCorners()
    {
        var corners = new List<Vector3> { Vector3.zero, new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 10f) };
        Assert.AreEqual(20f, TacticalRoutePlanner.PathLength(corners), 0.001f);
        Assert.AreEqual(0f, TacticalRoutePlanner.PathLength(new List<Vector3>()), 0.001f);
    }

    private static List<Vector3> Candidates()
    {
        return new List<Vector3>
        {
            new Vector3(20f, 0f, 30f),
            new Vector3(2f, 0f, 30f),
            new Vector3(0f, 0f, 3f),
            new Vector3(-20f, 0f, 30f),
            new Vector3(60f, 0f, 30f)
        };
    }

    [Test]
    public void PickFlank_SkipsPointsOnTheRouteNearTheEndsOrTooFarOff()
    {
        int pick = TacticalRoutePlanner.PickFlank(Start, Goal, Candidates(), Straight, 0, null);

        Assert.That(pick, Is.EqualTo(0).Or.EqualTo(3), "only the two side points qualify");
    }

    [Test]
    public void PickFlank_HonoursThePreferredSide()
    {
        Assert.AreEqual(3, TacticalRoutePlanner.PickFlank(Start, Goal, Candidates(), Straight, 1, null));
        Assert.AreEqual(0, TacticalRoutePlanner.PickFlank(Start, Goal, Candidates(), Straight, -1, null));
    }

    [Test]
    public void PickFlank_FallsBackToTheOtherSideWhenThePreferredHasNone()
    {
        var onlyRight = new List<Vector3> { new Vector3(20f, 0f, 30f) };

        Assert.AreEqual(0, TacticalRoutePlanner.PickFlank(Start, Goal, onlyRight, Straight, 1, null));
    }

    [Test]
    public void PickFlank_RejectsExpensiveDetours()
    {
        var far = new List<Vector3> { new Vector3(60f, 0f, 30f) };
        Assert.AreEqual(-1, TacticalRoutePlanner.PickFlank(Start, Goal, far, Straight, 0, null));
        Assert.AreEqual(0, TacticalRoutePlanner.PickFlank(Start, Goal, far, Straight, 0, null, 8f, 3f));
    }

    [Test]
    public void PickFlank_UsesTheSuppliedPathLengths()
    {
        var side = new List<Vector3> { new Vector3(20f, 0f, 30f) };
        float Walled(Vector3 a, Vector3 b)
        {
            bool direct = Mathf.Abs(a.x) < 1f && Mathf.Abs(b.x) < 1f;
            return Vector3.Distance(a, b) * (direct ? 3f : 1f);
        }

        Assert.AreEqual(0, TacticalRoutePlanner.PickFlank(Start, Goal, side, Walled, 0, null, 8f, 1.2f),
            "a blocked direct route makes the detour cheap");
    }

    [Test]
    public void PickFlank_HandlesNoCandidatesAndUnreachableGoals()
    {
        Assert.AreEqual(-1, TacticalRoutePlanner.PickFlank(Start, Goal, new List<Vector3>(), Straight, 0, null));
        Assert.AreEqual(-1, TacticalRoutePlanner.PickFlank(Start, Goal, null, Straight, 0, null));
        Assert.AreEqual(-1, TacticalRoutePlanner.PickFlank(Start, Goal, Candidates(), (a, b) => float.MaxValue, 0, null));
        Assert.AreEqual(-1, TacticalRoutePlanner.PickFlank(Start, Goal, Candidates(), null, 0, null));
    }

    [Test]
    public void PickFlank_WithARandomSource_StaysInsideTheQualifiedPool()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            int pick = TacticalRoutePlanner.PickFlank(Start, Goal, Candidates(), Straight, 0, new System.Random(seed));
            Assert.That(pick, Is.EqualTo(0).Or.EqualTo(3));
        }
    }

    [Test]
    public void StagingPoint_WalksBackAlongThePath()
    {
        var corners = new List<Vector3> { Vector3.zero, new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 10f) };

        Assert.That(TacticalRoutePlanner.StagingPoint(corners, 5f), Is.EqualTo(new Vector3(5f, 0f, 10f)).Using(Vector3EqualityComparer.Instance));
        Assert.That(TacticalRoutePlanner.StagingPoint(corners, 15f), Is.EqualTo(new Vector3(0f, 0f, 5f)).Using(Vector3EqualityComparer.Instance));
        Assert.That(TacticalRoutePlanner.StagingPoint(corners, 100f), Is.EqualTo(Vector3.zero).Using(Vector3EqualityComparer.Instance));
        Assert.That(TacticalRoutePlanner.StagingPoint(corners, 0f), Is.EqualTo(new Vector3(10f, 0f, 10f)).Using(Vector3EqualityComparer.Instance));
    }

    [Test]
    public void StagingPoint_HandlesEmptyAndSinglePointPaths()
    {
        Assert.AreEqual(Vector3.zero, TacticalRoutePlanner.StagingPoint(new List<Vector3>(), 5f));
        Assert.AreEqual(new Vector3(1f, 2f, 3f), TacticalRoutePlanner.StagingPoint(new List<Vector3> { new Vector3(1f, 2f, 3f) }, 5f));
    }
}
