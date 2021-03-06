using System.Reflection;
using System.Reflection.Emit;
using AtraBase.Toolkit.Reflection;
using AtraShared.Utils.Extensions;
using AtraShared.Utils.HarmonyHelper;
using HarmonyLib;
using MoreFertilizers.Framework;
using Netcode;
using StardewValley.Buildings;

namespace MoreFertilizers.HarmonyPatches.DomesticatedFishFood;

/// <summary>
/// Holds transpilers against FishPond.dayUpdate.
/// </summary>
[HarmonyPatch(typeof(FishPond))]
internal static class FishPondDayUpdateTranspiler
{
    private static int GetAdditionalGrowthFactor(Random r, FishPond pond)
    {
        try
        {
            if (pond.modData?.GetBool(CanPlaceHandler.DomesticatedFishFood) == true
                && r.NextDouble() < 0.15)
            {
                ModEntry.ModMonitor.DebugOnlyLog($"Speeding up fish growth at pond at {pond.tileX}, {pond.tileY}", LogLevel.Info);
                return 2;
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Error in speeding up fish growth in fish ponds!\n\n{ex}", LogLevel.Error);
        }
        return 1;
    }

#pragma warning disable SA1116 // Split parameters should start on line after declaration
    [HarmonyPatch(nameof(FishPond.dayUpdate))]
    private static IEnumerable<CodeInstruction>? Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
    {
        try
        {
            ILHelper helper = new(original, instructions, ModEntry.ModMonitor, gen);
            helper.FindNext(new CodeInstructionWrapper[]
            {
                new(OpCodes.Newobj, typeof(Random).Constructor(new[] { typeof(int) })),
                new(SpecialCodeInstructionCases.StLoc),
            }).Advance(1);

            CodeInstruction? local = helper.CurrentInstruction.ToLdLoc();

            helper.FindNext(new CodeInstructionWrapper[]
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, typeof(FishPond).InstanceFieldNamed(nameof(FishPond.daysSinceSpawn))),
                new(OpCodes.Callvirt, typeof(NetFieldBase<int, NetInt>).InstancePropertyNamed("Value").GetGetMethod()),
                new(OpCodes.Ldc_I4_1),
                new(OpCodes.Add),
            })
            .FindNext(new CodeInstructionWrapper[]
            {
                new(OpCodes.Ldc_I4_1),
            })
            .GetLabels(out IList<Label>? labelsToMove)
            .ReplaceInstruction(OpCodes.Call, typeof(FishPondDayUpdateTranspiler).StaticMethodNamed(nameof(GetAdditionalGrowthFactor)))
            .Insert(new CodeInstruction[]
            {
                local,
                new(OpCodes.Ldarg_0),
            }, withLabels: labelsToMove);

            return helper.Render();
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Mod crashed while transpiling FishPond.dayUpdate:\n\n{ex}", LogLevel.Error);
        }
        return null;
    }
#pragma warning restore SA1116 // Split parameters should start on line after declaration
}