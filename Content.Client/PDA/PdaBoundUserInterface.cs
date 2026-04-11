using Content.Client.CartridgeLoader;
using Content.Shared._Stalker_EN.PDA;
using Content.Shared._Stalker_EN.PDA.Ringer;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PDA;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Player;

namespace Content.Client.PDA
{
    [UsedImplicitly]
    public sealed class PdaBoundUserInterface : CartridgeLoaderBoundUserInterface
    {
        private readonly PdaSystem _pdaSystem;
        private readonly ISharedPlayerManager _playerMgr; // stalker-en-changes

        [ViewVariables]
        private PdaMenu? _menu;

        public PdaBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _pdaSystem = EntMan.System<PdaSystem>();
            _playerMgr = IoCManager.Resolve<ISharedPlayerManager>(); // stalker-en-changes
        }

        protected override void Open()
        {
            base.Open();

            if (_menu == null)
                CreateMenu();
        }

        private void CreateMenu()
        {
            _menu = this.CreateWindowCenteredLeft<PdaMenu>();

            _menu.FlashLightToggleButton.OnToggled += _ =>
            {
                SendMessage(new PdaToggleFlashlightMessage());
            };

            _menu.EjectIdButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaIdSlotId));
            };

            _menu.EjectPenButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPenSlotId));
            };

            _menu.EjectPaiButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPaiSlotId));
            };

            _menu.ActivateMusicButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowMusicMessage());
            };

            _menu.AccessRingtoneButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowRingtoneMessage());
            };

            _menu.SilentModeButton.OnPressed += _ =>
            {
                SendMessage(new STPdaToggleSilentModeMessage());
            };

            _menu.ShowUplinkButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowUplinkMessage());
            };

            _menu.LockUplinkButton.OnPressed += _ =>
            {
                SendMessage(new PdaLockUplinkMessage());
            };

            // stalker-en-changes: PDA password settings
            _menu.SetPasswordButton.OnPressed += _ =>
            {
                SendMessage(new STPdaPasswordOpenSettingsMessage());
            };

            _menu.OnProgramDeactivated += DeactivateActiveCartridge;

            var borderColorComponent = GetBorderColorComponent();
            if (borderColorComponent == null)
                return;

            _menu.BorderColor = borderColorComponent.BorderColor;
            _menu.AccentHColor = borderColorComponent.AccentHColor;
            _menu.AccentVColor = borderColorComponent.AccentVColor;
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not PdaUpdateState updateState)
                return;

            if (_menu == null)
            {
                _pdaSystem.Log.Error("PDA state received before menu was created.");
                return;
            }

            _menu.UpdateState(updateState);

            // stalker-en-changes-start: always show password button — if the user can see the PDA UI,
            // they already passed the server's OnOpenAttempt auth check (owner, unlocked, or no lock).
            // Server-side IsAuthorized in OnOpenSettings provides the actual security gate.
            _menu.SetPasswordButton.Visible = true;
            // stalker-en-changes-end

            // Switch to home if there's no active program and we're on program view
            // This handles the case when server closes a program
            if (updateState.ActiveUI == null && _menu.GetCurrentView() == PdaMenu.ProgramContentViewIndex)
            {
                _menu.ToHomeScreen();
            }
        }

        protected override void AttachCartridgeUI(Control cartridgeUIFragment, string? title)
        {
            // Force the cartridge UI to expand to fill the entire ProgramView
            cartridgeUIFragment.VerticalExpand = true;
            cartridgeUIFragment.HorizontalExpand = true;

            _menu?.ProgramView.AddChild(cartridgeUIFragment);
            _menu?.ToProgramView();

        }

        protected override void DetachCartridgeUI(Control cartridgeUIFragment)
        {
            if (_menu is null)
                return;

            _menu.ProgramView.RemoveChild(cartridgeUIFragment);
        }

        protected override void UpdateAvailablePrograms(List<(EntityUid, CartridgeComponent)> programs)
        {
            _menu?.UpdateAvailablePrograms(programs, ActivateCartridge, InstallCartridge, UninstallCartridge);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _menu != null)
            {
                _menu.OnProgramDeactivated -= DeactivateActiveCartridge;
            }

            base.Dispose(disposing);
        }

        private PdaBorderColorComponent? GetBorderColorComponent()
        {
            return EntMan.GetComponentOrNull<PdaBorderColorComponent>(Owner);
        }
    }
}
