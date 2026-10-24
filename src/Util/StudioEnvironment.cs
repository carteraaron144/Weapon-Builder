using Godot;

namespace WeaponBuilder;

/// A plain grey studio backdrop with soft ambient light.
public static class StudioEnvironment
{
    public static WorldEnvironment Make() => new()
    {
        Environment = new Environment
        {
            BackgroundMode = Environment.BGMode.Color,
            BackgroundColor = new Color(0.16f, 0.16f, 0.18f),
            AmbientLightSource = Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.55f, 0.6f),
            AmbientLightEnergy = 0.6f,
        },
    };

    /// A soft, evenly lit sky (light below as well as above) for metals to reflect, so they read as metal from
    /// any angle instead of mirroring a dark ground.
    public static Sky ReflectionSky() => new()
    {
        SkyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.62f, 0.66f, 0.72f),
            SkyHorizonColor = new Color(0.85f, 0.85f, 0.86f),
            GroundHorizonColor = new Color(0.8f, 0.79f, 0.78f),
            GroundBottomColor = new Color(0.5f, 0.49f, 0.48f),
        },
    };
}
