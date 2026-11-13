using JetBrains.Annotations;

namespace ManualHarmony.Runtime;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public struct ManualPatchOptions
{
	public static ManualPatchOptions Default { get; } = new() { EnableTryCatch = true };

  public bool EnableTryCatch { get; init; }

  public Action<Exception>? Logger { get; init; }

  public Func<ManualPatchContext, bool>? ShouldPatch { get; init; }

  public Func<ManualPatchContext, bool>? ShouldUnpatch { get; init; }
}