using JetBrains.Annotations;

namespace ManualHarmony.Runtime;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class HarmonyMethodOptions
{
  public int? Priority { get; init; }

  public string[]? Before { get; init; }

  public string[]? After { get; init; }

  public bool? Debug { get; init; }
}