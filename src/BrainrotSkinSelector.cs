using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Brainrot
{
    public sealed class BrainrotSkinSelectorVM : ViewModel
    {
        private readonly List<BrainrotHeroSkins.Skin> skins;
        private Hero hero;
        private int revision = -1;
        private bool suppress;
        private SelectorVM<SelectorItemVM> selector;
        private bool visible;

        internal BrainrotSkinSelectorVM() : this(BrainrotHeroSkins.Available()) { }

        internal BrainrotSkinSelectorVM(List<BrainrotHeroSkins.Skin> available)
        {
            skins = available;
        }

        [DataSourceProperty]
        public SelectorVM<SelectorItemVM> SkinSelector
        {
            get => selector;
            private set { selector = value; OnPropertyChanged(nameof(SkinSelector)); }
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => visible;
            private set { if (visible == value) return; visible = value; OnPropertyChanged(nameof(IsVisible)); }
        }

        internal void Sync(Hero selected)
        {
            if (hero == selected && revision == BrainrotHeroSkins.Revision) return;
            hero = selected;
            IsVisible = BrainrotHeroSkins.Eligible(hero);
            string key = BrainrotHeroSkins.Selection(hero);
            int index = skins.FindIndex(s => s.Id == key) + 1;
            suppress = true;
            try
            {
                SkinSelector = new SelectorVM<SelectorItemVM>(
                    new[] { "Default appearance" }.Concat(skins.Select(s => s.Name)), index, OnSelected);
            }
            finally { suppress = false; }
            revision = BrainrotHeroSkins.Revision;
        }

        private void OnSelected(SelectorVM<SelectorItemVM> selection)
        {
            if (suppress || !IsVisible || selection.SelectedIndex < 0 || selection.SelectedIndex > skins.Count) return;
            string key = selection.SelectedIndex == 0 ? null : skins[selection.SelectedIndex - 1].Id;
            BrainrotHeroSkins.Choose(hero, key);
            revision = BrainrotHeroSkins.Revision;
        }
    }

    internal static class BrainrotSkinSelector
    {
        private const string Owner = "Brainrot.HeroSkinSelector";
        private static Harmony harmony;
        private static FieldInfo dataSource, layer, currentCharacter, inventoryLogic;
        private static Session active;
        private static readonly HashSet<string> Errors = new HashSet<string>();

        private sealed class Session
        {
            internal object Screen;
            internal object Logic;
            internal object NativeVM;
            internal GauntletLayer Layer;
            internal GauntletMovieIdentifier Movie;
            internal BrainrotSkinSelectorVM VM;
        }

        internal static void Install()
        {
            if (harmony != null || !BrainrotPartyPreview.IsInstalled) return;
            Type screen = AccessTools.TypeByName("SandBox.GauntletUI.GauntletInventoryScreen");
            if (screen == null) throw new TypeLoadException("Inventory screen not found.");
            dataSource = AccessTools.Field(screen, "_dataSource");
            layer = AccessTools.Field(screen, "_gauntletLayer");
            currentCharacter = AccessTools.Field(dataSource.FieldType, "_currentCharacter");
            inventoryLogic = AccessTools.Field(dataSource.FieldType, "_inventoryLogic");
            if (currentCharacter == null || inventoryLogic == null || layer == null) throw new MissingFieldException("Inventory bindings changed.");
            harmony = new Harmony(Owner);
            try
            {
                harmony.Patch(AccessTools.DeclaredMethod(screen, "OnFrameTick"), postfix: Patch(nameof(Tick)));
                harmony.Patch(AccessTools.DeclaredMethod(screen, "OnFinalize"), prefix: Patch(nameof(FinalizeScreen)));
                harmony.Patch(AccessTools.DeclaredMethod(inventoryLogic.FieldType, "DoneLogic"), postfix: Patch(nameof(Done)));
                harmony.Patch(AccessTools.DeclaredMethod(inventoryLogic.FieldType, "Reset"), postfix: Patch(nameof(Reset)));
                BrainrotPreviewLog.Write("Stage 7: inventory skin selector hooks installed.");
            }
            catch { Uninstall(); throw; }
        }

        private static HarmonyMethod Patch(string name) => new HarmonyMethod(typeof(BrainrotSkinSelector), name);

        private static void Tick(object __instance)
        {
            try
            {
                if (!ReferenceEquals(ScreenManager.TopScreen, __instance)) return;
                object native = dataSource.GetValue(__instance);
                var gauntlet = layer.GetValue(__instance) as GauntletLayer;
                if (native == null || gauntlet == null) return;
                if (active == null || !ReferenceEquals(active.Screen, __instance))
                {
                    Close();
                    BrainrotHeroSkins.Begin();
                    active = new Session { Screen = __instance, NativeVM = native, Logic = inventoryLogic.GetValue(native), Layer = gauntlet };
                    active.VM = new BrainrotSkinSelectorVM();
                    active.VM.Sync((currentCharacter.GetValue(native) as CharacterObject)?.HeroObject);
                    active.Movie = gauntlet.LoadMovie("BrainrotSkinSelector", active.VM);
                    BrainrotPreviewLog.Write("Stage 7: skin selector movie loaded.");
                }
                active.VM.Sync((currentCharacter.GetValue(native) as CharacterObject)?.HeroObject);
            }
            catch (Exception error) { Report(error); }
        }

        private static void Done(object __instance, bool __result)
        {
            if (active == null || !ReferenceEquals(__instance, active.Logic) || !__result) return;
            try { BrainrotHeroSkins.Commit(); }
            catch (Exception error)
            {
                Report(error);
                InformationManager.DisplayMessage(new InformationMessage("Brainrot could not save skin choices. Previous choices were kept."));
            }
        }

        private static void Reset(object __instance)
        {
            if (active != null && ReferenceEquals(__instance, active.Logic)) BrainrotHeroSkins.Reset();
        }

        private static void FinalizeScreen(object __instance)
        {
            if (active != null && ReferenceEquals(active.Screen, __instance)) Close();
        }

        private static void Close()
        {
            var closing = active;
            active = null;
            BrainrotHeroSkins.End();
            if (closing == null) return;
            try
            {
                if (closing.Movie != null) closing.Layer.ReleaseMovie(closing.Movie);
                closing.VM?.OnFinalize();
            }
            catch (Exception error) { Report(error); }
        }

        internal static void Uninstall()
        {
            Close();
            harmony?.UnpatchAll(Owner);
            harmony = null;
        }

        private static void Report(Exception error)
        {
            if (Errors.Add(error.ToString())) BrainrotPreviewLog.Write("Stage 7 selector: " + error);
        }
    }
}
