# Development rules

- Do not replace missing standard APIs or compiler features (including `CompilerServices` types) with in-repository dummy, stub, or hand-written polyfill implementations. Add an appropriate NuGet package reference instead.
- Keep method arguments and parameters on the same line while the line stays within 121 columns. When wrapping is necessary, group multiple arguments per line instead of putting every argument on its own line.
- Avoid naming members of nested or local types after members of their containing type when that would hide the containing-type member; use a distinct, descriptive name instead.
- Treat the null-forgiving operator (`!`) as a last resort. Prefer flow analysis, explicit null checks, and nullable-aware APIs; use `!` only when non-nullability is guaranteed but the compiler cannot determine it.
- When adding the requested ManualHarmony edge-case tests, cover HarmonyPatch argument variations (`Ref`, `Out`, and `Pointer`) and confirm the generated target signature applies the corresponding by-ref or pointer type.
- When adding the requested ManualHarmony edge-case tests, cover a patch class whose Prefix and Transpiler have separate method-level HarmonyPatch argument-type arrays and must patch different overloads.
- When adding the requested ManualHarmony edge-case tests, cover Snapshot ReversePatch targets with no Harmony patch info and ensure they behave like Original reverse patches rather than throwing.
