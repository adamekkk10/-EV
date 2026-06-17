namespace PlusEV.Core.Math;

/// <summary>Registry of the built-in <see cref="IDevigMethod"/> implementations.</summary>
public static class DevigMethods
{
    public static IDevigMethod Multiplicative { get; } = new MultiplicativeDevig();
    public static IDevigMethod Power { get; } = new PowerDevig();
    public static IDevigMethod Shin { get; } = new ShinDevig();

    public static IReadOnlyList<IDevigMethod> All { get; } = new[] { Multiplicative, Power, Shin };

    public static IDevigMethod ByKey(string key) =>
        All.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unknown devig method '{key}'.", nameof(key));
}
