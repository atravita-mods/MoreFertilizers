using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using AtraBase.Toolkit;
using AtraBase.Toolkit.Reflection;
using AtraShared.Utils.Extensions;
using AtraShared.Utils.HarmonyHelper;
using HarmonyLib;
using MoreFertilizers.Framework;
using Netcode;

namespace MoreFertilizers.HarmonyPatches.Compat;

/// <summary>
/// Holds transpiler against automate to handle organic crops.
/// </summary>
internal static class AutomateTranspiler
{
    /// <summary>
    /// Applies the patch against Automate.
    /// </summary>
    /// <param name="harmony">Harmony instance.</param>
    /// <exception cref="MethodNotFoundException">Some type or something wasn't found.</exception>
    internal static void ApplyPatches(Harmony harmony)
    {
        try
        {
            // Patch the generic automate machine.
            Type machine = AccessTools.TypeByName("Pathoschild.Stardew.Automate.Framework.GenericObjectMachine`1")?.MakeGenericType(typeof(SObject))
                ?? throw new MethodNotFoundException("Automate machine");
            Type storage = AccessTools.TypeByName("Pathoschild.Stardew.Automate.IStorage")
                ?? throw new MethodNotFoundException("Automate IStorage");
            Type recipe = AccessTools.TypeByName("Pathoschild.Stardew.Automate.IRecipe")
                ?? throw new MethodNotFoundException("Automate IRecipe");
            harmony.Patch(
                original: machine.InstanceMethodNamed("GenericPullRecipe", new[] { storage, recipe.MakeArrayType(), typeof(Item).MakeByRefType() }),
                transpiler: new HarmonyMethod(typeof(AutomateTranspiler), nameof(Transpiler)));

            // Patch the bone mill.
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Mod failed while transpiling automate. Integration may not work.\n\n{ex}", LogLevel.Error);
        }
    }

    /// <summary>
    /// Given a specific item, mark the output as organic if necessary.
    /// </summary>
    /// <param name="obj">The SObject to alter.</param>
    /// <param name="input">The input.</param>
    /// <returns>The altered SObject.</returns>
    [MethodImpl(TKConstants.Hot)]
    internal static SObject? MakeOrganic(SObject? obj, Item? input)
    {
        if (obj is not null && input?.modData?.GetBool(CanPlaceHandler.Organic) == true)
        {
            try
            {
                obj.modData?.SetBool(CanPlaceHandler.Organic, true);
                if (!obj.Name.Contains("Organic"))
                {
                    obj.Name += " (Organic)";
                }
                obj.MarkContextTagsDirty();
            }
            catch (Exception ex)
            {
                ModEntry.ModMonitor.Log($"Error in making Automate object organic\n\n{ex}", LogLevel.Error);
            }
        }
        return obj;
    }

    private static IEnumerable<CodeInstruction>? Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
    {
        try
        {
            ILHelper helper = new(original, instructions, ModEntry.ModMonitor, gen);

            helper.FindNext(new CodeInstructionWrapper[]
            { // These instructions will get a reference to the machine.
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call),
                new(OpCodes.Box),
            })
            .Copy(3, out IEnumerable<CodeInstruction>? copy)
            .FindNext(new CodeInstructionWrapper[]
            {
                new(OpCodes.Ldc_I4_1),
                new(OpCodes.Ret),
            })
            .GetLabels(out IList<Label>? labels)
            .Insert(copy.ToArray(), withLabels: labels)
            .Insert(new CodeInstruction[]
            {
                new(OpCodes.Ldfld, typeof(SObject).InstanceFieldNamed(nameof(SObject.heldObject))),
                new(OpCodes.Dup),
                new(OpCodes.Callvirt, typeof(NetFieldBase<SObject, NetRef<SObject>>).InstancePropertyNamed("Value").GetGetMethod()),
                new(OpCodes.Ldarg_3),
                new(OpCodes.Ldind_Ref),
                new(OpCodes.Call, typeof(AutomateTranspiler).StaticMethodNamed(nameof(MakeOrganic))),
                new(OpCodes.Callvirt, typeof(NetFieldBase<SObject, NetRef<SObject>>).InstancePropertyNamed("Value").GetSetMethod()),
            });

            // helper.Print();
            return helper.Render();
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Mod crashed while transpiling Automate:\n\n{ex}", LogLevel.Error);
        }
        return null;
    }
}