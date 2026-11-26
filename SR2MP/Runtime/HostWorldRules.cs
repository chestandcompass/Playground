using System.Reflection;
using HarmonyLib;
using SR2MP.Handlers.Internal;
using Il2CppMonomiPark.SlimeRancher.DataModel;
using Il2CppMonomiPark.SlimeRancher.Options;
using Il2CppMonomiPark.SlimeRancher.Persist;
using SR2MP.Packets.Internal;
using SR2MP.Packets.Utils;
using SR2MP.Shared.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Runtime;

internal static class HostWorldRules
{
    private static WorldRuleState? host, guest;
    private static bool applying;
    private static bool smokeGuest;
    private static float nextPoll;
    private static uint broadcastRevision;
    private static OptionsItemDefinition[]? activeDefinitions;
    internal static bool GuestLocked => (CoopSaveManager.Role == "guest" || (MenuSmokeTest.Enabled && smokeGuest)) && GameHelpers.inGame;
    internal static bool AllowDirectChange => applying || !GuestLocked;
    internal static void Reset() { host = guest = null; activeDefinitions = null; nextPoll = 0; broadcastRevision = 0; }

    // Use native asset identity, not translated captions or guessed setting IDs.
    private static int RuleIndex(OptionsItemDefinition definition)
    {
        var director = GameContext.Instance?.GameSettingsDirector;
        if (director == null) return -1;
        var toggle = definition.TryCast<ToggleOptionDefinition>();
        if (toggle != null && toggle.TargetBool != null)
        {
            if (toggle.TargetBool.Pointer == director.tarrEnabled.Pointer) return 0;
            if (toggle.TargetBool.Pointer == director.feralEnabled.Pointer) return 1;
        }
        var number = definition.TryCast<ScriptedFloatOptionDefinition>();
        if (number != null && number._targetValue != null && number._targetValue.Pointer == director.damageMultiplier.Pointer) return 2;
        var range = definition.TryCast<ScriptedValueRangeOptionDefinition>();
        if (range != null && range._targetValue != null && range._targetValue.Pointer == director.damageMultiplier.Pointer) return 2;
        var presets = definition.TryCast<ScriptedValuePresetOptionDefinition>();
        if (presets?._optionsPresets != null)
            foreach (var preset in presets._optionsPresets)
                if (preset._scriptedFloatSettings != null)
                    foreach (var value in preset._scriptedFloatSettings)
                        if (value._scriptedValue != null && value._scriptedValue.Pointer == director.damageMultiplier.Pointer) return 2;
        return -1;
    }

    private static OptionsItemDefinition[] Definitions()
    {
        if (activeDefinitions != null) return activeDefinitions;
        var found = new OptionsItemDefinition[WorldRuleState.Count];
        foreach (var definition in Resources.FindObjectsOfTypeAll<OptionsItemDefinition>())
        {
            var index = RuleIndex(definition);
            if (index >= 0) found[index] = definition;
        }
        if (found.Any(d => d == null))
            throw new InvalidOperationException("Native world rule definitions are unavailable: "
                + string.Join(",", found.Select(d => d == null ? "missing" : d.name)));
        return activeDefinitions = found;
    }

    internal static WorldRuleState Capture()
    {
        var definitions = Definitions();
        var values = definitions.Select(definition =>
        {
            var value = new OptionItemDataV01();
            GameState.GetGameSettingsModel().GetOptionModelFromDefinition(definition).Pull(value);
            return value.OptionValueKey;
        }).ToArray();
        var candidate = new WorldRuleState(host?.Revision ?? 1, values);
        if (host == null) host = candidate;
        else if (!host.SameValues(candidate)) host = new WorldRuleState(checked(host.Revision + 1), values);
        return host;
    }

    internal static void Apply(WorldRuleState rules)
    {
        if (!GuestLocked || !rules.IsNewerThan(guest)) return;
        var definitions = Definitions();
        applying = true;
        try
        {
            for (var i = 0; i < definitions.Length; i++)
            {
                var model = GameState.GetGameSettingsModel().GetOptionModelFromDefinition(definitions[i]);
                model.Push(new OptionItemDataV01 { PersistenceKey = definitions[i].ReferenceId, OptionValueKey = rules.Values[i] });
            }
            guest = rules;
            SrLogger.LogMessage($"Applied host world rules, revision {rules.Revision}.");
        }
        finally { applying = false; }
    }

    internal static bool CanEdit(OptionsItemDefinition definition)
        => applying || !GuestLocked || RuleIndex(definition) < 0;

    internal static void Update()
    {
        if (!Main.Server.IsRunning || !GameHelpers.inGame || Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + 0.5f;
        try
        {
            var current = Capture();
            if (current.Revision != broadcastRevision)
            {
                Main.Server.SendToAll(new WorldRulesPacket { Rules = current });
                broadcastRevision = current.Revision;
            }
        }
        catch (Exception ex)
        {
            nextPoll = Time.realtimeSinceStartup + 10;
            SrLogger.LogError($"World rules synchronization failed: {ex}");
        }
    }

    internal static void SmokeTest()
    {
        if (!MenuSmokeTest.Enabled || !Main.Server.IsRunning) throw new InvalidOperationException("Load an isolated test host world first.");
        var original = Capture();
        var definitions = Definitions();
        var director = GameContext.Instance.GameSettingsDirector;
        var tarr = director.TarrEnabled;
        var feral = director.FeralEnabled;
        var damage = director.DamageMultiplier;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("RULES SMOKE FAILED: " + message);
            SrLogger.LogMessage("RULES SMOKE PASS: " + message);
        }
        try
        {
            using (var writer = PacketWriter.Borrow())
            {
                WorldRulesPacket.Write(writer, original);
                using var reader = PacketReader.Borrow(writer.ToSpan().ToArray());
                var decoded = WorldRulesPacket.Read(reader);
                Check(decoded.Revision == original.Revision && decoded.SameValues(original), "world rules round-trip through the real network serializer");
            }
            foreach (var definition in definitions)
            {
                var model = GameState.GetGameSettingsModel().GetOptionModelFromDefinition(definition).Cast<PresetOptionsItemModel>();
                model.SelectValue((model.CurrentPresetIndex + 1) % model.OptionPresetCount);
                model.ApplySelectedValue();
                model.SaveSelectedValue();
            }
            var changed = Capture();
            Check(director.TarrEnabled != tarr && director.FeralEnabled != feral && director.DamageMultiplier != damage
                && changed.Revision > original.Revision, "host edits update all three gameplay rules and synchronized snapshot");
            smokeGuest = true;
            Check(definitions.All(d => !GameState.GetGameSettingsModel().GetOptionModelFromDefinition(d).ShouldEnable()), "all three guest rule rows are disabled");
            var before = director.TarrEnabled;
            var beforeFeral = director.FeralEnabled;
            var beforeDamage = director.DamageMultiplier;
            foreach (var definition in definitions)
            {
                var model = GameState.GetGameSettingsModel().GetOptionModelFromDefinition(definition).Cast<PresetOptionsItemModel>();
                model.SelectAndSaveValue((model.CurrentPresetIndex + 1) % model.OptionPresetCount);
                model.Reset();
                model.ApplySelectedValue();
            }
            director.TarrEnabled = !before;
            director.FeralEnabled = !beforeFeral;
            Check(director.TarrEnabled == before && director.FeralEnabled == beforeFeral && director.DamageMultiplier == beforeDamage,
                "guest UI and direct setting changes are blocked");
            var restored = new WorldRuleState(changed.Revision + 1, original.Values);
            Apply(restored);
            Check(director.TarrEnabled == tarr && director.FeralEnabled == feral && director.DamageMultiplier == damage, "host snapshot applies through native models and updates gameplay values");
            Apply(changed);
            Check(director.TarrEnabled == tarr, "late older snapshots cannot undo newer host rules");
            var tutorial = Resources.FindObjectsOfTypeAll<ToggleOptionDefinition>().First(d => d.TargetBool != null && d.TargetBool.Pointer == director.tutorialsEnabled.Pointer);
            Check(CanEdit(tutorial), "personal tutorial setting remains editable");
        }
        finally
        {
            smokeGuest = true;
            Apply(new WorldRuleState(uint.MaxValue, original.Values));
            smokeGuest = false;
            Reset();
        }
    }
}

[HarmonyPatch(typeof(GameSettingsDirector), nameof(GameSettingsDirector.TarrEnabled), MethodType.Setter)]
internal static class GuestTarrSetterGuard
{ internal static bool Prefix() => HostWorldRules.AllowDirectChange; }

[HarmonyPatch(typeof(GameSettingsDirector), nameof(GameSettingsDirector.FeralEnabled), MethodType.Setter)]
internal static class GuestFeralSetterGuard
{ internal static bool Prefix() => HostWorldRules.AllowDirectChange; }

[HarmonyPatch(typeof(OptionsItemModel), "get_DetailsText")]
internal static class GuestRuleDetails
{
    internal static void Postfix(OptionsItemModel __instance, ref UnityEngine.Localization.LocalizedString __result)
    {
        if (!HostWorldRules.CanEdit(__instance.optionsItemDefinition))
            __result = NativeMenu.Text(__result, "Controlled by the host");
    }
}

[HarmonyPatch(typeof(OptionsItemModel), nameof(OptionsItemModel.ShouldEnable))]
internal static class GuestRuleModelEnabled
{
    internal static void Postfix(OptionsItemModel __instance, ref bool __result)
    { if (!HostWorldRules.CanEdit(__instance.optionsItemDefinition)) __result = false; }
}

// Also guard actual mutation methods: Apply/Reset must not bypass a disabled row.
[HarmonyPatch]
internal static class GuestRuleEditGuard
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { "SelectValue", "ApplyValue", "Reset" })
            yield return AccessTools.Method(typeof(PresetOptionsItemModel), name);
        foreach (var name in new[] { "SetSelectedValue", "SetSavedValue" })
            yield return AccessTools.Method(typeof(ScriptedFloatOptionDefinition.Model), name);
    }
    internal static bool Prefix(OptionsItemModel __instance) => HostWorldRules.CanEdit(__instance.optionsItemDefinition);
}

[PacketHandler((byte)PacketType.WorldRules, HandlerType.Client)]
internal sealed class WorldRulesHandler : BasePacketHandler<WorldRulesPacket>
{
    protected override bool Handle(WorldRulesPacket packet, System.Net.IPEndPoint? endpoint)
    {
        HostWorldRules.Apply(packet.Rules);
        return false;
    }
}
