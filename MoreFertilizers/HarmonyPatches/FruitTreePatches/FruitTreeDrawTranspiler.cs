using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using AtraBase.Toolkit;
using AtraBase.Toolkit.Reflection;
using AtraShared.Utils.Extensions;
using AtraShared.Utils.HarmonyHelper;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MoreFertilizers.Framework;
using StardewValley.TerrainFeatures;

namespace MoreFertilizers.HarmonyPatches.FruitTreePatches;

/// <summary>
/// Transpilers to color fertilized fruit trees.
/// </summary>
[HarmonyPatch(typeof(FruitTree))]
internal static class FruitTreeDrawTranspiler
{
    /// <summary>
    /// Applies this patch to DGA.
    /// </summary>
    /// <param name="harmony">Harmony instance.</param>
    internal static void ApplyDGAPatch(Harmony harmony)
    {
        try
        {
            Type dgaFruitTree = AccessTools.TypeByName("DynamicGameAssets.Game.CustomFruitTree") ?? throw new("DGA Fruit trees not found!");
            harmony.Patch(
                original: dgaFruitTree.InstanceMethodNamed("draw"),
                transpiler: new HarmonyMethod(typeof(FruitTreeDrawTranspiler), nameof(Transpiler)));
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Mod crashed while transpiling DGA. Integration may not work correctly.\n\n{ex}", LogLevel.Error);
        }
    }

    [MethodImpl(TKConstants.Hot)]
    private static Color ReplaceColorIfNeeded(Color prevcolor, FruitTree tree)
    {
        try
        {
            if (tree.modData?.GetInt(CanPlaceHandler.FruitTreeFertilizer) is int result)
            {
                return result > 1 ? Color.Red : Color.Orange;
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.LogOnce($"Crash while drawing fruit trees!\n\n{ex}", LogLevel.Error);
        }
        return prevcolor;
    }

    [HarmonyPatch(nameof(FruitTree.draw))]
    private static IEnumerable<CodeInstruction>? Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
    {
        try
        {
            ILHelper helper = new(original, instructions, ModEntry.ModMonitor, gen);
            helper.FindNext(new CodeInstructionWrapper[]
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, typeof(FruitTree).InstanceFieldNamed(nameof(FruitTree.growthStage))),
                new(OpCodes.Call),
                new(OpCodes.Ldc_I4_4),
                new(OpCodes.Bge),
            })
            .FindNext(new CodeInstructionWrapper[]
            {
                new(OpCodes.Call, typeof(Color).StaticPropertyNamed(nameof(Color.White)).GetGetMethod()),
            })
            .Advance(1)
            .Insert(new CodeInstruction[]
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, typeof(FruitTreeDrawTranspiler).StaticMethodNamed(nameof(FruitTreeDrawTranspiler.ReplaceColorIfNeeded))),
            });

            // helper.Print();
            return helper.Render();
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Mod crashed while transpiling FruitTree.Draw:\n\n{ex}", LogLevel.Error);
        }
        return null;
    }
}
