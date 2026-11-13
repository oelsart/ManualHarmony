using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ManualHarmony.Runtime;
using NUnit.Framework;
using ManualHarmony.TestGenerated;

namespace ManualHarmony.Tests;

[TestFixture]
[NonParallelizable]
public sealed class ManualHarmonyTests
{
  private const string HarmonyId = "ManualHarmony.Test";

  private readonly Harmony harmony = new(HarmonyId);
  private readonly IManualPatches patchApi = new GeneratedPatchApi();

  [SetUp]
  public void SetUp()
  {
    harmony.UnpatchAll(HarmonyId);
    BasicPatch.FinalizerCalls = 0;
    OverloadPatch.PrefixCalls = 0;
    OverloadPatch.TranspilerCalls = 0;
    LazyTargetPatch.ResolutionCount = 0;
    MultiTargetPatch.ResolutionCount = 0;
    PrepareFalsePatch.ResolutionCount = 0;
    InnerPatchKinds.PrefixCalls = 0;
    InnerPatchKinds.PostfixCalls = 0;
    InnerPatchKinds.FinalizerCalls = 0;
  }

  [TearDown]
  public void TearDown() => harmony.UnpatchAll(HarmonyId);

  [Test]
  public void ExposesCategoriesThroughInterface()
  {
    Assert.That(patchApi.PatchCategories, Is.EquivalentTo([
	    "basic",
      "overloads",
      "argument-variations",
      "lazy-target",
      "multiple-targets",
      "prepare-false",
      "snapshot-without-patches",
      "snapshot-with-patches",
      "original-reverse-patch",
      "inner-patches",
      "missing-target"
    ]));
  }

  [Test]
  public void AppliesPrefixPostfixFinalizerAndUnpatchesByCategory()
  {
    patchApi.PatchCategory(harmony, "basic", new ManualPatchOptions { EnableTryCatch = false });

    Assert.That(PatchTargets.Basic(4), Is.EqualTo(15));
    Assert.That(BasicPatch.FinalizerCalls, Is.EqualTo(1));
    Assert.That(Harmony.GetPatchInfo(AccessTools.Method(typeof(PatchTargets), nameof(PatchTargets.Basic)))?.Prefixes,
      Has.Count.EqualTo(1));

    patchApi.UnpatchCategory(harmony, "basic");

    Assert.That(PatchTargets.Basic(4), Is.EqualTo(8));
    Assert.That(Harmony.GetPatchInfo(AccessTools.Method(typeof(PatchTargets), nameof(PatchTargets.Basic)))?.Prefixes,
      Is.Null.Or.Empty);
  }

  [Test]
  public void AppliesMethodLevelTargetsToTheirDistinctOverloads()
  {
    patchApi.PatchCategory(harmony, "overloads", new ManualPatchOptions { EnableTryCatch = false });

    var intTarget = AccessTools.Method(typeof(PatchTargets), nameof(PatchTargets.Overloaded), [typeof(int)])
                    ?? throw new InvalidOperationException("The integer overload was not found.");
    var stringTarget = AccessTools.Method(typeof(PatchTargets), nameof(PatchTargets.Overloaded), [typeof(string)])
                       ?? throw new InvalidOperationException("The string overload was not found.");
    var intPatches = Harmony.GetPatchInfo(intTarget);
    var stringPatches = Harmony.GetPatchInfo(stringTarget);

    Assert.That(intPatches?.Prefixes.Select(patch => patch.PatchMethod),
      Does.Contain(AccessTools.Method(typeof(OverloadPatch), nameof(OverloadPatch.Prefix))));
    Assert.That(intPatches?.Transpilers, Is.Null.Or.Empty);
    Assert.That(stringPatches?.Prefixes, Is.Null.Or.Empty);
    Assert.That(stringPatches?.Transpilers.Select(patch => patch.PatchMethod),
      Does.Contain(AccessTools.Method(typeof(OverloadPatch), nameof(OverloadPatch.Transpiler))));
    Assert.That(OverloadPatch.PrefixCalls, Is.Zero);
    Assert.That(intTarget.Invoke(null, [2]), Is.EqualTo(102));
    Assert.That(OverloadPatch.PrefixCalls, Is.EqualTo(1));
    Assert.That(stringTarget.Invoke(null, ["ab"]), Is.EqualTo(4));
    Assert.That(OverloadPatch.TranspilerCalls, Is.GreaterThan(0));
  }

  [Test]
  public unsafe void ResolvesRefOutAndPointerArgumentVariations()
  {
    patchApi.PatchCategory(harmony, "argument-variations",
      new ManualPatchOptions { EnableTryCatch = false });

    var refValue = 1;
    Assert.That(ArgumentTargets.RefTarget(ref refValue), Is.EqualTo(11));

    var outResult = ArgumentTargets.OutTarget(out var outValue);
    Assert.That(outResult, Is.EqualTo(3));
    Assert.That(outValue, Is.EqualTo(20));

    var pointerValue = 1;
    Assert.That(ArgumentTargets.PointerTarget(&pointerValue), Is.EqualTo(21));
  }

  [Test]
  public void ResolvesTargetMethodsOnlyAfterCategoryMatches()
  {
    patchApi.PatchCategory(harmony, "not-lazy");

    Assert.That(LazyTargetPatch.ResolutionCount, Is.Zero);
    Assert.That(PatchTargets.Lazy(), Is.EqualTo(1));

    patchApi.PatchCategory(harmony, "lazy-target",
      new ManualPatchOptions { EnableTryCatch = false });

    Assert.That(LazyTargetPatch.ResolutionCount, Is.EqualTo(1));
    Assert.That(PatchTargets.Lazy(), Is.EqualTo(11));
  }

  [Test]
  public void AppliesTargetMethodsToEachReturnedMethod()
  {
    patchApi.PatchCategory(harmony, "multiple-targets",
      new ManualPatchOptions { EnableTryCatch = false });

    Assert.That(MultiTargetPatch.ResolutionCount, Is.EqualTo(1));
    Assert.That(PatchTargets.FirstTarget(), Is.EqualTo(11));
    Assert.That(PatchTargets.SecondTarget(), Is.EqualTo(12));
  }

  [Test]
  public void PrepareFalseSkipsPatchAndTargetResolution()
  {
    patchApi.PatchCategory(harmony, "prepare-false");

    Assert.That(PrepareFalsePatch.ResolutionCount, Is.Zero);
    Assert.That(PatchTargets.PrepareFalse(), Is.EqualTo(1));
  }

  [Test]
  public void SnapshotReversePatchFallsBackToOriginalWhenNoPatchInfoExists()
  {
    patchApi.PatchCategory(harmony, "snapshot-without-patches",
      new ManualPatchOptions { EnableTryCatch = false });

    Assert.That(ReversePatchTargets.SnapshotWithoutPatches(4), Is.EqualTo(7));
  }

  [Test]
  public void SnapshotReversePatchCopiesExistingPatchesWhileOriginalDoesNot()
  {
    var original = AccessTools.Method(typeof(ReversePatchTargets), nameof(ReversePatchTargets.Patched));
    harmony.CreateProcessor(original)
      .AddTranspiler(AccessTools.Method(typeof(ExternalReversePatch), nameof(ExternalReversePatch.AddTenToConstant)))
      .Patch();

    patchApi.PatchCategory(harmony, "snapshot-with-patches",
      new ManualPatchOptions { EnableTryCatch = false });
    patchApi.PatchCategory(harmony, "original-reverse-patch",
      new ManualPatchOptions { EnableTryCatch = false });

    Assert.That(ReversePatchTargets.Patched(4), Is.EqualTo(14));
    Assert.That(ReversePatchStubs.Snapshot(4), Is.EqualTo(14));
    Assert.That(ReversePatchStubs.Original(4), Is.EqualTo(7));
  }

  [Test]
  public void SupportsInnerPatchKinds()
  {
    patchApi.PatchCategory(harmony, "inner-patches",
      new ManualPatchOptions { EnableTryCatch = false });

    var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(PatchTargets), nameof(PatchTargets.Inner)));
    Assert.That(info, Is.Not.Null);
    Assert.That(info?.InnerPrefixes, Has.Count.EqualTo(1));
    Assert.That(info?.InnerPostfixes, Has.Count.EqualTo(1));
    Assert.That(info?.InnerFinalizers, Has.Count.EqualTo(1));
    Assert.That(PatchTargets.Inner(), Is.EqualTo(1));
    Assert.That(InnerPatchKinds.PrefixCalls, Is.EqualTo(1));
    Assert.That(InnerPatchKinds.PostfixCalls, Is.EqualTo(1));
    Assert.That(InnerPatchKinds.FinalizerCalls, Is.EqualTo(1));
  }

  [Test]
  public void MissingTargetIsReportedInsteadOfSilentlySkipped()
  {
    Assert.Throws<NullReferenceException>(() => patchApi.PatchCategory(harmony, "missing-target",
      new ManualPatchOptions { EnableTryCatch = false }));
  }

  [Test]
  public void ShouldPatchAndShouldUnpatchReceiveTheTargetContext()
  {
    var patchContexts = new List<ManualPatchContext>();
    var unpatchContexts = new List<ManualPatchContext>();
    var options = new ManualPatchOptions
    {
      EnableTryCatch = false,
      ShouldPatch = context =>
      {
        patchContexts.Add(context);
        return true;
      },
      ShouldUnpatch = context =>
      {
        unpatchContexts.Add(context);
        return false;
      }
    };

    patchApi.PatchCategory(harmony, "basic", options);
    patchApi.UnpatchCategory(harmony, "basic", options);

    Assert.That(patchContexts, Has.Count.EqualTo(3));
    Assert.That(patchContexts, Has.All.Property(nameof(ManualPatchContext.Original)).Not.Null);
    Assert.That(patchContexts, Has.All.Property(nameof(ManualPatchContext.Category)).EqualTo("basic"));
    Assert.That(unpatchContexts, Has.Count.EqualTo(3));
    Assert.That(PatchTargets.Basic(4), Is.EqualTo(15));
  }
}

public static class PatchTargets
{
  public static int Basic(int value) => value * 2;

  [MethodImpl(MethodImplOptions.NoInlining)]
  public static int Overloaded(int value) => value;

  [MethodImpl(MethodImplOptions.NoInlining)]
  public static int Overloaded(string value) => value.Length * 2;

  public static int Lazy() => 1;

  public static int FirstTarget() => 1;

  public static int SecondTarget() => 2;

  public static int PrepareFalse() => 1;

  public static int Inner() => InnerCallTargets.Value();
}

[HarmonyPatch(typeof(PatchTargets), nameof(PatchTargets.Basic))]
[HarmonyPatchCategory("basic")]
public static class BasicPatch
{
  public static int FinalizerCalls;

  public static bool Prefix(ref int __result)
  {
    __result = 10;
    return false;
  }

  public static void Postfix(ref int __result) => __result += 5;

  public static Exception Finalizer(Exception __exception)
  {
    FinalizerCalls++;
    return __exception;
  }
}

[HarmonyPatch]
[HarmonyPatchCategory("overloads")]
public static class OverloadPatch
{
  public static int TranspilerCalls;
  public static int PrefixCalls;

  [HarmonyPatch(typeof(PatchTargets), nameof(PatchTargets.Overloaded), new[] { typeof(int) })]
  public static void Prefix(ref int __0)
  {
    PrefixCalls++;
    __0 += 100;
  }

  [HarmonyPatch(typeof(PatchTargets), nameof(PatchTargets.Overloaded), new[] { typeof(string) })]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    TranspilerCalls++;
    return instructions;
  }
}

public static class ArgumentTargets
{
  public static int RefTarget(ref int value)
  {
    value++;
    return value;
  }

  public static int OutTarget(out int value)
  {
    value = 3;
    return value;
  }

  public static unsafe int PointerTarget(int* value)
  {
    (*value)++;
    return *value;
  }
}

[HarmonyPatch]
[HarmonyPatchCategory("argument-variations")]
public static class RefArgumentPatch
{
  [HarmonyPatch(typeof(ArgumentTargets), nameof(ArgumentTargets.RefTarget), [typeof(int)],
	  [ArgumentType.Ref])]
  public static void Prefix(ref int __0) => __0 = 10;
}

[HarmonyPatch]
[HarmonyPatchCategory("argument-variations")]
public static class OutArgumentPatch
{
  [HarmonyPatch(typeof(ArgumentTargets), nameof(ArgumentTargets.OutTarget), [typeof(int)],
	  [ArgumentType.Out])]
  public static void Postfix(ref int __0) => __0 = 20;
}

[HarmonyPatch]
[HarmonyPatchCategory("argument-variations")]
public static class PointerArgumentPatch
{
  [HarmonyPatch(typeof(ArgumentTargets), nameof(ArgumentTargets.PointerTarget), [typeof(int)],
	  [ArgumentType.Pointer])]
  public static unsafe void Prefix(int* __0) => *__0 = 20;
}

[HarmonyPatch]
[HarmonyPatchCategory("lazy-target")]
public static class LazyTargetPatch
{
  public static int ResolutionCount;

  public static MethodBase TargetMethod()
  {
    ResolutionCount++;
    return AccessTools.Method(typeof(PatchTargets), nameof(PatchTargets.Lazy));
  }

  public static void Postfix(ref int __result) => __result += 10;
}

[HarmonyPatch]
[HarmonyPatchCategory("multiple-targets")]
public static class MultiTargetPatch
{
  public static int ResolutionCount;

  public static IEnumerable<MethodBase> TargetMethods()
  {
    ResolutionCount++;
    yield return AccessTools.Method(typeof(PatchTargets), nameof(PatchTargets.FirstTarget));
    yield return AccessTools.Method(typeof(PatchTargets), nameof(PatchTargets.SecondTarget));
  }

  public static void Postfix(ref int __result) => __result += 10;
}

[HarmonyPatch]
[HarmonyPatchCategory("prepare-false")]
public static class PrepareFalsePatch
{
  public static int ResolutionCount;

  public static bool Prepare() => false;

  public static MethodBase TargetMethod()
  {
    ResolutionCount++;
    return AccessTools.Method(typeof(PatchTargets), nameof(PatchTargets.PrepareFalse));
  }

  public static void Postfix(ref int __result) => __result += 10;
}

[HarmonyPatch(typeof(ReversePatchTargets), nameof(ReversePatchTargets.SnapshotWithoutPatches))]
[HarmonyPatchCategory("snapshot-without-patches")]
public static class SnapshotWithoutPatchesReversePatch
{
  [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
  public static int Standin(int value) => throw new NotImplementedException();
}

[HarmonyPatch(typeof(ReversePatchTargets), nameof(ReversePatchTargets.Patched))]
[HarmonyPatchCategory("snapshot-with-patches")]
public static class SnapshotWithPatchesReversePatch
{
  [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
  public static int Standin(int value) => throw new NotImplementedException();
}

[HarmonyPatch(typeof(ReversePatchTargets), nameof(ReversePatchTargets.Patched))]
[HarmonyPatchCategory("original-reverse-patch")]
public static class OriginalReversePatch
{
  [HarmonyReversePatch]
  public static int Standin(int value) => throw new NotImplementedException();
}

public static class ReversePatchTargets
{
  public static int SnapshotWithoutPatches(int value) => value + 3;

  public static int Patched(int value) => value + 3;
}

public static class ReversePatchStubs
{
  public static int Snapshot(int value) => SnapshotWithPatchesReversePatch.Standin(value);

  public static int Original(int value) => OriginalReversePatch.Standin(value);
}

public static class ExternalReversePatch
{
  public static IEnumerable<CodeInstruction> AddTenToConstant(IEnumerable<CodeInstruction> instructions)
  {
    foreach (var instruction in instructions)
    {
      if (instruction.opcode == System.Reflection.Emit.OpCodes.Ldc_I4_3)
      {
        instruction.opcode = System.Reflection.Emit.OpCodes.Ldc_I4_S;
        instruction.operand = (sbyte)10;
      }

      yield return instruction;
    }
  }
}

[HarmonyPatch(typeof(PatchTargets), nameof(PatchTargets.Inner))]
[HarmonyPatchCategory("inner-patches")]
public static class InnerPatchKinds
{
  public static int PrefixCalls;
  public static int PostfixCalls;
  public static int FinalizerCalls;

  [HarmonyInfix(typeof(InnerCallTargets), nameof(InnerCallTargets.Value))]
  public static void InnerPrefix()
  {
    PrefixCalls++;
  }

  [HarmonyInfix(typeof(InnerCallTargets), nameof(InnerCallTargets.Value))]
  public static void InnerPostfix()
  {
    PostfixCalls++;
  }

  [HarmonyInfix(typeof(InnerCallTargets), nameof(InnerCallTargets.Value))]
  public static void InnerFinalizer()
  {
    FinalizerCalls++;
  }
}

[HarmonyPatch]
public static class InnerCallTargets
{
  public static int Value() => 1;
}

[HarmonyPatch(typeof(PatchTargets), "MissingMethod")]
[HarmonyPatchCategory("missing-target")]
public static class MissingTargetPatch
{
  public static void Prefix()
  {
  }
}
