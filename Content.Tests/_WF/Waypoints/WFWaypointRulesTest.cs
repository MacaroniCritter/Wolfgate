using System.Numerics;
using Content.Shared._WF.Waypoints;
using NUnit.Framework;

namespace Content.Tests._WF.Waypoints;

[TestFixture]
public sealed class WFWaypointRulesTest
{
    [TestCase("12.5", "-300", 12.5f, -300f)]
    [TestCase(" 7 ", "0", 7f, 0f)]
    [TestCase("1e3", "2", 1000f, 2f)]
    public void ReadsTypedCoordinates(string x, string y, float expectedX, float expectedY)
    {
        Assert.That(WFWaypointRules.TryParsePosition(x, y, out var position), Is.True);
        Assert.That(position, Is.EqualTo(new Vector2(expectedX, expectedY)));
    }

    [TestCase("", "1")]
    [TestCase("1", "")]
    [TestCase("abc", "1")]
    [TestCase("1,5", "1")]
    [TestCase("NaN", "1")]
    [TestCase("1", "Infinity")]
    [TestCase("1000001", "1")]
    [TestCase("1", "-1000001")]
    public void RefusesBadCoordinates(string x, string y)
    {
        Assert.That(WFWaypointRules.TryParsePosition(x, y, out _), Is.False);
    }

    [Test]
    public void RefusesPositionsThatSkipTheForm()
    {
        Assert.That(WFWaypointRules.IsValidPosition(new Vector2(float.NaN, 0)), Is.False);
        Assert.That(WFWaypointRules.IsValidPosition(new Vector2(0, float.PositiveInfinity)), Is.False);
        Assert.That(WFWaypointRules.IsValidPosition(new Vector2(WFWaypointRules.MaxCoordinate, -WFWaypointRules.MaxCoordinate)), Is.True);
    }

    [Test]
    public void CleansNames()
    {
        Assert.That(WFWaypointRules.CleanName("  Rally\nPoint\t "), Is.EqualTo("RallyPoint"));
        Assert.That(WFWaypointRules.CleanName("   "), Is.Empty);
        Assert.That(WFWaypointRules.CleanName(new string('x', 100)), Has.Length.EqualTo(WFWaypointRules.MaxNameLength));
        Assert.That(WFWaypointRules.CleanName(new string('x', WFWaypointRules.MaxNameLength - 1) + "\U0001F680"),
            Has.Length.EqualTo(WFWaypointRules.MaxNameLength - 1), "A cut must not split a surrogate pair.");
    }
}
