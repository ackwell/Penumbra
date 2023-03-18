using System;
using System.IO;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using ImGuiNET;
using OtterGui;
using OtterGui.Widgets;
using Penumbra.Api;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Files;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Resolver;
using Penumbra.Interop.Structs;
using Penumbra.Mods;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.Util;
using static OtterGui.Raii.ImRaii;
using CharacterBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;
using CharacterUtility = Penumbra.Interop.CharacterUtility;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using ResidentResourceManager = Penumbra.Interop.Services.ResidentResourceManager;

namespace Penumbra.UI.Tabs;

public class DebugTab : ITab
{
    private readonly StartTracker            _timer;
    private readonly PerformanceTracker      _performance;
    private readonly Configuration           _config;
    private readonly ModCollection.Manager   _collectionManager;
    private readonly Mod.Manager             _modManager;
    private readonly ValidityChecker         _validityChecker;
    private readonly HttpApi                 _httpApi;
    private readonly PathResolver            _pathResolver;
    private readonly ActorService            _actorService;
    private readonly DalamudServices         _dalamud;
    private readonly StainService            _stains;
    private readonly CharacterUtility        _characterUtility;
    private readonly ResidentResourceManager _residentResources;
    private readonly ResourceManagerService  _resourceManager;
    private readonly PenumbraIpcProviders    _ipc;

    public DebugTab(StartTracker timer, PerformanceTracker performance, Configuration config, ModCollection.Manager collectionManager,
        ValidityChecker validityChecker, Mod.Manager modManager, HttpApi httpApi, PathResolver pathResolver, ActorService actorService,
        DalamudServices dalamud, StainService stains, CharacterUtility characterUtility, ResidentResourceManager residentResources,
        ResourceManagerService resourceManager, PenumbraIpcProviders ipc)
    {
        _timer             = timer;
        _performance       = performance;
        _config            = config;
        _collectionManager = collectionManager;
        _validityChecker   = validityChecker;
        _modManager        = modManager;
        _httpApi           = httpApi;
        _pathResolver      = pathResolver;
        _actorService      = actorService;
        _dalamud           = dalamud;
        _stains            = stains;
        _characterUtility  = characterUtility;
        _residentResources = residentResources;
        _resourceManager   = resourceManager;
        _ipc               = ipc;
    }

    public ReadOnlySpan<byte> Label
        => "Debug"u8;

    public bool IsVisible
        => _config.DebugMode;

#if DEBUG
    private const string DebugVersionString = "(Debug)";
#else
    private const string DebugVersionString = "(Release)";
#endif

    public void DrawContent()
    {
        using var child = Child("##DebugTab", -Vector2.One);
        if (!child)
            return;

        DrawDebugTabGeneral();
        DrawPerformanceTab();
        ImGui.NewLine();
        DrawPathResolverDebug();
        ImGui.NewLine();
        DrawActorsDebug();
        ImGui.NewLine();
        DrawDebugCharacterUtility();
        ImGui.NewLine();
        DrawStainTemplates();
        ImGui.NewLine();
        DrawDebugTabMetaLists();
        ImGui.NewLine();
        DrawDebugResidentResources();
        ImGui.NewLine();
        DrawResourceProblems();
        ImGui.NewLine();
        DrawPlayerModelInfo();
        ImGui.NewLine();
        DrawDebugTabIpc();
        ImGui.NewLine();
    }

    /// <summary> Draw general information about mod and collection state. </summary>
    private void DrawDebugTabGeneral()
    {
        if (!ImGui.CollapsingHeader("General"))
            return;

        using var table = Table("##DebugGeneralTable", 2, ImGuiTableFlags.SizingFixedFit,
            new Vector2(-1, ImGui.GetTextLineHeightWithSpacing() * 1));
        if (!table)
            return;

        PrintValue("Penumbra Version",                 $"{_validityChecker.Version} {DebugVersionString}");
        PrintValue("Git Commit Hash",                  _validityChecker.CommitHash);
        PrintValue(TutorialService.SelectedCollection, _collectionManager.Current.Name);
        PrintValue("    has Cache",                    _collectionManager.Current.HasCache.ToString());
        PrintValue(TutorialService.DefaultCollection,  _collectionManager.Default.Name);
        PrintValue("    has Cache",                    _collectionManager.Default.HasCache.ToString());
        PrintValue("Mod Manager BasePath",             _modManager.BasePath.Name);
        PrintValue("Mod Manager BasePath-Full",        _modManager.BasePath.FullName);
        PrintValue("Mod Manager BasePath IsRooted",    Path.IsPathRooted(_config.ModDirectory).ToString());
        PrintValue("Mod Manager BasePath Exists",      Directory.Exists(_modManager.BasePath.FullName).ToString());
        PrintValue("Mod Manager Valid",                _modManager.Valid.ToString());
        PrintValue("Path Resolver Enabled",            _pathResolver.Enabled.ToString());
        PrintValue("Web Server Enabled",               _httpApi.Enabled.ToString());
    }

    private void DrawPerformanceTab()
    {
        ImGui.NewLine();
        if (ImGui.CollapsingHeader("Performance"))
            return;

        using (var start = TreeNode("Startup Performance", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (start)
            {
                _timer.Draw("##startTimer", TimingExtensions.ToName);
                ImGui.NewLine();
            }
        }

        _performance.Draw("##performance", "Enable Runtime Performance Tracking", TimingExtensions.ToName);
    }

    private unsafe void DrawActorsDebug()
    {
        if (!ImGui.CollapsingHeader("Actors"))
            return;

        using var table = Table("##actors", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        void DrawSpecial(string name, ActorIdentifier id)
        {
            if (!id.IsValid)
                return;

            ImGuiUtil.DrawTableColumn(name);
            ImGuiUtil.DrawTableColumn(string.Empty);
            ImGuiUtil.DrawTableColumn(_actorService.AwaitedService.ToString(id));
            ImGuiUtil.DrawTableColumn(string.Empty);
        }

        DrawSpecial("Current Player",  _actorService.AwaitedService.GetCurrentPlayer());
        DrawSpecial("Current Inspect", _actorService.AwaitedService.GetInspectPlayer());
        DrawSpecial("Current Card",    _actorService.AwaitedService.GetCardPlayer());
        DrawSpecial("Current Glamour", _actorService.AwaitedService.GetGlamourPlayer());

        foreach (var obj in DalamudServices.SObjects)
        {
            ImGuiUtil.DrawTableColumn($"{((GameObject*)obj.Address)->ObjectIndex}");
            ImGuiUtil.DrawTableColumn($"0x{obj.Address:X}");
            var identifier = _actorService.AwaitedService.FromObject(obj, false, true, false);
            ImGuiUtil.DrawTableColumn(_actorService.AwaitedService.ToString(identifier));
            var id = obj.ObjectKind == ObjectKind.BattleNpc ? $"{identifier.DataId} | {obj.DataId}" : identifier.DataId.ToString();
            ImGuiUtil.DrawTableColumn(id);
        }
    }

    /// <summary>
    /// Draw information about which draw objects correspond to which game objects
    /// and which paths are due to be loaded by which collection.
    /// </summary>
    private unsafe void DrawPathResolverDebug()
    {
        if (!ImGui.CollapsingHeader("Path Resolver"))
            return;

        ImGui.TextUnformatted(
            $"Last Game Object: 0x{_pathResolver.LastGameObject:X} ({_pathResolver.LastGameObjectData.ModCollection.Name})");
        using (var drawTree = TreeNode("Draw Object to Object"))
        {
            if (drawTree)
            {
                using var table = Table("###DrawObjectResolverTable", 5, ImGuiTableFlags.SizingFixedFit);
                if (table)
                    foreach (var (ptr, (c, idx)) in _pathResolver.DrawObjectMap)
                    {
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(ptr.ToString("X"));
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(idx.ToString());
                        ImGui.TableNextColumn();
                        var obj = (GameObject*)_dalamud.Objects.GetObjectAddress(idx);
                        var (address, name) =
                            obj != null ? ($"0x{(ulong)obj:X}", new ByteString(obj->Name).ToString()) : ("NULL", "NULL");
                        ImGui.TextUnformatted(address);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(name);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(c.ModCollection.Name);
                    }
            }
        }

        using (var pathTree = TreeNode("Path Collections"))
        {
            if (pathTree)
            {
                using var table = Table("###PathCollectionResolverTable", 3, ImGuiTableFlags.SizingFixedFit);
                if (table)
                    foreach (var (path, collection) in _pathResolver.PathCollections)
                    {
                        ImGui.TableNextColumn();
                        ImGuiNative.igTextUnformatted(path.Path, path.Path + path.Length);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(collection.ModCollection.Name);
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(collection.AssociatedGameObject.ToString("X"));
                    }
            }
        }

        using (var resourceTree = TreeNode("Subfile Collections"))
        {
            if (resourceTree)
            {
                using var table = Table("###ResourceCollectionResolverTable", 3, ImGuiTableFlags.SizingFixedFit);
                if (table)
                {
                    ImGuiUtil.DrawTableColumn("Current Mtrl Data");
                    ImGuiUtil.DrawTableColumn(_pathResolver.CurrentMtrlData.ModCollection.Name);
                    ImGuiUtil.DrawTableColumn($"0x{_pathResolver.CurrentMtrlData.AssociatedGameObject:X}");

                    ImGuiUtil.DrawTableColumn("Current Avfx Data");
                    ImGuiUtil.DrawTableColumn(_pathResolver.CurrentAvfxData.ModCollection.Name);
                    ImGuiUtil.DrawTableColumn($"0x{_pathResolver.CurrentAvfxData.AssociatedGameObject:X}");

                    ImGuiUtil.DrawTableColumn("Current Resources");
                    ImGuiUtil.DrawTableColumn(_pathResolver.SubfileCount.ToString());
                    ImGui.TableNextColumn();

                    foreach (var (resource, resolve) in _pathResolver.ResourceCollections)
                    {
                        ImGuiUtil.DrawTableColumn($"0x{resource:X}");
                        ImGuiUtil.DrawTableColumn(resolve.ModCollection.Name);
                        ImGuiUtil.DrawTableColumn($"0x{resolve.AssociatedGameObject:X}");
                    }
                }
            }
        }

        using (var identifiedTree = TreeNode("Identified Collections"))
        {
            if (identifiedTree)
            {
                using var table = Table("##PathCollectionsIdentifiedTable", 3, ImGuiTableFlags.SizingFixedFit);
                if (table)
                    foreach (var (address, identifier, collection) in PathResolver.IdentifiedCache)
                    {
                        ImGuiUtil.DrawTableColumn($"0x{address:X}");
                        ImGuiUtil.DrawTableColumn(identifier.ToString());
                        ImGuiUtil.DrawTableColumn(collection.Name);
                    }
            }
        }

        using (var cutsceneTree = TreeNode("Cutscene Actors"))
        {
            if (cutsceneTree)
            {
                using var table = Table("###PCutsceneResolverTable", 2, ImGuiTableFlags.SizingFixedFit);
                if (table)
                    foreach (var (idx, actor) in _pathResolver.CutsceneActors)
                    {
                        ImGuiUtil.DrawTableColumn($"Cutscene Actor {idx}");
                        ImGuiUtil.DrawTableColumn(actor.Name.ToString());
                    }
            }
        }

        using (var groupTree = TreeNode("Group"))
        {
            if (groupTree)
            {
                using var table = Table("###PGroupTable", 2, ImGuiTableFlags.SizingFixedFit);
                if (table)
                {
                    ImGuiUtil.DrawTableColumn("Group Members");
                    ImGuiUtil.DrawTableColumn(GroupManager.Instance()->MemberCount.ToString());
                    for (var i = 0; i < 8; ++i)
                    {
                        ImGuiUtil.DrawTableColumn($"Member #{i}");
                        var member = GroupManager.Instance()->GetPartyMemberByIndex(i);
                        ImGuiUtil.DrawTableColumn(member == null ? "NULL" : new ByteString(member->Name).ToString());
                    }
                }
            }
        }

        using (var bannerTree = TreeNode("Party Banner"))
        {
            if (bannerTree)
            {
                var agent = &AgentBannerParty.Instance()->AgentBannerInterface;
                if (agent->Data == null)
                    agent = &AgentBannerMIP.Instance()->AgentBannerInterface;

                if (agent->Data != null)
                {
                    using var table = Table("###PBannerTable", 2, ImGuiTableFlags.SizingFixedFit);
                    if (table)
                        for (var i = 0; i < 8; ++i)
                        {
                            var c = agent->Character(i);
                            ImGuiUtil.DrawTableColumn($"Character {i}");
                            var name = c->Name1.ToString();
                            ImGuiUtil.DrawTableColumn(name.Length == 0 ? "NULL" : $"{name} ({c->WorldId})");
                        }
                }
                else
                {
                    ImGui.TextUnformatted("INACTIVE");
                }
            }
        }
    }

    private void DrawStainTemplates()
    {
        if (!ImGui.CollapsingHeader("Staining Templates"))
            return;

        foreach (var (key, data) in _stains.StmFile.Entries)
        {
            using var tree = TreeNode($"Template {key}");
            if (!tree)
                continue;

            using var table = Table("##table", 5, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg);
            if (!table)
                continue;

            for (var i = 0; i < StmFile.StainingTemplateEntry.NumElements; ++i)
            {
                var (r, g, b) = data.DiffuseEntries[i];
                ImGuiUtil.DrawTableColumn($"{r:F6} | {g:F6} | {b:F6}");

                (r, g, b) = data.SpecularEntries[i];
                ImGuiUtil.DrawTableColumn($"{r:F6} | {g:F6} | {b:F6}");

                (r, g, b) = data.EmissiveEntries[i];
                ImGuiUtil.DrawTableColumn($"{r:F6} | {g:F6} | {b:F6}");

                var a = data.SpecularPowerEntries[i];
                ImGuiUtil.DrawTableColumn($"{a:F6}");

                a = data.GlossEntries[i];
                ImGuiUtil.DrawTableColumn($"{a:F6}");
            }
        }
    }

    /// <summary>
    /// Draw information about the character utility class from SE,
    /// displaying all files, their sizes, the default files and the default sizes.
    /// </summary>
    private unsafe void DrawDebugCharacterUtility()
    {
        if (!ImGui.CollapsingHeader("Character Utility"))
            return;

        using var table = Table("##CharacterUtility", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        for (var i = 0; i < CharacterUtility.RelevantIndices.Length; ++i)
        {
            var idx      = CharacterUtility.RelevantIndices[i];
            var intern   = new CharacterUtility.InternalIndex(i);
            var resource = _characterUtility.Address->Resource(idx);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"0x{(ulong)resource:X}");
            ImGui.TableNextColumn();
            UiHelpers.Text(resource);
            ImGui.TableNextColumn();
            ImGui.Selectable($"0x{resource->GetData().Data:X}");
            if (ImGui.IsItemClicked())
            {
                var (data, length) = resource->GetData();
                if (data != nint.Zero && length > 0)
                    ImGui.SetClipboardText(string.Join("\n",
                        new ReadOnlySpan<byte>((byte*)data, length).ToArray().Select(b => b.ToString("X2"))));
            }

            ImGuiUtil.HoverTooltip("Click to copy bytes to clipboard.");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{resource->GetData().Length}");
            ImGui.TableNextColumn();
            ImGui.Selectable($"0x{_characterUtility.DefaultResource(intern).Address:X}");
            if (ImGui.IsItemClicked())
                ImGui.SetClipboardText(string.Join("\n",
                    new ReadOnlySpan<byte>((byte*)_characterUtility.DefaultResource(intern).Address,
                        _characterUtility.DefaultResource(intern).Size).ToArray().Select(b => b.ToString("X2"))));

            ImGuiUtil.HoverTooltip("Click to copy bytes to clipboard.");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{_characterUtility.DefaultResource(intern).Size}");
        }
    }

    private void DrawDebugTabMetaLists()
    {
        if (!ImGui.CollapsingHeader("Metadata Changes"))
            return;

        using var table = Table("##DebugMetaTable", 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        foreach (var list in _characterUtility.Lists)
        {
            ImGuiUtil.DrawTableColumn(list.GlobalIndex.ToString());
            ImGuiUtil.DrawTableColumn(list.Entries.Count.ToString());
            ImGuiUtil.DrawTableColumn(string.Join(", ", list.Entries.Select(e => $"0x{e.Data:X}")));
        }
    }

    /// <summary> Draw information about the resident resource files. </summary>
    private unsafe void DrawDebugResidentResources()
    {
        if (!ImGui.CollapsingHeader("Resident Resources"))
            return;

        if (_residentResources.Address == null || _residentResources.Address->NumResources == 0)
            return;

        using var table = Table("##ResidentResources", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
            -Vector2.UnitX);
        if (!table)
            return;

        for (var i = 0; i < _residentResources.Address->NumResources; ++i)
        {
            var resource = _residentResources.Address->ResourceList[i];
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"0x{(ulong)resource:X}");
            ImGui.TableNextColumn();
            UiHelpers.Text(resource);
        }
    }

    /// <summary> Draw information about the models, materials and resources currently loaded by the local player. </summary>
    private unsafe void DrawPlayerModelInfo()
    {
        var player = _dalamud.ClientState.LocalPlayer;
        var name   = player?.Name.ToString() ?? "NULL";
        if (!ImGui.CollapsingHeader($"Player Model Info: {name}##Draw") || player == null)
            return;

        var model = (CharacterBase*)((Character*)player.Address)->GameObject.GetDrawObject();
        if (model == null)
            return;

        using (var t1 = Table("##table", 2, ImGuiTableFlags.SizingFixedFit))
        {
            if (t1)
            {
                ImGuiUtil.DrawTableColumn("Flags");
                ImGuiUtil.DrawTableColumn($"{model->UnkFlags_01:X2}");
                ImGuiUtil.DrawTableColumn("Has Model In Slot Loaded");
                ImGuiUtil.DrawTableColumn($"{model->HasModelInSlotLoaded:X8}");
                ImGuiUtil.DrawTableColumn("Has Model Files In Slot Loaded");
                ImGuiUtil.DrawTableColumn($"{model->HasModelFilesInSlotLoaded:X8}");
            }
        }

        using var table = Table($"##{name}DrawTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        ImGui.TableNextColumn();
        ImGui.TableHeader("Slot");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Imc Ptr");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Imc File");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Model Ptr");
        ImGui.TableNextColumn();
        ImGui.TableHeader("Model File");

        for (var i = 0; i < model->SlotCount; ++i)
        {
            var imc = (ResourceHandle*)model->IMCArray[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"Slot {i}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(imc == null ? "NULL" : $"0x{(ulong)imc:X}");
            ImGui.TableNextColumn();
            if (imc != null)
                UiHelpers.Text(imc);

            var mdl = (RenderModel*)model->ModelArray[i];
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(mdl == null ? "NULL" : $"0x{(ulong)mdl:X}");
            if (mdl == null || mdl->ResourceHandle == null || mdl->ResourceHandle->Category != ResourceCategory.Chara)
                continue;

            ImGui.TableNextColumn();
            {
                UiHelpers.Text(mdl->ResourceHandle);
            }
        }
    }

    /// <summary> Draw resources with unusual reference count. </summary>
    private unsafe void DrawResourceProblems()
    {
        var header = ImGui.CollapsingHeader("Resource Problems");
        ImGuiUtil.HoverTooltip("Draw resources with unusually high reference count to detect overflows.");
        if (!header)
            return;

        using var table = Table("##ProblemsTable", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        _resourceManager.IterateResources((_, r) =>
        {
            if (r->RefCount < 10000)
                return;

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(r->Category.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(r->FileType.ToString("X"));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(r->Id.ToString("X"));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(((ulong)r).ToString("X"));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(r->RefCount.ToString());
            ImGui.TableNextColumn();
            ref var name = ref r->FileName;
            if (name.Capacity > 15)
                UiHelpers.Text(name.BufferPtr, (int)name.Length);
            else
                fixed (byte* ptr = name.Buffer)
                {
                    UiHelpers.Text(ptr, (int)name.Length);
                }
        });
    }


    /// <summary> Draw information about IPC options and availability. </summary>
    private void DrawDebugTabIpc()
    {
        if (!ImGui.CollapsingHeader("IPC"))
        {
            _ipc.Tester.UnsubscribeEvents();
            return;
        }

        _ipc.Tester.Draw();
    }

    /// <summary> Helper to print a property and its value in a 2-column table. </summary>
    private static void PrintValue(string name, string value)
    {
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(name);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(value);
    }
}
