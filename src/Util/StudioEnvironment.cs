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
}
