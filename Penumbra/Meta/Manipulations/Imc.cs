using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.String.Classes;

namespace Penumbra.Meta.Manipulations;

public readonly record struct ImcIdentifier(
    PrimaryId PrimaryId,
    Variant Variant,
    ObjectType ObjectType,
    SecondaryId SecondaryId,
    EquipSlot EquipSlot,
    BodySlot BodySlot) : IMetaIdentifier, IComparable<ImcIdentifier>
{
    public ImcIdentifier(EquipSlot slot, PrimaryId primaryId, ushort variant)
        : this(primaryId, (Variant)Math.Clamp(variant, (ushort)0, byte.MaxValue),
            slot.IsAccessory() ? ObjectType.Accessory : ObjectType.Equipment, 0, slot,
            variant > byte.MaxValue ? BodySlot.Body : BodySlot.Unknown)
    { }

    public ImcIdentifier(EquipSlot slot, PrimaryId primaryId, Variant variant)
        : this(primaryId, variant, slot.IsAccessory() ? ObjectType.Accessory : ObjectType.Equipment, 0, slot, BodySlot.Unknown)
    { }

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, object?> changedItems)
    {
        var path = ObjectType switch
        {
            ObjectType.Equipment or ObjectType.Accessory => GamePaths.Equipment.Mtrl.Path(PrimaryId, GenderRace.MidlanderMale, EquipSlot,
                Variant,
                "a"),
            ObjectType.Weapon => GamePaths.Weapon.Mtrl.Path(PrimaryId, SecondaryId.Id, Variant, "a"),
            ObjectType.DemiHuman => GamePaths.DemiHuman.Mtrl.Path(PrimaryId, SecondaryId.Id, EquipSlot, Variant,
                "a"),
            ObjectType.Monster => GamePaths.Monster.Mtrl.Path(PrimaryId, SecondaryId.Id, Variant, "a"),
            _                  => string.Empty,
        };
        if (path.Length == 0)
            return;

        identifier.Identify(changedItems, path);
    }

    public Utf8GamePath GamePath()
    {
        return ObjectType switch
        {
            ObjectType.Accessory => Utf8GamePath.FromString(GamePaths.Accessory.Imc.Path(PrimaryId), out var p) ? p : Utf8GamePath.Empty,
            ObjectType.Equipment => Utf8GamePath.FromString(GamePaths.Equipment.Imc.Path(PrimaryId), out var p) ? p : Utf8GamePath.Empty,
            ObjectType.DemiHuman => Utf8GamePath.FromString(GamePaths.DemiHuman.Imc.Path(PrimaryId, SecondaryId.Id), out var p)
                ? p
                : Utf8GamePath.Empty,
            ObjectType.Monster => Utf8GamePath.FromString(GamePaths.Monster.Imc.Path(PrimaryId, SecondaryId.Id), out var p)
                ? p
                : Utf8GamePath.Empty,
            ObjectType.Weapon => Utf8GamePath.FromString(GamePaths.Weapon.Imc.Path(PrimaryId, SecondaryId.Id), out var p)
                ? p
                : Utf8GamePath.Empty,
            _ => throw new NotImplementedException(),
        };
    }

    public MetaIndex FileIndex()
        => (MetaIndex)(-1);

    public override string ToString()
        => ObjectType is ObjectType.Equipment or ObjectType.Accessory
            ? $"Imc - {PrimaryId} - {EquipSlot.ToName()} - {Variant}"
            : $"Imc - {PrimaryId} - {ObjectType.ToName()} - {SecondaryId} - {BodySlot} - {Variant}";

    public bool Validate()
    {
        switch (ObjectType)
        {
            case ObjectType.Accessory:
            case ObjectType.Equipment:
                if (BodySlot is not BodySlot.Unknown)
                    return false;
                if (!EquipSlot.IsEquipment() && !EquipSlot.IsAccessory())
                    return false;
                if (SecondaryId != 0)
                    return false;

                break;
            case ObjectType.DemiHuman:
                if (BodySlot is not BodySlot.Unknown)
                    return false;
                if (!EquipSlot.IsEquipment() && !EquipSlot.IsAccessory())
                    return false;

                break;
            default:
                if (!Enum.IsDefined(BodySlot))
                    return false;
                if (EquipSlot is not EquipSlot.Unknown)
                    return false;
                if (!Enum.IsDefined(ObjectType))
                    return false;

                break;
        }

        return true;
    }

    public int CompareTo(ImcIdentifier other)
    {
        var o = ObjectType.CompareTo(other.ObjectType);
        if (o != 0)
            return o;

        var i = PrimaryId.Id.CompareTo(other.PrimaryId.Id);
        if (i != 0)
            return i;

        if (ObjectType is ObjectType.Equipment or ObjectType.Accessory)
        {
            var e = EquipSlot.CompareTo(other.EquipSlot);
            return e != 0 ? e : Variant.Id.CompareTo(other.Variant.Id);
        }

        if (ObjectType is ObjectType.DemiHuman)
        {
            var e = EquipSlot.CompareTo(other.EquipSlot);
            if (e != 0)
                return e;
        }

        var s = SecondaryId.Id.CompareTo(other.SecondaryId.Id);
        if (s != 0)
            return s;

        var b = BodySlot.CompareTo(other.BodySlot);
        return b != 0 ? b : Variant.Id.CompareTo(other.Variant.Id);
    }

    public static ImcIdentifier? FromJson(JObject jObj)
    {
        var objectType = jObj["PrimaryId"]?.ToObject<ObjectType>() ?? ObjectType.Unknown;
        var primaryId  = new PrimaryId(jObj["PrimaryId"]?.ToObject<ushort>() ?? 0);
        var variant    = jObj["Variant"]?.ToObject<ushort>() ?? 0;
        if (variant > byte.MaxValue)
            return null;

        ImcIdentifier ret;
        switch (objectType)
        {
            case ObjectType.Equipment:
            case ObjectType.Accessory:
            {
                var slot = jObj["EquipSlot"]?.ToObject<EquipSlot>() ?? EquipSlot.Unknown;
                ret = new ImcIdentifier(slot, primaryId, variant);
                break;
            }
            case ObjectType.DemiHuman:
            {
                var secondaryId = new SecondaryId(jObj["SecondaryId"]?.ToObject<ushort>() ?? 0);
                var slot        = jObj["Slot"]?.ToObject<EquipSlot>() ?? EquipSlot.Unknown;
                ret = new ImcIdentifier(primaryId, (Variant)variant, objectType, secondaryId, slot, BodySlot.Unknown);
                break;
            }

            case ObjectType.Monster:
            case ObjectType.Weapon:
            {
                var secondaryId = new SecondaryId(jObj["SecondaryId"]?.ToObject<ushort>() ?? 0);
                ret = new ImcIdentifier(primaryId, (Variant)variant, objectType, secondaryId, EquipSlot.Unknown, BodySlot.Body);
                break;
            }
            default: return null;
        }

        return ret.Validate() ? ret : null;
    }

    public JObject AddToJson(JObject jObj)
    {
        jObj["ObjectType"] = ObjectType.ToString();
        jObj["PrimaryId"]  = PrimaryId.Id;
        jObj["PrimaryId"]  = SecondaryId.Id;
        jObj["Variant"]    = Variant.Id;
        jObj["EquipSlot"]  = EquipSlot.ToString();
        jObj["BodySlot"]   = BodySlot.ToString();
        return jObj;
    }
}
