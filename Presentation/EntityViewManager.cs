using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using TheWaningBorder.Gameplay;

public class EntityViewManager : MonoBehaviour
{
    // =========================== PATHS ===========================
    [Header("CURSE — Buildings (Resources paths, omit .prefab)")]
    public string Curse_MainNode              = "Prefabs/Curse/Buildings/MainNode";
    public string Curse_Sub_Resource          = "Prefabs/Curse/Buildings/Sub_Resource";
    public string Curse_Sub_Turret            = "Prefabs/Curse/Buildings/Sub_Turret";
    public string Curse_Sub_Repair            = "Prefabs/Curse/Buildings/Sub_Repair";
    public string Curse_Sub_Enforcer          = "Prefabs/Curse/Buildings/Sub_Enforcer";
    public string Curse_Sub_Storm             = "Prefabs/Curse/Buildings/Sub_Storm";
    public string Curse_Sub_Obelisk           = "Prefabs/Curse/Buildings/Sub_Obelisk";
    public string Curse_Sub_Nexus             = "Prefabs/Curse/Buildings/Sub_Nexus";

    [Header("CURSE — Units")]
    public string Curse_Unit_Crystalling      = "Prefabs/Curse/Units/Crystalling";
    public string Curse_Unit_Veilstinger      = "Prefabs/Curse/Units/Veilstinger";
    public string Curse_Unit_Shardmender      = "Prefabs/Curse/Units/Shardmender";
    public string Curse_Unit_Godsplinter      = "Prefabs/Curse/Units/Godsplinter";
    public string Curse_Unit_Colossus         = "Prefabs/Curse/Units/Colossus";

    [Header("ERA 1 — Buildings")]
    public string E1_Hall                     = "Prefabs/Buildings/Hall";
    public string E1_GatherersHut             = "Prefabs/Buildings/GatherersHut";
    public string E1_Hut                      = "Prefabs/Buildings/Hut";
    public string E1_Barracks                 = "Prefabs/Buildings/Barracks";
    public string E1_Workshop                 = "Prefabs/Buildings/Workshop";
    public string E1_Depot                    = "Prefabs/Buildings/Depot";
    public string E1_Temple                   = "Prefabs/Buildings/TempleOfRidan";
    public string E1_Wall                     = "Prefabs/Buildings/WallSegment";

    [Header("ERA 1 — Units")]
    public string E1_Unit_Melee               = "Prefabs/Units/Swordsman";
    public string E1_Unit_Ranged              = "Prefabs/Units/Archer";
    public string E1_Unit_Siege               = "Prefabs/Units/Catapult";
    public string E1_Unit_Magic               = "Prefabs/Units/Acolyte";
    public string E1_Unit_Builder             = "Prefabs/Units/Builder"; 
    public string E1_Unit_Litharch             = "Prefabs/Units/Litharch"; 
    public string E1_Unit_Scout             = "Prefabs/Units/Scout"; 

    [Header("ERA 2 — RUNAI — Buildings")]
    public string E2_Runai_Capital            = "Prefabs/Runai/Buildings/ThessarasBazaar";
    public string E2_Runai_Outpost            = "Prefabs/Runai/Buildings/OutpostStation";
    public string E2_Runai_TradeHub           = "Prefabs/Runai/Buildings/TradeHub";
    public string E2_Runai_Vault              = "Prefabs/Runai/Buildings/VaultOfAlmierra";

    [Header("ERA 2 — RUNAI — Units (optional overrides)")]
    public string E2_Runai_Unit_Melee         = "";  // leave empty to use E1 defaults
    public string E2_Runai_Unit_Ranged        = "";
    public string E2_Runai_Unit_Siege         = "";
    public string E2_Runai_Unit_Magic         = "";

    [Header("ERA 2 — ALANTHOR — Buildings")]
    public string E2_Alanthor_Capital         = "Prefabs/Alanthor/Buildings/KingsCourt";
    public string E2_Alanthor_Wall            = "Prefabs/Alanthor/Buildings/CitadelWall";
    public string E2_Alanthor_Smelter         = "Prefabs/Alanthor/Buildings/Smelter";
    public string E2_Alanthor_Crucible        = "Prefabs/Alanthor/Buildings/ArchitectsCrucible";

    [Header("ERA 2 — ALANTHOR — Units (optional overrides)")]
    public string E2_Alanthor_Unit_Melee      = "";
    public string E2_Alanthor_Unit_Ranged     = "";
    public string E2_Alanthor_Unit_Siege      = "";
    public string E2_Alanthor_Unit_Magic      = "";

    [Header("ERA 2 — FERALDIS — Buildings")]
    public string E2_Feraldis_Capital         = "Prefabs/Feraldis/Buildings/FiendstoneKeep";
    public string E2_Feraldis_Hunting         = "Prefabs/Feraldis/Buildings/HuntingLodge";
    public string E2_Feraldis_Logging         = "Prefabs/Feraldis/Buildings/LoggingStation";
    public string E2_Feraldis_Foundry         = "Prefabs/Feraldis/Buildings/WarbrandFoundry";

    [Header("ERA 2 — FERALDIS — Units (optional overrides)")]
    public string E2_Feraldis_Unit_Melee      = "";
    public string E2_Feraldis_Unit_Ranged     = "";
    public string E2_Feraldis_Unit_Siege      = "";
    public string E2_Feraldis_Unit_Magic      = "";

    [Header("SECTS — Chapels (Small/Large) + Unique Building, Unique Unit")]
    // Runai
    public string Sect_StillFlame_ChapelS     = "Prefabs/Sects/Runai/StillFlame/Chapel_S";
    public string Sect_StillFlame_ChapelL     = "Prefabs/Sects/Runai/StillFlame/Chapel_L";
    public string Sect_StillFlame_UniqueBld   = "Prefabs/Sects/Runai/StillFlame/SanctumForge";
    public string Sect_StillFlame_Unit        = "Prefabs/Sects/Runai/StillFlame/Units/VigilantPaladin";

    public string Sect_QuietVault_ChapelS     = "Prefabs/Sects/Runai/QuietVault/Chapel_S";
    public string Sect_QuietVault_ChapelL     = "Prefabs/Sects/Runai/QuietVault/Chapel_L";
    public string Sect_QuietVault_UniqueBld   = "Prefabs/Sects/Runai/QuietVault/CatacombGate";
    public string Sect_QuietVault_Unit        = "Prefabs/Sects/Runai/QuietVault/Units/TombWarden";

    public string Sect_MirrorRite_ChapelS     = "Prefabs/Sects/Runai/MirrorRite/Chapel_S";
    public string Sect_MirrorRite_ChapelL     = "Prefabs/Sects/Runai/MirrorRite/Chapel_L";
    public string Sect_MirrorRite_UniqueBld   = "Prefabs/Sects/Runai/MirrorRite/HallOfReflection";
    public string Sect_MirrorRite_Unit        = "Prefabs/Sects/Runai/MirrorRite/Units/RiteSinger";

    public string Sect_ShardJudgment_ChapelS  = "Prefabs/Sects/Runai/ShardJudgment/Chapel_S";
    public string Sect_ShardJudgment_ChapelL  = "Prefabs/Sects/Runai/ShardJudgment/Chapel_L";
    public string Sect_ShardJudgment_UniqueBld= "Prefabs/Sects/Runai/ShardJudgment/TribunalForge";
    public string Sect_ShardJudgment_Unit     = "Prefabs/Sects/Runai/ShardJudgment/Units/ArbiterSentinel";

    // Alanthor
    public string Sect_Antiquity_ChapelS      = "Prefabs/Sects/Alanthor/Antiquity/Chapel_S";
    public string Sect_Antiquity_ChapelL      = "Prefabs/Sects/Alanthor/Antiquity/Chapel_L";
    public string Sect_Antiquity_UniqueBld    = "Prefabs/Sects/Alanthor/Antiquity/ArchivumCore";
    public string Sect_Antiquity_Unit         = "Prefabs/Sects/Alanthor/Antiquity/Units/MachinistAdept";

    public string Sect_Renewal_ChapelS        = "Prefabs/Sects/Alanthor/Renewal/Chapel_S";
    public string Sect_Renewal_ChapelL        = "Prefabs/Sects/Alanthor/Renewal/Chapel_L";
    public string Sect_Renewal_UniqueBld      = "Prefabs/Sects/Alanthor/Renewal/HospiceOfRenewal";
    public string Sect_Renewal_Unit           = "Prefabs/Sects/Alanthor/Renewal/Units/ScarGuard";

    public string Sect_LivingStone_ChapelS    = "Prefabs/Sects/Alanthor/LivingStone/Chapel_S";
    public string Sect_LivingStone_ChapelL    = "Prefabs/Sects/Alanthor/LivingStone/Chapel_L";
    public string Sect_LivingStone_UniqueBld  = "Prefabs/Sects/Alanthor/LivingStone/ArchitectsCrucible";
    public string Sect_LivingStone_Unit       = "Prefabs/Sects/Alanthor/LivingStone/Units/ColossusConstruct";

    public string Sect_VeiledMemory_ChapelS   = "Prefabs/Sects/Alanthor/VeiledMemory/Chapel_S";
    public string Sect_VeiledMemory_ChapelL   = "Prefabs/Sects/Alanthor/VeiledMemory/Chapel_L";
    public string Sect_VeiledMemory_UniqueBld = "Prefabs/Sects/Alanthor/VeiledMemory/HallOfEchoes";
    public string Sect_VeiledMemory_Unit      = "Prefabs/Sects/Alanthor/VeiledMemory/Units/ArchivistSavant";

    // Feraldis
    public string Sect_EmberAsh_ChapelS       = "Prefabs/Sects/Feraldis/EmberAsh/Chapel_S";
    public string Sect_EmberAsh_ChapelL       = "Prefabs/Sects/Feraldis/EmberAsh/Chapel_L";
    public string Sect_EmberAsh_UniqueBld     = "Prefabs/Sects/Feraldis/EmberAsh/WarbrandFoundry_Temple";
    public string Sect_EmberAsh_Unit          = "Prefabs/Sects/Feraldis/EmberAsh/Units/FlameBearBerserker";

    public string Sect_HollowBrand_ChapelS    = "Prefabs/Sects/Feraldis/HollowBrand/Chapel_S";
    public string Sect_HollowBrand_ChapelL    = "Prefabs/Sects/Feraldis/HollowBrand/Chapel_L";
    public string Sect_HollowBrand_UniqueBld  = "Prefabs/Sects/Feraldis/HollowBrand/ReaversDen";
    public string Sect_HollowBrand_Unit       = "Prefabs/Sects/Feraldis/HollowBrand/Units/BrandedHeretic";

    public string Sect_FlameChains_ChapelS    = "Prefabs/Sects/Feraldis/FlameChains/Chapel_S";
    public string Sect_FlameChains_ChapelL    = "Prefabs/Sects/Feraldis/FlameChains/Chapel_L";
    public string Sect_FlameChains_UniqueBld  = "Prefabs/Sects/Feraldis/FlameChains/CruciblePit";
    public string Sect_FlameChains_Unit       = "Prefabs/Sects/Feraldis/FlameChains/Units/ChainforgedGaoler";

    public string Sect_Unmaker_ChapelS        = "Prefabs/Sects/Feraldis/Unmaker/Chapel_S";
    public string Sect_Unmaker_ChapelL        = "Prefabs/Sects/Feraldis/Unmaker/Chapel_L";
    public string Sect_Unmaker_UniqueBld      = "Prefabs/Sects/Feraldis/Unmaker/AbyssalMonolith";
    public string Sect_Unmaker_Unit           = "Prefabs/Sects/Feraldis/Unmaker/Units/VoidReaper";

    // ===================== RUNTIME STATE =====================
    private World _world;
    private EntityManager _em;
    private readonly Dictionary<Entity, GameObject> _views = new();
    private readonly Dictionary<string, GameObject> _cache = new(); // path->prefab cache

    void OnEnable()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        if (_world != null && _world.IsCreated) _em = _world.EntityManager;
    }

    public bool TryGetView(Entity e, out GameObject go) => _views.TryGetValue(e, out go) && go != null;

    void Start() => RebuildAll();

    void OnDisable()
    {
        foreach (var kv in _views) if (kv.Value) Destroy(kv.Value);
        _views.Clear();
        _cache.Clear();
    }

    void RebuildAll()
    {
        if (_em == default) return;
        var q = _em.CreateEntityQuery(typeof(PresentationId), typeof(LocalTransform));
        using var ents = q.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var e in ents) EnsureViewFor(e);
    }

    void Update()
    {
        if (_em == default) return;

        var q   = _em.CreateEntityQuery(typeof(PresentationId), typeof(LocalTransform));
        using var es  = q.ToEntityArray(Unity.Collections.Allocator.Temp);
        using var xfs = q.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < es.Length; i++)
        {
            var e  = es[i];
            var xf = xfs[i];
            var go = EnsureViewFor(e);
            if (go != null)
            {
                go.transform.position   = (Vector3)xf.Position;
                go.transform.rotation   = (Quaternion)xf.Rotation;
                go.transform.localScale = new Vector3(xf.Scale, xf.Scale, xf.Scale);
            }
        }

        // Cleanup views whose entities no longer exist/present
        var toRemove = new List<Entity>();
        foreach (var kv in _views)
        {
            var e = kv.Key;
            if (!_em.Exists(e) || !_em.HasComponent<PresentationId>(e))
            {
                if (kv.Value) Destroy(kv.Value);
                toRemove.Add(e);
            }
        }
        for (int i = 0; i < toRemove.Count; i++) _views.Remove(toRemove[i]);
    }

    // ===================== VIEW CREATION =====================
    GameObject EnsureViewFor(Entity e)
    {
        if (!_em.Exists(e) || !_em.HasComponent<PresentationId>(e)) return null;
        if (_views.TryGetValue(e, out var exist) && exist) return exist;

        // Resolve prefab key & unit class
        bool isBuilding;
        UnitClass uClass;
        var key = ResolveKeyFor(e, out isBuilding, out uClass);

        GameObject prefab = LoadPrefabByKey(key);

        var go = prefab != null
            ? Instantiate(prefab)
            : (isBuilding ? CreateFallbackBuilding() : CreateFallbackUnit(uClass));

        // Determine faction
        var fac = _em.HasComponent<FactionTag>(e) ? _em.GetComponentData<FactionTag>(e).Value : Faction.Blue;

        // Optional faction tint (fallbacks only)
        TintIfFallback(go, fac, prefab == null, isBuilding, uClass);

        // NEW: tint Hall roof stripes for real Hall prefab (non-fallback)
        if (prefab != null && isBuilding && key == "e1/bld/hall")
        {
            TintHallRoof(go, fac);
        }
        if (prefab != null && isBuilding && key == "e1/bld/hut")
        {
            TintHutRoof(go, fac);
        }


        // Place
        var xf = _em.GetComponentData<LocalTransform>(e);
        go.transform.position = (Vector3)xf.Position;
        go.transform.rotation = (Quaternion)xf.Rotation;
        go.transform.localScale = Vector3.one;

        _views[e] = go;
        return go;
    }

    // ===================== KEY RESOLUTION ====================
    enum Culture { None=0, Runai=1, Alanthor=2, Feraldis=3 }

    string ResolveKeyFor(Entity e, out bool isBuilding, out UnitClass uClass)
    {
        isBuilding = false; uClass = UnitClass.Melee;

        bool hasB = _em.HasComponent<BuildingTag>(e);
        bool hasU = _em.HasComponent<UnitTag>(e);
        Culture culture = Culture.None;
        if (_em.HasComponent<FactionProgress>(e)) culture = (Culture)_em.GetComponentData<FactionProgress>(e).Culture;

        // Sect detection (optional tags)
        int sectId = -1;

        if (hasB)
        {
            isBuilding = true;

            var bt = _em.GetComponentData<BuildingTag>(e);
            if (bt.IsBase == 1)
            {
                if (culture == Culture.Runai)    return "e2/runai/bld/capital";
                if (culture == Culture.Alanthor) return "e2/alanthor/bld/capital";
                if (culture == Culture.Feraldis) return "e2/feraldis/bld/capital";
                return "e1/bld/hall";
            }

            if (_em.HasComponent<GathererHutTag>(e)) return "e1/bld/gatherer";
            if (_em.HasComponent<HutTag>(e)) return "e1/bld/hut";
            if (_em.HasComponent<BarracksTag>(e))    return "e1/bld/barracks";
            if (_em.HasComponent<WorkshopTag>(e))    return "e1/bld/workshop";
            if (_em.HasComponent<DepotTag>(e))       return "e1/bld/depot";
            if (_em.HasComponent<TempleTag>(e))      return "e1/bld/temple";
            if (_em.HasComponent<WallTag>(e))
                return (culture == Culture.Alanthor) ? "e2/alanthor/bld/wall" : "e1/bld/wall";

            if (culture == Culture.Runai)
            {
                if (_em.HasComponent<OutpostTag>(e))  return "e2/runai/bld/outpost";
                if (_em.HasComponent<TradeHubTag>(e)) return "e2/runai/bld/tradehub";
                if (_em.HasComponent<VaultTag>(e))    return "e2/runai/bld/vault";
            }
            else if (culture == Culture.Alanthor)
            {
                if (_em.HasComponent<SmelterTag>(e))  return "e2/alanthor/bld/smelter";
                if (_em.HasComponent<CrucibleTag>(e)) return "e2/alanthor/bld/crucible";
            }
            else if (culture == Culture.Feraldis)
            {
                if (_em.HasComponent<HuntingLodgeTag>(e))   return "e2/feraldis/bld/hunting";
                if (_em.HasComponent<LoggingStationTag>(e)) return "e2/feraldis/bld/logging";
                if (_em.HasComponent<WarbrandFoundryTag>(e))return "e2/feraldis/bld/foundry";
            }

            if (sectId >= 0)
            {
                if (_em.HasComponent<ChapelSmallTag>(e))       return $"sect/{sectId}/chapel_s";
                if (_em.HasComponent<ChapelLargeTag>(e))       return $"sect/{sectId}/chapel_l";
                if (_em.HasComponent<SectUniqueBuildingTag>(e))return $"sect/{sectId}/unique_bld";
            }

            return "fallback/building";
        }

        if (hasU)
        {
            var ut = _em.GetComponentData<UnitTag>(e);
            uClass = ut.Class;

            if (sectId >= 0 && _em.HasComponent<SectUniqueUnitTag>(e))
                return $"sect/{sectId}/unique_unit";

            if (culture == Culture.Runai)
            {
                switch (ut.Class)
                {
                    case UnitClass.Melee:  if (!string.IsNullOrEmpty(E2_Runai_Unit_Melee))  return "e2/runai/unit/melee";  break;
                    case UnitClass.Ranged: if (!string.IsNullOrEmpty(E2_Runai_Unit_Ranged)) return "e2/runai/unit/ranged"; break;
                    case UnitClass.Siege:  if (!string.IsNullOrEmpty(E2_Runai_Unit_Siege))  return "e2/runai/unit/siege";  break;
                    case UnitClass.Magic:  if (!string.IsNullOrEmpty(E2_Runai_Unit_Magic))  return "e2/runai/unit/magic";  break;
                }
            }
            else if (culture == Culture.Alanthor)
            {
                switch (ut.Class)
                {
                    case UnitClass.Melee:  if (!string.IsNullOrEmpty(E2_Alanthor_Unit_Melee))  return "e2/alanthor/unit/melee";  break;
                    case UnitClass.Ranged: if (!string.IsNullOrEmpty(E2_Alanthor_Unit_Ranged)) return "e2/alanthor/unit/ranged"; break;
                    case UnitClass.Siege:  if (!string.IsNullOrEmpty(E2_Alanthor_Unit_Siege))  return "e2/alanthor/unit/siege";  break;
                    case UnitClass.Magic:  if (!string.IsNullOrEmpty(E2_Alanthor_Unit_Magic))  return "e2/alanthor/unit/magic";  break;
                }
            }
            else if (culture == Culture.Feraldis)
            {
                switch (ut.Class)
                {
                    case UnitClass.Melee:  if (!string.IsNullOrEmpty(E2_Feraldis_Unit_Melee))  return "e2/feraldis/unit/melee";  break;
                    case UnitClass.Ranged: if (!string.IsNullOrEmpty(E2_Feraldis_Unit_Ranged)) return "e2/feraldis/unit/ranged"; break;
                    case UnitClass.Siege:  if (!string.IsNullOrEmpty(E2_Feraldis_Unit_Siege))  return "e2/feraldis/unit/siege";  break;
                    case UnitClass.Magic:  if (!string.IsNullOrEmpty(E2_Feraldis_Unit_Magic))  return "e2/feraldis/unit/magic";  break;
                }
            }

            switch (ut.Class)
            {
                case UnitClass.Melee:   return "e1/unit/melee";
                case UnitClass.Ranged:  return "e1/unit/ranged";
                case UnitClass.Siege:   return "e1/unit/siege";
                case UnitClass.Magic:   return "e1/unit/magic";
                case UnitClass.Economy: return "e1/unit/builder";
                case UnitClass.Scout:   return "e1/unit/scout";
                default:                return "fallback/unit";
            }
        }

        return "fallback/unknown";
    }

    // ===================== PREFAB LOADING ====================
    GameObject LoadPrefabByKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        string path = key switch
        {
            "curse/bld/main"            => Curse_MainNode,
            "curse/bld/sub/resource"    => Curse_Sub_Resource,
            "curse/bld/sub/turret"      => Curse_Sub_Turret,
            "curse/bld/sub/repair"      => Curse_Sub_Repair,
            "curse/bld/sub/enforcer"    => Curse_Sub_Enforcer,
            "curse/bld/sub/storm"       => Curse_Sub_Storm,
            "curse/bld/sub/obelisk"     => Curse_Sub_Obelisk,
            "curse/bld/sub/nexus"       => Curse_Sub_Nexus,

            "curse/unit/crystalling"    => Curse_Unit_Crystalling,
            "curse/unit/veilstinger"    => Curse_Unit_Veilstinger,
            "curse/unit/shardmender"    => Curse_Unit_Shardmender,
            "curse/unit/godsplinter"    => Curse_Unit_Godsplinter,
            "curse/unit/colossus"       => Curse_Unit_Colossus,

            "e1/bld/hall"               => E1_Hall,
            "e1/bld/hut"                => E1_Hut,
            "e1/bld/gatherer"           => E1_GatherersHut,
            "e1/bld/barracks"           => E1_Barracks,
            "e1/bld/workshop"           => E1_Workshop,
            "e1/bld/depot"              => E1_Depot,
            "e1/bld/temple"             => E1_Temple,
            "e1/bld/wall"               => E1_Wall,

            "e1/unit/melee"             => E1_Unit_Melee,
            "e1/unit/ranged"            => E1_Unit_Ranged,
            "e1/unit/siege"             => E1_Unit_Siege,
            "e1/unit/magic"             => E1_Unit_Magic,
            "e1/unit/builder"           => E1_Unit_Builder,
            "e1/unit/litharch"           => E1_Unit_Litharch,
            "e1/unit/scout"           => E1_Unit_Scout,

            "e2/runai/bld/capital"      => E2_Runai_Capital,
            "e2/runai/bld/outpost"      => E2_Runai_Outpost,
            "e2/runai/bld/tradehub"     => E2_Runai_TradeHub,
            "e2/runai/bld/vault"        => E2_Runai_Vault,

            "e2/runai/unit/melee"       => E2_Runai_Unit_Melee,
            "e2/runai/unit/ranged"      => E2_Runai_Unit_Ranged,
            "e2/runai/unit/siege"       => E2_Runai_Unit_Siege,
            "e2/runai/unit/magic"       => E2_Runai_Unit_Magic,

            "e2/alanthor/bld/capital"   => E2_Alanthor_Capital,
            "e2/alanthor/bld/wall"      => E2_Alanthor_Wall,
            "e2/alanthor/bld/smelter"   => E2_Alanthor_Smelter,
            "e2/alanthor/bld/crucible"  => E2_Alanthor_Crucible,

            "e2/alanthor/unit/melee"    => E2_Alanthor_Unit_Melee,
            "e2/alanthor/unit/ranged"   => E2_Alanthor_Unit_Ranged,
            "e2/alanthor/unit/siege"    => E2_Alanthor_Unit_Siege,
            "e2/alanthor/unit/magic"    => E2_Alanthor_Unit_Magic,

            "e2/feraldis/bld/capital"   => E2_Feraldis_Capital,
            "e2/feraldis/bld/hunting"   => E2_Feraldis_Hunting,
            "e2/feraldis/bld/logging"   => E2_Feraldis_Logging,
            "e2/feraldis/bld/foundry"   => E2_Feraldis_Foundry,

            "e2/feraldis/unit/melee"    => E2_Feraldis_Unit_Melee,
            "e2/feraldis/unit/ranged"   => E2_Feraldis_Unit_Ranged,
            "e2/feraldis/unit/siege"    => E2_Feraldis_Unit_Siege,
            "e2/feraldis/unit/magic"    => E2_Feraldis_Unit_Magic,

            var _                       => null
        };

        if (path == null && key.StartsWith("sect/"))
        {
            var parts = key.Split('/');
            if (parts.Length >= 3 && int.TryParse(parts[1], out int sid))
            {
                string tail = string.Join("/", parts, 2, parts.Length - 2);
                path = ResolveSectPath(sid, tail);
            }
        }

        if (string.IsNullOrEmpty(path)) return null;

        if (_cache.TryGetValue(path, out var cached) && cached != null)
            return cached;

        var prefab = Resources.Load<GameObject>(path);
        if (prefab != null) _cache[path] = prefab;
        return prefab;
    }

    string ResolveSectPath(int sid, string tail)
    {
        switch (sid)
        {
            case 0:  return tail switch { "chapel_s"=>Sect_StillFlame_ChapelS,  "chapel_l"=>Sect_StillFlame_ChapelL,  "unique_bld"=>Sect_StillFlame_UniqueBld,  "unique_unit"=>Sect_StillFlame_Unit,  _=>null };
            case 1:  return tail switch { "chapel_s"=>Sect_QuietVault_ChapelS,  "chapel_l"=>Sect_QuietVault_ChapelL,  "unique_bld"=>Sect_QuietVault_UniqueBld,  "unique_unit"=>Sect_QuietVault_Unit,  _=>null };
            case 2:  return tail switch { "chapel_s"=>Sect_MirrorRite_ChapelS,  "chapel_l"=>Sect_MirrorRite_ChapelL,  "unique_bld"=>Sect_MirrorRite_UniqueBld,  "unique_unit"=>Sect_MirrorRite_Unit,  _=>null };
            case 3:  return tail switch { "chapel_s"=>Sect_ShardJudgment_ChapelS,"chapel_l"=>Sect_ShardJudgment_ChapelL,"unique_bld"=>Sect_ShardJudgment_UniqueBld,"unique_unit"=>Sect_ShardJudgment_Unit,_=>null };

            case 4:  return tail switch { "chapel_s"=>Sect_Antiquity_ChapelS,   "chapel_l"=>Sect_Antiquity_ChapelL,   "unique_bld"=>Sect_Antiquity_UniqueBld,   "unique_unit"=>Sect_Antiquity_Unit,   _=>null };
            case 5:  return tail switch { "chapel_s"=>Sect_Renewal_ChapelS,     "chapel_l"=>Sect_Renewal_ChapelL,     "unique_bld"=>Sect_Renewal_UniqueBld,     "unique_unit"=>Sect_Renewal_Unit,     _=>null };
            case 6:  return tail switch { "chapel_s"=>Sect_LivingStone_ChapelS, "chapel_l"=>Sect_LivingStone_ChapelL, "unique_bld"=>Sect_LivingStone_UniqueBld, "unique_unit"=>Sect_LivingStone_Unit, _=>null };
            case 7:  return tail switch { "chapel_s"=>Sect_VeiledMemory_ChapelS,"chapel_l"=>Sect_VeiledMemory_ChapelL,"unique_bld"=>Sect_VeiledMemory_UniqueBld,"unique_unit"=>Sect_VeiledMemory_Unit,_=>null };

            case 8:  return tail switch { "chapel_s"=>Sect_EmberAsh_ChapelS,    "chapel_l"=>Sect_EmberAsh_ChapelL,    "unique_bld"=>Sect_EmberAsh_UniqueBld,    "unique_unit"=>Sect_EmberAsh_Unit,    _=>null };
            case 9:  return tail switch { "chapel_s"=>Sect_HollowBrand_ChapelS, "chapel_l"=>Sect_HollowBrand_ChapelL, "unique_bld"=>Sect_HollowBrand_UniqueBld, "unique_unit"=>Sect_HollowBrand_Unit, _=>null };
            case 10: return tail switch { "chapel_s"=>Sect_FlameChains_ChapelS, "chapel_l"=>Sect_FlameChains_ChapelL, "unique_bld"=>Sect_FlameChains_UniqueBld, "unique_unit"=>Sect_FlameChains_Unit, _=>null };
            case 11: return tail switch { "chapel_s"=>Sect_Unmaker_ChapelS,     "chapel_l"=>Sect_Unmaker_ChapelL,     "unique_bld"=>Sect_Unmaker_UniqueBld,     "unique_unit"=>Sect_Unmaker_Unit,     _=>null };
            default: return null;
        }
    }

    // ===================== FALLBACKS & TINT ==================
    GameObject CreateFallbackBuilding()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Bld_Fallback_Cube";
        go.transform.localScale = new Vector3(6f, 4f, 6f);
        return go;
    }

    GameObject CreateFallbackUnit(UnitClass uClass)
    {
        GameObject go;
        switch (uClass)
        {
            case UnitClass.Melee:
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Unit_Fallback_Melee";
                go.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
                break;
            case UnitClass.Ranged:
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Unit_Fallback_Ranged";
                go.transform.localScale = new Vector3(0.6f, 1.4f, 0.6f);
                break;
            case UnitClass.Siege:
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Unit_Fallback_Siege";
                go.transform.localScale = new Vector3(1.2f, 0.7f, 1.2f);
                break;
            case UnitClass.Magic:
            default:
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Unit_Fallback_Magic";
                go.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                break;
        }
        return go;
    }

    void TintIfFallback(GameObject go, Faction fac, bool isFallback, bool isBuilding, UnitClass uc)
    {
        if (!isFallback || !go) return;

        var color = FactionColor(fac);
        var rend = go.GetComponentInChildren<Renderer>();
        if (!rend) return;

        var mpb = new MaterialPropertyBlock();
        rend.GetPropertyBlock(mpb);
        if (rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_BaseColor"))
            mpb.SetColor("_BaseColor", color);
        if (rend.sharedMaterial == null || rend.sharedMaterial.HasProperty("_Color"))
            mpb.SetColor("_Color", color);
        rend.SetPropertyBlock(mpb);
    }

    // NEW: tint the Hall roof "StripeColor" based on faction
    void TintHallRoof(GameObject go, Faction fac)
    {
        if (!go) return;

        // Assumes the Hall prefab has a child named "Roof"
        var roof = go.transform.Find("Roof");
        if (roof == null)
        {
            Debug.LogWarning($"TintHallRoof: could not find 'Roof' child on {go.name}");
            return;
        }

        if (!roof.TryGetComponent<Renderer>(out var rend))
        {
            Debug.LogWarning($"TintHallRoof: 'Roof' has no Renderer on {go.name}");
            return;
        }

        // Get per-instance material so we don't modify the shared asset
        var mat = rend.material;
        var color = FactionColor(fac);

        if (mat.HasProperty("StripeColor"))
        {
            mat.SetColor("StripeColor", color);
        }
        else if (mat.HasProperty("_StripeColor"))
        {
            mat.SetColor("_StripeColor", color);
        }
        else
        {
            Debug.LogWarning($"TintHallRoof: material '{mat.name}' has no StripeColor/_StripeColor");
        }
    }
        void TintHutRoof(GameObject go, Faction fac)
    {
        if (!go) return;

        // Assumes the Hut prefab has a child named "Roof"
        var roof = go.transform.Find("Plane");
        if (roof == null)
        {
            Debug.LogWarning($"TintHutRoof: could not find 'Roof' child on {go.name}");
            return;
        }

        if (!roof.TryGetComponent<Renderer>(out var rend))
        {
            Debug.LogWarning($"TintHutRoof: 'Roof' has no Renderer on {go.name}");
            return;
        }

        // Get per-instance material so we don't modify the shared asset
        var mat = rend.material;
        var color = FactionColor(fac);

        if (mat.HasProperty("StripeColor"))
        {
            mat.SetColor("StripeColor", color);
        }
        else if (mat.HasProperty("_StripeColor"))
        {
            mat.SetColor("_StripeColor", color);
        }
        else
        {
            Debug.LogWarning($"TintHutRoof: material '{mat.name}' has no StripeColor/_StripeColor");
        }
    }


    static Color FactionColor(Faction f) => f switch
    {
        Faction.Blue   => new Color(0.2f, 0.6f, 1f),
        Faction.Red    => new Color(1f, 0.3f, 0.25f),
        Faction.Green  => new Color(0.3f, 0.9f, 0.3f),
        Faction.Yellow => new Color(1f, 0.9f, 0.2f),
        Faction.Purple => new Color(0.7f, 0.4f, 0.9f),
        Faction.Orange => new Color(1f, 0.6f, 0.2f),
        Faction.Teal   => new Color(0.2f, 0.9f, 0.8f),
        Faction.White  => new Color(0.85f, 0.85f, 0.85f),
        _              => Color.gray
    };
}
