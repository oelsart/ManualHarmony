using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace ManualHarmony.Runtime;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public readonly struct ManualPatchContext(Type patchType, MethodBase? original, MethodInfo patchMethod,
  HarmonyPatchType patchKind, string category)
{
  public Type PatchType { get; } = patchType;

  public MethodBase? Original { get; } = original;

  public MethodInfo PatchMethod { get; } = patchMethod;

  public HarmonyPatchType PatchKind { get; } = patchKind;

  public string Category { get; } = category;
}