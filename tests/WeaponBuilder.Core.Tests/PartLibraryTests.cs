using System.Numerics;
using WeaponBuilder.Core;

namespace WeaponBuilder.Core.Tests;

public class PartLibraryTests
{
    static string PartJson(string id, string length = "0.25", string sockets = "[]") => $$"""
        { "id": "{{id}}", "name": "N", "category": "Handles", "tags": ["handle"],
          "length": {{length}}, "width": 0.04, "defaultMaterial": "leather",
          "sockets": {{sockets}},
          "mesh": [ { "shape": "prism", "size": [0.02, 0.25, 0.02], "position": [0, 0.125, 0], "sides": 6 } ] }
        """;

    static PartLibrary Load(out List<string> errors, params (string, string)[] files) => PartLibrary.Load(files, out errors);

    [Fact]
    public void Load_SkipsUnderscoreFiles()
    {
        var lib = Load(out var errors, ("_template.json", PartJson("tpl")), ("grip.json", PartJson("grip")));
        Assert.Empty(errors);
        Assert.Equal("grip", Assert.Single(lib.All).Id);
    }

    [Fact]
    public void Load_DuplicateId_Error()
    {
        var lib = Load(out var errors, ("a.json", PartJson("grip")), ("b.json", PartJson("grip")));
        Assert.Contains("b.json", Assert.Single(errors));
        Assert.Single(lib.All);
    }

    [Fact]
    public void Load_NonPositiveLength_Error()
    {
        var lib = Load(out var errors, ("a.json", PartJson("grip", length: "0")));
        Assert.Single(errors);
        Assert.Empty(lib.All);
    }

    [Fact]
    public void Load_SocketWithNoAccepts_Error()
    {
        var lib = Load(out var errors, ("a.json", PartJson("grip",
            sockets: """[ { "name": "top", "position": [0,0,0], "accepts": [], "length": 1, "width": 0.1 } ]""")));
        Assert.Single(errors);
        Assert.Empty(lib.All);
    }

    [Fact]
    public void Load_BadJson_Error()
    {
        var lib = Load(out var errors, ("a.json", "{ not json"));
        Assert.Contains("a.json", Assert.Single(errors));
        Assert.Empty(lib.All);
    }

    [Fact]
    public void Load_ParsesSocketVectorsAndDefaults()
    {
        var lib = Load(out var errors, ("a.json", PartJson("grip",
            sockets: """[ { "name": "top", "position": [0, 0.25, 0], "accepts": ["blade", "head"], "length": 0.9, "width": 0.07, "rotationStep": 90 } ]""")));
        Assert.Empty(errors);
        var grip = lib.Get("grip");
        Assert.Equal("leather", grip.DefaultMaterial);
        var top = Assert.Single(grip.Sockets);
        Assert.Equal(new Vector3(0, 0.25f, 0), top.Position);
        Assert.Equal(Vector3.Zero, top.RotationDegrees);
        Assert.Equal(["blade", "head"], top.Accepts);
        Assert.Equal(90f, top.RotationStepDegrees);
        Assert.False(top.Required);
        var prim = Assert.Single(grip.Mesh);
        Assert.Equal("prism", prim.Shape);
        Assert.Equal(Vector3.One, prim.Scale);
        Assert.Equal(6, prim.Sides);
    }
}
