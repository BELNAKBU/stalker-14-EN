using System.Linq;
using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;
using Content.Shared.Whitelist;
using Content.Shared._Stalker.Weapon;
using Content.Shared._Stalker.Weapon.Projectile;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Stalker_EN.Examine;

/// <summary>
/// System for comparing armor and weapon stats between examined items and currently equipped items.
/// Provides a "Compare Stats" verb for items with ArmorComponent, ClothingComponent, and GunComponent.
/// </summary>
public sealed class StatsExamineSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private const float SpreadReferenceAngle = 95f; // Reference angle for spread calculation (95° ensures minAngle has proper impact on score)
    private const float StabilityMultiplier = 10f; // Multiplier for stability calculation (AngleDecay / (AngleIncrease * 10))

    private StatsExamineWindow? _window;
    private List<(EntityPrototype proto, CartridgeAmmoComponent cartridge)>? _cartridgeCache;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingComponent, GetVerbsEvent<ExamineVerb>>(OnClothingStatsVerb);
        SubscribeLocalEvent<GunComponent, GetVerbsEvent<ExamineVerb>>(OnGunStatsVerb);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        // Invalidate cartridge cache when EntityPrototype is modified
        if (args.WasModified<EntityPrototype>())
        {
            _cartridgeCache = null;
        }
    }

    /// <summary>
    /// Adds a "Compare Stats" verb to items with ArmorComponent and ClothingComponent.
    /// </summary>
    private void OnClothingStatsVerb(EntityUid uid, ClothingComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Only add the compare verb if the item has armor component
        if (!TryComp<ArmorComponent>(uid, out var armor))
            return;

        if (armor.Hidden || armor.HiddenExamine)
            return;

        // Create a separate verb for stat comparison
        var verb = new ExamineVerb
        {
            ClientExclusive = true,
            Act = () => OpenStatsWindow(args.User, args.Target, armor, component),
            Text = "Compare Stats",
            Message = "Compare armor stats with currently equipped item",
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Priority = -1 // Show after the standard armor examine verb
        };

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Adds a "Compare Stats" verb to items with GunComponent.
    /// </summary>
    private void OnGunStatsVerb(EntityUid uid, GunComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Only add the compare verb if the gun is examinable
        if (!component.ShowExamineText)
            return;

        var verb = new ExamineVerb
        {
            ClientExclusive = true,
            Act = () => OpenStatsWindow(args.User, args.Target, component),
            Text = "Compare Stats",
            Message = "Compare weapon stats with currently equipped weapon",
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Priority = -2
        };

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Opens the stats comparison window for a spawned entity (weapon).
    /// Compares the examined weapon's stats with the equipped weapon.
    /// </summary>
    private void OpenStatsWindow(EntityUid user, EntityUid target, GunComponent examinedGun)
    {
        _window?.Close();
        _window = new StatsExamineWindow();
        _window.OpenCentered();

        // 1. Check if equipped weapon is wielded
        var isEquippedWielded = false;
        GunComponent? equippedGun = null;
        if (TryGetEquippedWeaponStats(user, out var equippedGunTemp) && equippedGunTemp != null)
        {
            equippedGun = equippedGunTemp;
            if (TryComp<WieldableComponent>(equippedGun.Owner, out var wieldable))
                isEquippedWielded = wieldable.Wielded;
        }

        // 2. Examined weapon — base + wield bonus ONLY if equipped is wielded
        EntityPrototype? examinedProto = null;
        if (isEquippedWielded)
        {
            var examinedProtoId = MetaData(target).EntityPrototype?.ID;
            if (examinedProtoId != null)
                _prototypeManager.TryIndex<EntityPrototype>(examinedProtoId, out examinedProto);
        }

        var (examinedSpread, examinedStability, examinedFireRate) = CalculateWeaponStats(examinedGun, examinedProto, isEquippedWielded);

        // Get examined weapon prototype for projectile stats
        float? examinedPveDamage = null;
        float? examinedPvpDamage = null;
        Dictionary<string, float>? examinedDamageTypes = null;
        Dictionary<string, float>? examinedPveDamageTypes = null;
        string? examinedCartridgeId = null;
        var examinedGunProtoId = MetaData(target).EntityPrototype?.ID;
        if (examinedGunProtoId != null && _prototypeManager.TryIndex<EntityPrototype>(examinedGunProtoId, out var examinedGunProto))
        {
            // Check if weapon is a shotgun (has BallisticAmmoProviderComponent)
            var isShotgun = examinedGunProto.TryGetComponent<BallisticAmmoProviderComponent>(out _, _componentFactory);

            if (isShotgun)
            {
                // Shotguns use cartridges with projectile class 1 for both PvE and PvP
                if (TryGetProjectileStats(examinedGunProto, 1, out var shotgunDamage, out _, out var shotgunDamageTypes, out var shotgunPveDamageTypes, out var shotgunCartridgeId))
                {
                    examinedPveDamage = shotgunDamage;
                    examinedPvpDamage = shotgunDamage;
                    examinedDamageTypes = shotgunDamageTypes;
                    examinedPveDamageTypes = shotgunPveDamageTypes;
                    examinedCartridgeId = shotgunCartridgeId;
                }
            }
            else
            {
                // Other weapons use class 0 for PvE and class 2 for PvP
                // Get PvE damage (projectile class 0)
                if (TryGetProjectileStats(examinedGunProto, 0, out var pveDamage, out _, out _, out var pveDmgTypes, out var pveCartridgeId))
                {
                    examinedPveDamage = pveDamage;
                    examinedPveDamageTypes = pveDmgTypes;
                    examinedCartridgeId = pveCartridgeId;
                }

                // Get PvP damage (projectile class 2)
                if (TryGetProjectileStats(examinedGunProto, 2, out _, out var pvpDamage, out var damageTypes, out _, out var pvpCartridgeId))
                {
                    examinedPvpDamage = pvpDamage;
                    examinedDamageTypes = damageTypes;
                    // Show both cartridge IDs if different
                    if (examinedCartridgeId != pvpCartridgeId && pvpCartridgeId != null)
                        examinedCartridgeId = $"{examinedCartridgeId} / {pvpCartridgeId}";
                }
            }
        }

        // 3. Equipped weapon — always apply wield bonus if wielded
        float? equippedSpread = null;
        float? equippedStability = null;
        float? equippedFireRate = null;

        if (equippedGun != null)
        {
            EntityPrototype? equippedProto = null;
            if (isEquippedWielded)
            {
                var equippedProtoId = MetaData(equippedGun.Owner).EntityPrototype?.ID;
                if (equippedProtoId != null)
                    _prototypeManager.TryIndex<EntityPrototype>(equippedProtoId, out equippedProto);
            }

            var (spread, stability, fireRate) = CalculateWeaponStats(equippedGun, equippedProto, isEquippedWielded);
            equippedSpread = spread;
            equippedStability = stability;
            equippedFireRate = fireRate;
        }

        // Get equipped weapon prototype for projectile stats
        float? equippedPveDamage = null;
        float? equippedPvpDamage = null;
        Dictionary<string, float>? equippedDamageTypes = null;
        Dictionary<string, float>? equippedPveDamageTypes = null;
        string? equippedCartridgeId = null;
        if (equippedGun != null)
        {
            var equippedGunProtoId = MetaData(equippedGun.Owner).EntityPrototype?.ID;
            if (equippedGunProtoId != null && _prototypeManager.TryIndex<EntityPrototype>(equippedGunProtoId, out var equippedGunProto))
            {
                // Check if equipped weapon is a shotgun
                var isEquippedShotgun = equippedGunProto.TryGetComponent<BallisticAmmoProviderComponent>(out _, _componentFactory);

                if (isEquippedShotgun)
                {
                    // Shotguns use cartridges with projectile class 1 for both PvE and PvP
                    if (TryGetProjectileStats(equippedGunProto, 1, out var shotgunDamage, out _, out var shotgunDamageTypes, out var shotgunPveDamageTypes, out var shotgunCartridgeId))
                    {
                        equippedPveDamage = shotgunDamage;
                        equippedPvpDamage = shotgunDamage;
                        equippedDamageTypes = shotgunDamageTypes;
                        equippedPveDamageTypes = shotgunPveDamageTypes;
                        equippedCartridgeId = shotgunCartridgeId;
                    }
                }
                else
                {
                    // Other weapons use class 0 for PvE and class 2 for PvP
                    // Get PvE damage (projectile class 0)
                    if (TryGetProjectileStats(equippedGunProto, 0, out var pveDamage, out _, out _, out var pveDmgTypes, out var pveCartridgeId))
                    {
                        equippedPveDamage = pveDamage;
                        equippedPveDamageTypes = pveDmgTypes;
                        equippedCartridgeId = pveCartridgeId;
                    }

                    // Get PvP damage (projectile class 2)
                    if (TryGetProjectileStats(equippedGunProto, 2, out _, out var pvpDamage, out var damageTypes, out _, out var pvpCartridgeId))
                    {
                        equippedPvpDamage = pvpDamage;
                        equippedDamageTypes = damageTypes;
                        // Show both cartridge IDs if different
                        if (equippedCartridgeId != pvpCartridgeId && pvpCartridgeId != null)
                            equippedCartridgeId = $"{equippedCartridgeId} / {pvpCartridgeId}";
                    }
                }
            }
        }

        // Get weapon names
        var examinedWeaponName = MetaData(target).EntityName;
        var equippedWeaponName = equippedGun != null ? MetaData(equippedGun.Owner).EntityName : null;

        _window.UpdateStats(new StatsExamineWindow.WeaponComparisonStats(
            new DamageModifierSet(), null, null, null, null, null,
            examinedSpread, examinedStability, equippedSpread, equippedStability,
            examinedFireRate, equippedFireRate,
            examinedPveDamage, examinedPvpDamage, examinedDamageTypes, examinedCartridgeId, examinedPveDamageTypes,
            equippedPveDamage, equippedPvpDamage, equippedDamageTypes, equippedCartridgeId, equippedPveDamageTypes,
            examinedWeaponName, equippedWeaponName));
    }

    /// <summary>
    /// Opens the stats comparison window for a spawned entity (armor).
    /// Compares the examined item's stats with the equipped item in the same slot.
    /// </summary>
    private void OpenStatsWindow(EntityUid user, EntityUid target, ArmorComponent examinedArmor, ClothingComponent examinedClothing)
    {
        _window?.Close();
        _window = new StatsExamineWindow();
        _window.OpenCentered();

        // Get the examined item's modifiers
        var examinedModifiers = examinedArmor.Modifiers ?? examinedArmor.BaseModifiers;
        var examinedArmorClass = examinedArmor.ArmorClass;

        // Get reflect chance if available
        float? examinedReflectProb = null;
        if (TryComp<ReflectComponent>(target, out var reflect))
        {
            examinedReflectProb = reflect.ReflectProb;
        }

        // Get the equipped item in the same slot
        TryGetEquippedArmorStats(user, examinedClothing.Slots, out var equippedModifiers, out var equippedArmorClass, out var equippedReflectProb);

        _window.UpdateArmorStats(examinedModifiers, examinedArmorClass, examinedReflectProb, equippedModifiers, equippedArmorClass, equippedReflectProb);
    }

    /// <summary>
    /// Opens the stats comparison window for a prototype (shop item).
    /// </summary>
    public void OpenStatsWindowFromPrototype(EntityUid user, string prototypeId)
    {
        _window?.Close();
        _window = new StatsExamineWindow();
        _window.OpenCentered();

        if (!_prototypeManager.TryIndex<EntityPrototype>(prototypeId, out var prototype))
            return;

        // 1. Check equipped weapon wielded state (once for the entire method)
        var isEquippedWielded = false;
        if (TryGetEquippedWeaponStats(user, out var equippedGunWieldCheck) && equippedGunWieldCheck != null)
        {
            if (TryComp<WieldableComponent>(equippedGunWieldCheck.Owner, out var wieldable))
                isEquippedWielded = wieldable.Wielded;
        }

        // Get components from prototype
        DamageModifierSet? examinedModifiers = null;
        int? examinedArmorClass = null;
        float? examinedReflectProb = null;
        SlotFlags? slotFlags = null;

        if (prototype.TryGetComponent<ArmorComponent>(out var armor, _componentFactory))
        {
            examinedModifiers = armor.Modifiers ?? armor.BaseModifiers;
            examinedArmorClass = armor.ArmorClass;
        }

        if (prototype.TryGetComponent<ReflectComponent>(out var reflect, _componentFactory))
        {
            examinedReflectProb = reflect.ReflectProb;
        }

        if (prototype.TryGetComponent<ClothingComponent>(out var clothing, _componentFactory))
        {
            slotFlags = clothing.Slots;
        }

        // 2. Weapon stats for examined (prototype)
        float? examinedSpread = null;
        float? examinedStability = null;
        float? examinedFireRate = null;
        float? examinedPveDamage = null;
        float? examinedPvpDamage = null;
        Dictionary<string, float>? examinedDamageTypes = null;
        Dictionary<string, float>? examinedPveDamageTypes = null;
        string? examinedCartridgeId = null;

        // Check if weapon is a shotgun (has BallisticAmmoProviderComponent)
        var isShotgun = prototype.TryGetComponent<BallisticAmmoProviderComponent>(out _, _componentFactory);

        if (prototype.TryGetComponent<GunComponent>(out var gun, _componentFactory))
        {
            var (spread, stability, fireRate) = CalculateWeaponStats(gun, prototype, isEquippedWielded);
            examinedSpread = spread;
            examinedStability = stability;
            examinedFireRate = fireRate;

            if (isShotgun)
            {
                // Shotguns use cartridges with projectile class 1 for both PvE and PvP
                if (TryGetProjectileStats(prototype, 1, out var shotgunDamage, out _, out var shotgunDamageTypes, out var shotgunPveDamageTypes, out var shotgunCartridgeId))
                {
                    examinedPveDamage = shotgunDamage;
                    examinedPvpDamage = shotgunDamage;
                    examinedDamageTypes = shotgunDamageTypes;
                    examinedPveDamageTypes = shotgunPveDamageTypes;
                    examinedCartridgeId = shotgunCartridgeId;
                }
            }
            else
            {
                // Other weapons use class 0 for PvE and class 2 for PvP
                // Get PvE damage (projectile class 0)
                if (TryGetProjectileStats(prototype, 0, out var pveDamage, out _, out _, out var pveDmgTypes, out var pveCartridgeId))
                {
                    examinedPveDamage = pveDamage;
                    examinedPveDamageTypes = pveDmgTypes;
                    examinedCartridgeId = pveCartridgeId;
                }

                // Get PvP damage (projectile class 2)
                if (TryGetProjectileStats(prototype, 2, out _, out var pvpDamage, out var damageTypes, out _, out var pvpCartridgeId))
                {
                    examinedPvpDamage = pvpDamage;
                    examinedDamageTypes = damageTypes;
                    // Show both cartridge IDs if different
                    if (examinedCartridgeId != pvpCartridgeId && pvpCartridgeId != null)
                        examinedCartridgeId = $"{examinedCartridgeId} / {pvpCartridgeId}";
                }
            }
        }

        // Get the equipped item in the same slot
        DamageModifierSet? equippedModifiers = null;
        int? equippedArmorClass = null;
        float? equippedReflectProb = null;

        if (slotFlags.HasValue)
        {
            if (TryGetEquippedArmorStats(user, slotFlags.Value, out var armorModifiers, out var armorClass, out var reflectProb))
            {
                equippedModifiers = armorModifiers;
                equippedArmorClass = armorClass;
                equippedReflectProb = reflectProb;
            }
        }

        // 3. Equipped weapon stats (with wield bonus if needed)
        float? equippedSpread = null;
        float? equippedStability = null;
        float? equippedFireRate = null;
        float? equippedPveDamage = null;
        float? equippedPvpDamage = null;
        Dictionary<string, float>? equippedDamageTypes = null;
        Dictionary<string, float>? equippedPveDamageTypes = null;
        string? equippedCartridgeId = null;
        if (TryGetEquippedWeaponStats(user, out var equippedGun))
        {
            EntityPrototype? equippedProto = null;
            if (isEquippedWielded)
            {
                var equippedProtoId = MetaData(equippedGun!.Owner).EntityPrototype?.ID;
                if (equippedProtoId != null)
                    _prototypeManager.TryIndex<EntityPrototype>(equippedProtoId, out equippedProto);
            }

            var (spread, stability, fireRate) = CalculateWeaponStats(equippedGun!, equippedProto, isEquippedWielded);
            equippedSpread = spread;
            equippedStability = stability;
            equippedFireRate = fireRate;

            // Get equipped weapon prototype for projectile stats
            var equippedGunProtoId = MetaData(equippedGun!.Owner).EntityPrototype?.ID;
            if (equippedGunProtoId != null && _prototypeManager.TryIndex<EntityPrototype>(equippedGunProtoId, out var equippedGunProto))
            {
                // Check if equipped weapon is a shotgun
                var isEquippedShotgun = equippedGunProto.TryGetComponent<BallisticAmmoProviderComponent>(out _, _componentFactory);

                if (isEquippedShotgun)
                {
                    // Shotguns use cartridges with projectile class 1 for both PvE and PvP
                    if (TryGetProjectileStats(equippedGunProto, 1, out var shotgunDamage, out _, out var shotgunDamageTypes, out var shotgunPveDamageTypes, out var shotgunCartridgeId))
                    {
                        equippedPveDamage = shotgunDamage;
                        equippedPvpDamage = shotgunDamage;
                        equippedDamageTypes = shotgunDamageTypes;
                        equippedPveDamageTypes = shotgunPveDamageTypes;
                        equippedCartridgeId = shotgunCartridgeId;
                    }
                }
                else
                {
                    // Other weapons use class 0 for PvE and class 2 for PvP
                    // Get PvE damage (projectile class 0)
                    if (TryGetProjectileStats(equippedGunProto, 0, out var pveDamage, out _, out _, out var pveDmgTypes, out var pveCartridgeId))
                    {
                        equippedPveDamage = pveDamage;
                        equippedPveDamageTypes = pveDmgTypes;
                        equippedCartridgeId = pveCartridgeId;
                    }

                    // Get PvP damage (projectile class 2)
                    if (TryGetProjectileStats(equippedGunProto, 2, out _, out var pvpDamage, out var damageTypes, out _, out var pvpCartridgeId))
                    {
                        equippedPvpDamage = pvpDamage;
                        equippedDamageTypes = damageTypes;
                        // Show both cartridge IDs if different
                        if (equippedCartridgeId != pvpCartridgeId && pvpCartridgeId != null)
                            equippedCartridgeId = $"{equippedCartridgeId} / {pvpCartridgeId}";
                    }
                }
            }
        }

        // Get weapon names
        var examinedWeaponName = prototype.Name;
        var equippedWeaponName = equippedGun != null ? MetaData(equippedGun.Owner).EntityName : null;

        _window.UpdateStats(new StatsExamineWindow.WeaponComparisonStats(
            examinedModifiers ?? new DamageModifierSet(), examinedArmorClass, examinedReflectProb,
            equippedModifiers, equippedArmorClass, equippedReflectProb,
            examinedSpread, examinedStability, equippedSpread, equippedStability,
            examinedFireRate, equippedFireRate,
            examinedPveDamage, examinedPvpDamage, examinedDamageTypes, examinedCartridgeId, examinedPveDamageTypes,
            equippedPveDamage, equippedPvpDamage, equippedDamageTypes, equippedCartridgeId, equippedPveDamageTypes,
            examinedWeaponName, equippedWeaponName));
    }

    /// <summary>
    /// Gets the stats of the equipped item in the specified slot.
    /// Uses exact flag matching first, then falls back to partial matching.
    /// </summary>
    private bool TryGetEquippedArmorStats(
        EntityUid user,
        SlotFlags slotFlags,
        out DamageModifierSet? modifiers,
        out int? armorClass,
        out float? reflectProb)
    {
        modifiers = null;
        armorClass = null;
        reflectProb = null;

        if (!TryComp<InventoryComponent>(user, out var inventory))
            return false;

        // First try to find exact match (slot flags exactly match item flags)
        foreach (var slotDef in inventory.Slots)
        {
            if (slotDef.SlotFlags == slotFlags)
            {
                if (_inventory.TryGetSlotEntity(user, slotDef.Name, out var equippedUid))
                {
                    if (TryComp<ArmorComponent>(equippedUid, out var equippedArmor))
                    {
                        modifiers = equippedArmor.Modifiers ?? equippedArmor.BaseModifiers;
                        armorClass = equippedArmor.ArmorClass;
                    }

                    if (TryComp<ReflectComponent>(equippedUid, out var equippedReflect))
                    {
                        reflectProb = equippedReflect.ReflectProb;
                    }

                    return true;
                }
            }
        }

        // If no exact match found, try partial match (slot contains all item flags)
        if (modifiers == null)
        {
            foreach (var slotDef in inventory.Slots)
            {
                if ((slotDef.SlotFlags & slotFlags) == slotFlags)
                {
                    if (_inventory.TryGetSlotEntity(user, slotDef.Name, out var equippedUid))
                    {
                        if (TryComp<ArmorComponent>(equippedUid, out var equippedArmor))
                        {
                            modifiers = equippedArmor.Modifiers ?? equippedArmor.BaseModifiers;
                            armorClass = equippedArmor.ArmorClass;
                        }

                        if (TryComp<ReflectComponent>(equippedUid, out var equippedReflect))
                        {
                            reflectProb = equippedReflect.ReflectProb;
                        }

                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Calculates weapon stats (spread, stability, fire rate) with optional wield bonus application.
    /// </summary>
    private (float spread, float stability, float fireRate) CalculateWeaponStats(
        GunComponent gun,
        EntityPrototype? proto,
        bool applyWieldBonus)
    {
        float minAngle = (float)gun.MinAngle.Degrees;
        float maxAngle = (float)gun.MaxAngle.Degrees;
        float angleDecay = (float)gun.AngleDecay.Degrees;
        float angleIncrease = (float)gun.AngleIncrease.Degrees;

        if (applyWieldBonus && proto != null)
        {
            if (proto.TryGetComponent<GunWieldBonusComponent>(out var wieldBonus, _componentFactory))
            {
                minAngle = Math.Max(0, minAngle + (float)wieldBonus.MinAngle.Degrees);
                maxAngle = Math.Max(0, maxAngle + (float)wieldBonus.MaxAngle.Degrees);
                angleDecay += (float)wieldBonus.AngleDecay.Degrees;
                angleIncrease += (float)wieldBonus.AngleIncrease.Degrees;
            }
        }

        var spread = CalcSpread(minAngle, maxAngle);
        var fireRate = gun.FireRate;
        var fireDelay = fireRate > 0 ? 1f / fireRate : 0f;
        var stability = CalcStability(angleDecay, angleIncrease, fireDelay);

        return (spread, stability, fireRate);
    }

    /// <summary>
    /// Calculates spread percentage from min and max angle.
    /// Spread = 100 - (avgAngle / SpreadReferenceAngle * 100), clamped to 0-100.
    /// Uses average of MinAngle and MaxAngle for more honest representation.
    /// Lower angle = better accuracy (less spread).
    /// No guard clause needed since angles are always positive.
    /// </summary>
    private static float CalcSpread(float minAngle, float maxAngle)
    {
        var avg = (minAngle + maxAngle) / 2f;
        return Math.Clamp(100f - (avg / SpreadReferenceAngle * 100f), 0f, 100f);
    }

    /// <summary>
    /// Gets projectile stats (damage) from a gun prototype.
    /// Stalker weapons use three ammo systems:
    /// - BallisticAmmoProvider (shotguns - direct cartridge loading)
    /// - ItemSlots with gun_magazine (rifles - magazine-based)
    /// - RevolverAmmoProvider (revolvers - cylinder-based)
    /// </summary>
    private bool TryGetProjectileStats(EntityPrototype gunProto, int? projectileClass, out float pveDamage, out float pvpDamage, out Dictionary<string, float> damageTypes, out Dictionary<string, float> pveDamageTypes, out string cartridgeId)
    {
        pveDamage = 0f;
        pvpDamage = 0f;
        damageTypes = new Dictionary<string, float>();
        pveDamageTypes = new Dictionary<string, float>();
        cartridgeId = string.Empty;

        // Find cartridge prototype from gun
        var cartridgeProto = FindCartridgeFromGunProto(gunProto, projectileClass);
        if (cartridgeProto == null)
            return false;

        // Get projectile from cartridge
        if (!cartridgeProto.TryGetComponent<CartridgeAmmoComponent>(out var cartridge, _componentFactory))
            return false;

        // Only set cartridgeId if it's actually a cartridge (not a projectile)
        cartridgeId = cartridgeProto.ID;

        if (!_prototypeManager.TryIndex<EntityPrototype>(cartridge.Prototype, out var projectileProto))
            return false;

        // Check for shotgun pellets (ProjectileSpread) - if present, get damage from pellet proto
        int pelletCount = 1;
        EntityPrototype damageProjectileProto = projectileProto;
        if (projectileProto.TryGetComponent<ProjectileSpreadComponent>(out var spread, _componentFactory))
        {
            pelletCount = Math.Max(1, spread.Count);
            // Get the pellet prototype to get the actual damage per pellet
            if (_prototypeManager.TryIndex<EntityPrototype>(spread.Proto, out var pelletProto))
            {
                damageProjectileProto = pelletProto;
            }
        }

        // Calculate damage from projectile
        (pveDamage, pvpDamage, damageTypes, pveDamageTypes) = CalculateDamageFromProjectile(damageProjectileProto, pelletCount);

        // Return true if we found a projectile (even if damage is 0 or only Mutant)
        return projectileProto.TryGetComponent<ProjectileComponent>(out _, _componentFactory);
    }

    /// <summary>
    /// Finds cartridge prototype from gun prototype based on ammo provider type.
    /// Handles BallisticAmmoProvider (shotguns), RevolverAmmoProvider (revolvers), and ItemSlots (rifles with magazines).
    /// </summary>
    private EntityPrototype? FindCartridgeFromGunProto(EntityPrototype gunProto, int? projectileClass)
    {
        EntityPrototype? cartridgeProto = null;

        // Try BallisticAmmoProvider (shotguns)
        if (gunProto.TryGetComponent<BallisticAmmoProviderComponent>(out var ballisticProvider, _componentFactory))
        {
            if (ballisticProvider.Proto == null)
            {
                cartridgeProto = FindCartridgeFromWhitelist(ballisticProvider.Whitelist, projectileClass);
            }
            else
            {
                if (!_prototypeManager.TryIndex<EntityPrototype>(ballisticProvider.Proto, out cartridgeProto))
                    return null;

                // Check if it's actually a cartridge (has CartridgeAmmoComponent)
                // If not, it might be a projectile directly, so use whitelist instead
                if (!cartridgeProto.TryGetComponent<CartridgeAmmoComponent>(out _, _componentFactory))
                {
                    cartridgeProto = FindCartridgeFromWhitelist(ballisticProvider.Whitelist, projectileClass);
                }
            }
        }
        // Try RevolverAmmoProvider (revolvers)
        else if (gunProto.TryGetComponent<RevolverAmmoProviderComponent>(out var revolverProvider, _componentFactory))
        {
            if (revolverProvider.FillPrototype == null)
            {
                cartridgeProto = FindCartridgeFromWhitelist(revolverProvider.Whitelist, projectileClass);
            }
            else
            {
                if (!_prototypeManager.TryIndex<EntityPrototype>(revolverProvider.FillPrototype, out cartridgeProto))
                    return null;

                // Check if it's actually a cartridge (has CartridgeAmmoComponent)
                // If not, it might be a projectile directly, so use whitelist instead
                if (cartridgeProto != null && !cartridgeProto.TryGetComponent<CartridgeAmmoComponent>(out _, _componentFactory))
                {
                    cartridgeProto = FindCartridgeFromWhitelist(revolverProvider.Whitelist, projectileClass);
                }
            }
        }
        // Try ItemSlots with gun_magazine (rifles with magazines)
        else if (gunProto.TryGetComponent<ItemSlotsComponent>(out var gunItemSlots, _componentFactory))
        {
            EntityPrototype? magazineProto = null;
            foreach (var (slotName, slot) in gunItemSlots.Slots)
            {
                if (slotName == "gun_magazine" && slot.StartingItem != null)
                {
                    if (_prototypeManager.TryIndex<EntityPrototype>(slot.StartingItem, out var magProto))
                    {
                        magazineProto = magProto;
                        break;
                    }
                }
            }

            if (magazineProto == null)
                return null;

            // Get cartridge from magazine (magazines have BallisticAmmoProvider)
            if (!magazineProto.TryGetComponent<BallisticAmmoProviderComponent>(out var magBallisticProvider, _componentFactory))
                return null;

            if (magBallisticProvider.Proto == null)
            {
                cartridgeProto = FindCartridgeFromWhitelist(magBallisticProvider.Whitelist, projectileClass);
            }
            else
            {
                if (!_prototypeManager.TryIndex<EntityPrototype>(magBallisticProvider.Proto, out cartridgeProto))
                    return null;

                // Check if it's actually a cartridge (has CartridgeAmmoComponent)
                // If not, it might be a projectile directly, so use whitelist instead
                if (!cartridgeProto.TryGetComponent<CartridgeAmmoComponent>(out _, _componentFactory))
                {
                    cartridgeProto = FindCartridgeFromWhitelist(magBallisticProvider.Whitelist, projectileClass);
                }
            }
        }

        return cartridgeProto;
    }

    /// <summary>
    /// Calculates PvE and PvP damage from projectile prototype, handling pellet scaling for shotguns.
    /// </summary>
    private (float pveDamage, float pvpDamage, Dictionary<string, float> damageTypes, Dictionary<string, float> pveDamageTypes) CalculateDamageFromProjectile(EntityPrototype projectileProto, int pelletCount)
    {
        float pveDamage = 0f;
        float pvpDamage = 0f;
        var damageTypes = new Dictionary<string, float>();
        var pveDamageTypes = new Dictionary<string, float>();

        if (projectileProto.TryGetComponent<ProjectileComponent>(out var projectile, _componentFactory))
        {
            foreach (var (damageType, value) in projectile.Damage.DamageDict)
            {
                var damageValue = (float)value;
                pveDamage += damageValue; // PvE includes all damage types
                pveDamageTypes[damageType] = damageValue; // Track all damage types for PvE

                if (damageType != "Mutant")
                {
                    pvpDamage += damageValue; // PvP excludes Mutant damage
                    damageTypes[damageType] = damageValue; // Track individual damage types for PvP
                }
            }
        }

        pveDamage *= pelletCount;
        pvpDamage *= pelletCount;

        // Scale individual damage types by pellet count
        foreach (var key in damageTypes.Keys.ToList())
        {
            damageTypes[key] *= pelletCount;
        }

        // Scale PvE damage types by pellet count
        foreach (var key in pveDamageTypes.Keys.ToList())
        {
            pveDamageTypes[key] *= pelletCount;
        }

        return (pveDamage, pvpDamage, damageTypes, pveDamageTypes);
    }

    /// <summary>
    /// Checks if a prototype matches the given whitelist tags.
    /// </summary>
    private bool MatchesWhitelist(EntityPrototype proto, EntityWhitelist? whitelist)
    {
        if (whitelist?.Tags == null || whitelist.Tags.Count == 0)
            return true;

        if (!proto.TryGetComponent<TagComponent>(out var tags, _componentFactory) || tags.Tags == null)
            return false;

        return whitelist.RequireAll
            ? whitelist.Tags.All(t => tags.Tags.Any(ct => ct.Id == t.Id))
            : whitelist.Tags.Any(t => tags.Tags.Any(ct => ct.Id == t.Id));
    }

    /// <summary>
    /// Gets all cartridges with their components, cached for the session.
    /// </summary>
    private List<(EntityPrototype proto, CartridgeAmmoComponent cartridge)> GetAllCartridges()
    {
        if (_cartridgeCache != null)
            return _cartridgeCache;

        _cartridgeCache = _prototypeManager.EnumeratePrototypes<EntityPrototype>()
            .Select(p => (p, c: p.TryGetComponent<CartridgeAmmoComponent>(out var c, _componentFactory) ? c : null))
            .Where(x => x.c != null)
            .Select(x => (x.p, x.c!))
            .ToList();

        return _cartridgeCache;
    }

    /// <summary>
    /// Finds cartridge prototype from whitelist based on projectile class.
    /// First tries exact projectile class match, then null class, then fallback for PvP.
    /// If projectile class is 2 (PvP) and still no match, falls back to any cartridge except class 0.
    /// </summary>
    private EntityPrototype? FindCartridgeFromWhitelist(EntityWhitelist? whitelist, int? projectileClass = null)
    {
        var allCartridges = GetAllCartridges();

        // First pass: try to find exact projectile class match
        if (projectileClass.HasValue)
        {
            var exactMatch = allCartridges
                .Where(x => MatchesWhitelist(x.proto, whitelist))
                .Where(x =>
                {
                    if (!_prototypeManager.TryIndex<EntityPrototype>(x.cartridge.Prototype, out var projectileProto))
                        return false;

                    if (!projectileProto.TryGetComponent<ProjectileComponent>(out var projectile, _componentFactory))
                        return false;

                    // Match if projectile class is null (not specified) or matches the requested class
                    if (projectile.ProjectileClass != null && projectile.ProjectileClass != projectileClass.Value)
                        return false;

                    return true;
                })
                .OrderBy(x => x.proto.ID)
                .FirstOrDefault();

            if (exactMatch.proto != null)
                return exactMatch.proto;
        }

        // Second pass: try cartridges without a projectile class (null)
        var nullClassMatch = allCartridges
            .Where(x => MatchesWhitelist(x.proto, whitelist))
            .Where(x =>
            {
                if (!_prototypeManager.TryIndex<EntityPrototype>(x.cartridge.Prototype, out var projectileProto))
                    return false;

                if (!projectileProto.TryGetComponent<ProjectileComponent>(out var projectile, _componentFactory))
                    return false;

                // Only match if projectile class is null (not specified)
                if (projectile.ProjectileClass != null)
                    return false;

                return true;
            })
            .OrderBy(x => x.proto.ID)
            .FirstOrDefault();

        if (nullClassMatch.proto != null)
            return nullClassMatch.proto;

        // Third pass: for PvP (class 2), fall back to any cartridge except class 0
        if (projectileClass == 2)
        {
            var fallbackMatch = allCartridges
                .Where(x => MatchesWhitelist(x.proto, whitelist))
                .Where(x =>
                {
                    if (!_prototypeManager.TryIndex<EntityPrototype>(x.cartridge.Prototype, out var projectileProto))
                        return false;

                    if (!projectileProto.TryGetComponent<ProjectileComponent>(out var projectile, _componentFactory))
                        return false;

                    // Exclude class 0 (PvE only)
                    if (projectile.ProjectileClass == 0)
                        return false;

                    return true;
                })
                .OrderBy(x => x.proto.ID)
                .FirstOrDefault();

            if (fallbackMatch.proto != null)
                return fallbackMatch.proto;
        }

        return null;
    }

    /// <summary>
    /// Calculates stability percentage from angle decay, angle increase, and fire rate.
    /// Stability = (decayPerSecond * fireDelaySeconds) / increasePerShot * 100, clamped to 0-100.
    /// Accounts for fire rate: higher fire rate = less time between shots = less recovery per cycle = lower stability.
    /// </summary>
    private static float CalcStability(float decayPerSecond, float increasePerShot, float fireDelaySeconds)
    {
        if (fireDelaySeconds <= 0f) return 100f;
        if (increasePerShot <= 0f) return 100f;

        float recoveryPerCycle = decayPerSecond * fireDelaySeconds;

        if (recoveryPerCycle >= increasePerShot)
            return 100f;

        return Math.Clamp(recoveryPerCycle / increasePerShot * 100f, 0f, 100f);
    }

    /// <summary>
    /// Gets the equipped weapon from the active hand.
    /// </summary>
    private bool TryGetEquippedWeaponStats(EntityUid user, out GunComponent? gun)
    {
        gun = null;

        if (_hands.GetActiveItem(user) is { } activeItem && TryComp<GunComponent>(activeItem, out var gunComp))
        {
            gun = gunComp;
            return true;
        }

        return false;
    }
}
