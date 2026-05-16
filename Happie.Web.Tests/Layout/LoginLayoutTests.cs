using Bunit;
using Happie.Web.Layout;
using Microsoft.AspNetCore.Components;

namespace Happie.Web.Tests.Layout;

public class LoginLayoutTests : BunitContext
{
    [Fact]
    public void Render_WithBody_HasNoSidebar()
    {
        // Arrange.
        var body = (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Test content</p>"));

        // Act.
        var cut = Render<LoginLayout>(parameters =>
            parameters.Add(p => p.Body, body));

        // Assert.
        Assert.Empty(cut.FindAll(".sidebar"));
        Assert.Empty(cut.FindAll("nav"));
    }

    [Fact]
    public void Render_WithBody_HasNoTopBar()
    {
        // Arrange.
        var body = (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Test content</p>"));

        // Act.
        var cut = Render<LoginLayout>(parameters =>
            parameters.Add(p => p.Body, body));

        // Assert.
        Assert.Empty(cut.FindAll(".top-bar"));
        Assert.Empty(cut.FindAll(".navbar"));
    }

    [Fact]
    public void Render_WithBody_RootElementHasLoginPageClass()
    {
        // Arrange.
        var body = (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Test content</p>"));

        // Act.
        var cut = Render<LoginLayout>(parameters =>
            parameters.Add(p => p.Body, body));

        // Assert.
        var rootElement = cut.Find("div");
        Assert.Contains("login-page", rootElement.ClassList);
    }
}
