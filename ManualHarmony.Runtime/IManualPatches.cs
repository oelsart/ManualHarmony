using HarmonyLib;

namespace ManualHarmony.Runtime;

public interface IManualPatches
{
  IReadOnlyList<string> PatchCategories { get; }

  void PatchAll(Harmony harmony, ManualPatchOptions? options = null);

  void UnpatchAll(Harmony harmony, ManualPatchOptions? options = null);

  void PatchCategory(Harmony harmony, string category, ManualPatchOptions? options = null);

  void UnpatchCategory(Harmony harmony, string category, ManualPatchOptions? options = null);

  void PatchUncategorized(Harmony harmony, ManualPatchOptions? options = null);

  void UnpatchUncategorized(Harmony harmony, ManualPatchOptions? options = null);
}
