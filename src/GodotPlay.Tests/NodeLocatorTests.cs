using GodotPlay;
using GodotPlay.Protocol;

namespace GodotPlay.Tests;

[TestFixture]
public class NodeLocatorTests
{
    [Test]
    public void Locator_ByPath_BuildsCorrectQuery()
    {
        var locator = new NodeLocator(path: "/root/UI/StartButton");
        var query = locator.ToQuery();
        Assert.That(query.Path, Is.EqualTo("/root/UI/StartButton"));
    }

    [Test]
    public void Locator_ByClassName_BuildsCorrectQuery()
    {
        var locator = new NodeLocator(className: "Button");
        var query = locator.ToQuery();
        Assert.That(query.ClassName, Is.EqualTo("Button"));
    }

    [Test]
    public void Locator_ByNamePattern_BuildsCorrectQuery()
    {
        var locator = new NodeLocator(namePattern: "Start*");
        var query = locator.ToQuery();
        Assert.That(query.NamePattern, Is.EqualTo("Start*"));
    }

    [Test]
    public void Locator_Combined_BuildsCorrectQuery()
    {
        var locator = new NodeLocator(className: "Button", namePattern: "Start*");
        var query = locator.ToQuery();
        Assert.That(query.ClassName, Is.EqualTo("Button"));
        Assert.That(query.NamePattern, Is.EqualTo("Start*"));
    }

    [Test]
    public void Locator_Chain_CreatesChildLocator()
    {
        var parent = new NodeLocator(className: "VBoxContainer");
        var child = parent.Locator(className: "Button");
        Assert.That(child.Parent, Is.SameAs(parent));
        Assert.That(child.ToQuery().ClassName, Is.EqualTo("Button"));
    }
}
