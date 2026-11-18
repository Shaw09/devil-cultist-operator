using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;

namespace Devil.Server.Loadouts;

[Injectable(InjectionType.Singleton)]
public sealed class ReserveAmmoPatch : AbstractPatch
{
    private const string M4A1 = "5447a9cd4bdc2dbd208b4567";
    private const string M855A1 = "54527ac44bdc2d36668b4567";
    private const string Vector = "5fb64bc92b1b027b1f50bcf2";
    private const string ApAmmo = "5efb0cabfb3e451d70735af5";
    private const string M4Magazine = "59c1383d86f774290a37e0ca";
    private const string VectorMagazine = "5fb651dc85f90547f674b6f4";

    private bool _enabled;

    public ReserveAmmoPatch() : base(nameof(ReserveAmmoPatch))
    {
    }

    internal void Start()
    {
        if (_enabled)
        {
            return;
        }

        Enable();
        _enabled = true;
    }

    protected override MethodBase? GetTargetMethod()
    {
        return typeof(BotInventoryGenerator).GetMethod(
            "GenerateInventory",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [typeof(MongoId), typeof(MongoId), typeof(BotType), typeof(BotGenerationDetails)],
            null);
    }

    [PatchPostfix]
    private static void SetReserveAmmo(BotBaseInventory __result, BotGenerationDetails botGenerationDetails)
    {
        var items = __result.Items;
        if (items is null)
        {
            return;
        }

        var role = botGenerationDetails.RoleLowercase;
        if (role != "bossdevil")
        {
            return;
        }

        Item? primary = null;
        foreach (var item in items)
        {
            if (item.SlotId == "FirstPrimaryWeapon")
            {
                primary = item;
                break;
            }
        }

        var primaryTpl = primary?.Template.ToString();

        if (primaryTpl == M4A1)
        {
            SetAmmoStackCount(items, M855A1, 60, 400);
            TrimSpareMagazines(items, M4Magazine, 4);
        }

        SetAmmoStackCount(items, ApAmmo, 50, 60);
        TrimSpareMagazines(items, VectorMagazine, 4);
    }

    private static void SetAmmoStackCount(List<Item> items, string ammoTpl, int stackSize, int total)
    {
        Item? secureContainer = null;
        foreach (var item in items)
        {
            if (item.SlotId == "SecuredContainer")
            {
                secureContainer = item;
                break;
            }
        }

        if (secureContainer is null)
        {
            return;
        }

        var containerId = secureContainer.Id.ToString();
        var stacks = new List<Item>();
        foreach (var item in items)
        {
            if (item.ParentId == containerId && item.Template.ToString() == ammoTpl)
            {
                stacks.Add(item);
            }
        }

        var remaining = total;
        foreach (var stack in stacks)
        {
            if (remaining == 0)
            {
                items.Remove(stack);
                continue;
            }

            var amount = Math.Min(stackSize, remaining);
            stack.Upd ??= new Upd();
            stack.Upd.StackObjectsCount = amount;
            remaining -= amount;
        }
    }

    private static void TrimSpareMagazines(List<Item> items, string magazineTpl, int spareCount)
    {
        var spareMagazines = new List<Item>();
        foreach (var item in items)
        {
            if (item.Template.ToString() == magazineTpl && item.SlotId != "mod_magazine")
            {
                spareMagazines.Add(item);
            }
        }

        for (var i = spareCount; i < spareMagazines.Count; i++)
        {
            RemoveWithChildren(items, spareMagazines[i]);
        }
    }

    private static void RemoveWithChildren(List<Item> items, Item parent)
    {
        var parentId = parent.Id.ToString();
        var children = new List<Item>();
        foreach (var item in items)
        {
            if (item.ParentId == parentId)
            {
                children.Add(item);
            }
        }

        foreach (var child in children)
        {
            RemoveWithChildren(items, child);
        }

        items.Remove(parent);
    }
}