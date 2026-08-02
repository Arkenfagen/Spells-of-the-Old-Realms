using SOTOR;
using SOTOR.Extensions;
using SOTOR.GameManagers;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace SOTOR.AbilitySystem
{
    public class AbilityManagerMissionLogic : MissionLogic
    {
        private AbilityModeState _currentState = AbilityModeState.Off;
        private AbilityComponent _abilityComponent;
        private AbilityHUDMissionView _abilityView;
        private GameKey _quickCastMenuKey;
        private bool _hasInitializedForMainAgent;
        private bool _loggedGatingBlock;

        private bool _battleResultReached;
        private int _timeRequestID = 1338;

        private const float PostCastSuppressDuration = 0.3f;
        private float _postCastSuppressUntil;

        private bool _shouldSheathWeapon;
        private bool _shouldWieldWeapon;
        private EquipmentIndex _mainHand = EquipmentIndex.None;
        private EquipmentIndex _offHand = EquipmentIndex.None;

        private bool _shouldPlayIdleCastStanceAnim;
        private ActionIndexCache? _idleCastAnimation;
        private bool _loggedAnimResolve;

        private const string CastStanceParticleName = "psys_spellcasting_stance";
        private ParticleSystem[] _castStancePsys;
        private GameEntity[] _castStanceEntities;
        private Agent _castStanceAgent;

        public AbilityModeState CurrentState => _currentState;

        public bool ShouldSuppressCombatActions =>
            _currentState == AbilityModeState.QuickMenuSelection
            || _currentState == AbilityModeState.Targeting
            || _currentState == AbilityModeState.Casting
            || (Mission != null && Mission.CurrentTime < _postCastSuppressUntil);

        private static bool _missionPatchesApplied;

        public override void OnRemoveBehavior()
        {
            RemoveMissionOnlyPatches();
            base.OnRemoveBehavior();
        }

        public override void OnMissionResultReady(MissionResult missionResult)
        {
            base.OnMissionResultReady(missionResult);

            if (missionResult == null || (!missionResult.PlayerDefeated && !missionResult.PlayerVictory))
            {
                return;
            }

            _battleResultReached = true;

            if (_currentState != AbilityModeState.Off)
            {
                DisableAbilityMode();
            }

            var agents = Mission?.AllAgents;
            if (agents != null)
            {
                foreach (var agent in (System.Collections.Generic.List<Agent>)agents)
                {
                    agent?.GetComponent<StatusEffects.StatusEffectComponent>()?.Dispose();
                }
            }

            SotorSpellDamageLog.FlushExpired(Mission);
            SotorSpellDamageLog.Reset();

            SotorLog.Info($"OnMissionResultReady: magic stopped (victory={missionResult.PlayerVictory} defeat={missionResult.PlayerDefeated}); status effects disposed.");
        }

        private static void ApplyMissionOnlyPatches()
        {
            if (_missionPatchesApplied)
            {
                return;
            }

            try
            {
                SubModule.HarmonyInstance?.PatchCategory(
                    typeof(SubModule).Assembly, SotorPatchCategories.MissionOnly);
                _missionPatchesApplied = true;
                SotorLog.Info("Mission-only Harmony patches applied (combat-actions lockout live).");
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"Failed to apply mission-only patches: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void RemoveMissionOnlyPatches()
        {
            if (!_missionPatchesApplied)
            {
                return;
            }

            try
            {
                SubModule.HarmonyInstance?.UnpatchCategory(
                    typeof(SubModule).Assembly, SotorPatchCategories.MissionOnly);
                SotorLog.Info("Mission-only Harmony patches removed (off the campaign map / doll).");
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"Failed to remove mission-only patches: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _missionPatchesApplied = false;
            }
        }

        public override void EarlyStart()
        {
            base.EarlyStart();

            ApplyMissionOnlyPatches();

            _abilityView = Mission.GetMissionBehavior<AbilityHUDMissionView>();

            _quickCastMenuKey = null;
            try
            {
                GameKeyContext sotorCategory = null;
                foreach (var cat in HotKeyManager.GetAllCategories())
                {
                    if (cat is SotorGameKeyContext)
                    {
                        sotorCategory = cat;
                        break;
                    }
                }

                _quickCastMenuKey = sotorCategory?.GetGameKey(SotorGameKeyContext.QuickCastSelectionMenu);
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"AbilityManager EarlyStart: hotkey category lookup failed ({ex.GetType().Name}); using Q fallback.");
            }

            SotorLog.Info(
                $"AbilityManager EarlyStart. castingMission={IsCastingMission()} quickCastKey={(_quickCastMenuKey != null ? "ok" : "missing (Q fallback)")}");
            EnsureMainAgentAbilityComponent();
        }

        private bool _tickCrashLogged;

        private void Safe(string where, System.Action body, bool throttle = false)
        {
            try
            {
                body();
            }
            catch (System.Exception ex)
            {
                if (throttle && _tickCrashLogged)
                {
                    return;
                }

                _tickCrashLogged = true;
                SotorLog.Error($"EXCEPTION in {where}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public override void OnPreMissionTick(float dt)
        {
            Safe("OnPreMissionTick", () =>
            {
                if (Agent.Main != null)
                {
                    EnsureMainAgentAbilityComponent();
                    _abilityComponent = Agent.Main.GetComponent<AbilityComponent>();
                    if (!_hasInitializedForMainAgent)
                    {
                        ApplyBattleStartWindsPerks();
                    }
                    _hasInitializedForMainAgent = true;
                }

                if (!_hasInitializedForMainAgent)
                {
                    return;
                }

                if (_shouldSheathWeapon || _shouldWieldWeapon)
                {
                    UpdateWieldedItems();
                }

                if (IsAbilityModeAvailableForMainAgent())
                {
                    _loggedGatingBlock = false;
                    HandleInput();
                    HandleAnimations();
                }
                else if (Agent.Main != null && !_loggedGatingBlock)
                {
                    LogAbilityGatingOnce();
                }
            }, throttle: true);
        }

        private void LogAbilityGatingOnce()
        {
            _loggedGatingBlock = true;
            var main = Agent.Main;
            SotorLog.Debug(
                $"Ability input gated. active={main.IsActive()} mouse={ScreenManager.GetMouseVisibility()} " +
                $"castingMission={IsCastingMission()} photo={Mission.IsInPhotoMode} orders={Mission.IsOrderMenuOpen} " +
                $"mode={(int)Mission.Mode} component={(_abilityComponent != null)} " +
                $"current={_abilityComponent?.CurrentAbility?.StringID ?? "none"} abilityUser={main.IsAbilityUser()}");
        }

        public override void OnAgentCreated(Agent agent)
        {
            Safe("OnAgentCreated", () =>
            {
                if (!ShouldAttachAbilityComponent(agent))
                {
                    return;
                }

                if (agent.GetComponent<AbilityComponent>() == null)
                {
                    agent.AddComponent(new AbilityComponent(agent));
                }
            });
        }

        private void EnsureMainAgentAbilityComponent()
        {
            var main = Agent.Main;
            if (main == null || !AbilityMissionModeHelper.IsBattleAbilityContext(Mission))
            {
                return;
            }

            var hero = main.GetHero();
            if (hero != null)
            {
                hero.AddAttribute("AbilityUser");
                hero.AddAttribute("SpellCaster");

                var info = hero.GetExtendedInfo();
                if (info != null)
                {
                    foreach (var loreId in info.AcquiredLores ?? new System.Collections.Generic.List<string>())
                    {
                        foreach (var template in AbilityFactory.GetTemplatesByLore(loreId))
                        {
                            if (info.HasSpell(template.StringID) && !hero.HasAbility(template.StringID))
                            {
                                hero.AddAbility(template.StringID);
                            }
                        }
                    }
                }
            }

            if (!ShouldAttachAbilityComponent(main))
            {
                return;
            }

            if (main.GetComponent<AbilityComponent>() == null)
            {
                main.AddComponent(new AbilityComponent(main));
                SotorLog.Info("Attached AbilityComponent to main agent (fallback).");
            }
        }

        private bool ShouldAttachAbilityComponent(Agent agent)
        {
            if (!AbilityMissionModeHelper.IsBattleAbilityContext(Mission))
            {
                return false;
            }

            if (agent.IsAbilityUser())
            {
                return agent.GetSelectedAbilities().Count > 0;
            }

            var hero = agent.GetHero();
            return agent.IsMainAgent && hero != null && hero.GetExtendedInfo()?.SelectedAbilities.Count > 0;
        }

        private bool IsQuickCastMenuKeyPressed()
        {
            if (_quickCastMenuKey != null)
            {
                if (Input.IsKeyPressed(_quickCastMenuKey.KeyboardKey.InputKey)
                    || Input.IsKeyPressed(_quickCastMenuKey.ControllerKey.InputKey))
                {
                    return true;
                }
            }

            return Input.IsKeyPressed(InputKey.Q);
        }

        private bool IsQuickCastMenuKeyDown()
        {
            if (_quickCastMenuKey != null)
            {
                if (Input.IsKeyDown(_quickCastMenuKey.KeyboardKey.InputKey)
                    || Input.IsKeyDown(_quickCastMenuKey.ControllerKey.InputKey))
                {
                    return true;
                }
            }

            return Input.IsKeyDown(InputKey.Q);
        }

        private void HandleInput()
        {
            if (Input.IsKeyDown(InputKey.LeftAlt))
            {
                return;
            }

            if ((_currentState == AbilityModeState.QuickMenuSelection || _currentState == AbilityModeState.Targeting)
                && Input.IsKeyPressed(InputKey.RightMouseButton))
            {
                DisableAbilityMode();
                return;
            }

            switch (_currentState)
            {
                case AbilityModeState.Off:
                    if (IsQuickCastMenuKeyPressed())
                    {
                        EnableQuickSelectionMenuMode();
                    }

                    break;

                case AbilityModeState.QuickMenuSelection:
                    if (IsQuickCastMenuKeyDown())
                    {
                        break;
                    }

                    var ability = _abilityComponent?.CurrentAbility;
                    if (ability == null)
                    {
                        DisableAbilityMode();
                        break;
                    }

                    if (ability.IsDisabled(Agent.Main, out var disabledReason))
                    {
                        SotorLog.Info($"Q release: '{ability.StringID}' disabled: {disabledReason?.ToString() ?? "unknown"}");
                        DisableAbilityMode();
                        break;
                    }

                    if (ability.IsThrownWeapon)
                    {
                        TryQuickCastCurrentAbility();

                        DisableAbilityMode(suppressWeaponRestore: true);
                        break;
                    }

                    if (ability.RequiresTargeting)
                    {
                        EnableTargetingMode();
                    }
                    else
                    {
                        TryQuickCastCurrentAbility();
                        DisableAbilityMode();
                    }

                    break;

                case AbilityModeState.Targeting:

                    var aimCrosshair = _abilityComponent?.CurrentAbility?.Crosshair;
                    aimCrosshair?.Tick();
                    if (Input.IsKeyPressed(InputKey.LeftMouseButton))
                    {
                        if (IsCrosshairReadyToFire(aimCrosshair))
                        {
                            TryQuickCastCurrentAbility();
                            DisableAbilityMode();
                        }

                    }

                    break;
            }
        }

        private void EnableTargetingMode()
        {

            CacheWieldedItemsForRestore();
            _shouldSheathWeapon = true;

            _shouldPlayIdleCastStanceAnim = true;
            _loggedAnimResolve = false;

            try
            {
                SetUpCastStanceParticles();
                EnableCastStanceParticles(true);
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"Cast-stance particles failed: {ex.Message}");
            }

            _currentState = AbilityModeState.Targeting;

            _abilityView?.MissionScreen?.UnregisterRadialMenuObject(_abilityView);

            var missionScreen = _abilityView?.MissionScreen;
            var ability = _abilityComponent?.CurrentAbility;
            if (ability != null && Agent.Main != null && missionScreen != null)
            {

                var crosshair = AbilityFactory.InitializeCrosshair(ability.Template, Mission, missionScreen, Agent.Main);
                ability.SetCrosshair(crosshair);
                crosshair?.Show();
            }

            SlowDownTime(true);
            SotorLog.Info($"Targeting mode ON for '{ability?.StringID}' (crosshair={ability?.Crosshair?.GetType().Name ?? "none"}; left-click fires, right-click cancels).");
        }

        private static bool IsCrosshairReadyToFire(Crosshairs.AbilityCrosshair crosshair)
        {
            if (crosshair == null || !crosshair.IsVisible)
            {
                return false;
            }

            if (crosshair is Crosshairs.SingleTargetCrosshair single)
            {
                return single.IsTargetLocked;
            }

            return true;
        }

        private void EnableQuickSelectionMenuMode()
        {

            SotorThrownJavelinMissionLogic.Instance?.CancelReadiedJavelin("new cast (cast key)");

            _currentState = AbilityModeState.QuickMenuSelection;
            var missionScreen = _abilityView?.MissionScreen;
            missionScreen?.RegisterRadialMenuObject(_abilityView);
            _abilityView?.OnQuickMenuOpened();
            SlowDownTime(true);
            SotorLog.Info("Radial menu opened (Q).");
        }

        private void TryQuickCastCurrentAbility()
        {
            Safe("TryQuickCastCurrentAbility", () =>
            {
                var main = Agent.Main;
                if (main == null || _abilityComponent == null)
                {
                    SotorLog.Debug($"Q release: cannot cast (mainAgent={(main != null)} component={(_abilityComponent != null)}).");
                    return;
                }

                var ability = _abilityComponent.CurrentAbility;
                if (ability == null)
                {
                    SotorLog.Debug("Q release: no current ability to cast.");
                    return;
                }

                _abilityComponent.LastCastWasQuickCast = true;

                SotorTarget preferred = null;
                var locked = (ability.Crosshair as Crosshairs.SingleTargetCrosshair)?.CachedTarget;
                if (locked != null)
                {
                    preferred = new SotorTarget { Agent = locked };
                }

                SotorLog.Debug($"Q release: attempting cast '{ability.StringID}' (prefab='{ability.Template?.ParticleEffectPrefab}', lockedTarget='{locked?.Name ?? "none"}').");
                if (!ability.TryCast(main, preferred, out var failureReason))
                {
                    SotorLog.Info($"Q quick-cast '{ability.StringID}' failed: {failureReason?.ToString() ?? "unknown"}");
                }
                else
                {

                    if (Mission != null && !ability.IsThrownWeapon)
                    {
                        _postCastSuppressUntil = Mission.CurrentTime + PostCastSuppressDuration;
                    }

                    if (!ability.IsThrownWeapon)
                    {
                        PlayCastReleaseAnimation(main, ability.Template?.AnimationActionName);
                    }

                    SotorLog.Info($"Q quick-cast '{ability.StringID}' succeeded.");
                }
            });
        }

        private void DisableAbilityMode(bool suppressWeaponRestore = false)
        {

            var main = Agent.Main;
            var curMain = main != null ? main.GetPrimaryWieldedItemIndex() : EquipmentIndex.None;
            var curOff = main != null ? main.GetOffhandWieldedItemIndex() : EquipmentIndex.None;
            bool mainChanged = _mainHand != EquipmentIndex.None && curMain != _mainHand;
            bool offChanged = _offHand != EquipmentIndex.None && curOff != _offHand;
            _shouldWieldWeapon = !suppressWeaponRestore && (mainChanged || offChanged);
            if (suppressWeaponRestore)
            {
                _mainHand = EquipmentIndex.None;
                _offHand = EquipmentIndex.None;
            }
            _shouldSheathWeapon = false;
            _shouldPlayIdleCastStanceAnim = false;

            try
            {
                EnableCastStanceParticles(false);
                RemoveCastStanceParticles();
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"Cast-stance particle teardown failed: {ex.Message}");
            }

            _currentState = AbilityModeState.Off;
            if (_abilityComponent != null)
            {
                _abilityComponent.LastCastWasQuickCast = false;
            }

            SlowDownTime(false);
            _abilityView?.MissionScreen?.UnregisterRadialMenuObject(_abilityView);

            var crosshair = _abilityComponent?.CurrentAbility?.Crosshair;
            if (crosshair != null)
            {
                crosshair.Hide();
                crosshair.Dispose();
                _abilityComponent.CurrentAbility.SetCrosshair(null);
            }

            SotorLog.Info("Radial menu closed.");
        }

        private ActionIndexCache IdleCastAnimation
        {
            get
            {
                if (!_idleCastAnimation.HasValue)
                {
                    _idleCastAnimation = ActionIndexCache.Create("act_spellcasting_idle");
                }

                return _idleCastAnimation.Value;
            }
        }

        private void HandleAnimations()
        {
            if (_currentState == AbilityModeState.Off || Agent.Main == null)
            {
                return;
            }

            if (_currentState == AbilityModeState.Targeting && _shouldPlayIdleCastStanceAnim)
            {
                ActionIndexCache current = Agent.Main.GetCurrentAction(1);
                if (!_idleCastAnimation.HasValue || current != _idleCastAnimation.Value)
                {
                    ActionIndexCache idle = IdleCastAnimation;

                    if (!_loggedAnimResolve)
                    {
                        _loggedAnimResolve = true;
                        int idx = idle.Index;
                        int noneIdx = ActionIndexCache.act_none.Index;
                        SotorLog.Info(
                            $"CastAnim: act_spellcasting_idle index={idx} (act_none={noneIdx}; {(idx == noneIdx ? "NOT REGISTERED — merge failed" : "resolved OK")}). " +
                            $"channel1Before={current.Index} mainHandWielded={Agent.Main.GetPrimaryWieldedItemIndex()} shouldSheath={_shouldSheathWeapon}");
                    }

                    Agent.Main.SetActionChannel(1, idle, false, (AnimFlags)0, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                }
            }
        }

        private void PlayCastReleaseAnimation(Agent caster, string actionName)
        {
            if (caster == null || string.IsNullOrWhiteSpace(actionName)
                || actionName.Equals("none", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                var action = ActionIndexCache.Create(actionName);
                if (action.Index == ActionIndexCache.act_none.Index)
                {
                    SotorLog.Debug($"CastRelease: action '{actionName}' unresolved (act_none); skipping.");
                    return;
                }

                caster.SetActionChannel(1, action, false, (AnimFlags)0, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                SotorLog.Info($"CastRelease: played '{actionName}' (index={action.Index}) on '{caster.Name}'.");
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"PlayCastReleaseAnimation('{actionName}') failed: {ex.Message}");
            }
        }

        private void SetUpCastStanceParticles()
        {
            RemoveCastStanceParticles();

            var main = Agent.Main;
            if (main == null || main.AgentVisuals == null || Mission?.Scene == null)
            {
                return;
            }

            var monster = Game.Current?.DefaultMonster;
            if (monster == null)
            {
                return;
            }

            _castStanceAgent = main;
            _castStancePsys = new ParticleSystem[2];
            _castStanceEntities = new GameEntity[2];
            _castStancePsys[0] = ApplyParticleToAgentBone(main, CastStanceParticleName, monster.MainHandItemBoneIndex, out _castStanceEntities[0]);
            _castStancePsys[1] = ApplyParticleToAgentBone(main, CastStanceParticleName, monster.OffHandItemBoneIndex, out _castStanceEntities[1]);
            EnableCastStanceParticles(false);
        }

        private static ParticleSystem ApplyParticleToAgentBone(Agent agent, string particleId, sbyte boneIndex, out GameEntity childEntity)
        {
            childEntity = null;
            if (string.IsNullOrWhiteSpace(particleId) || agent.AgentVisuals == null)
            {
                return null;
            }

            var scene = Mission.Current?.Scene;
            if (scene == null)
            {
                return null;
            }

            var skeleton = agent.AgentVisuals.GetSkeleton();
            if (skeleton == null || !skeleton.IsValid || boneIndex < 0 || boneIndex >= skeleton.GetBoneCount())
            {
                return null;
            }

            childEntity = GameEntity.CreateEmpty(scene, true, true, true);
            var identity = Mat3.Identity;
            var origin = new Vec3(0f, 0f, 0f, -1f);
            var frame = new MatrixFrame(identity, origin);
            var psys = ParticleSystem.CreateParticleSystemAttachedToEntity(particleId, childEntity, ref frame);
            if (psys == null)
            {
                childEntity.Remove(0);
                childEntity = null;
                return null;
            }

            agent.AgentVisuals.AddChildEntity(childEntity);
            skeleton.AddComponentToBone(boneIndex, psys);
            return psys;
        }

        private void EnableCastStanceParticles(bool enable)
        {
            if (_castStancePsys == null)
            {
                return;
            }

            foreach (var psys in _castStancePsys)
            {
                psys?.SetEnable(enable);
            }
        }

        private void RemoveCastStanceParticles()
        {
            if (_castStancePsys == null)
            {
                return;
            }

            for (int i = 0; i < _castStancePsys.Length; i++)
            {
                var entity = (_castStanceEntities != null && i < _castStanceEntities.Length) ? _castStanceEntities[i] : null;
                if (entity != null)
                {
                    entity.RemoveAllParticleSystems();
                    if (_castStanceAgent != null && _castStanceAgent.AgentVisuals != null)
                    {
                        _castStanceAgent.AgentVisuals.RemoveChildEntity(entity, 0);
                    }
                    else
                    {
                        entity.Remove(0);
                    }
                }
            }

            _castStancePsys = null;
            _castStanceEntities = null;
            _castStanceAgent = null;
        }

        private void CacheWieldedItemsForRestore()
        {
            if (_shouldWieldWeapon || Agent.Main == null)
            {
                return;
            }

            _mainHand = Agent.Main.GetPrimaryWieldedItemIndex();
            _offHand = Agent.Main.GetOffhandWieldedItemIndex();
        }

        private void UpdateWieldedItems()
        {
            var main = Agent.Main;
            if (main == null || !main.IsActive())
            {
                _shouldSheathWeapon = false;
                _shouldWieldWeapon = false;
                _mainHand = EquipmentIndex.None;
                _offHand = EquipmentIndex.None;
                return;
            }

            if (_currentState == AbilityModeState.Targeting && _shouldSheathWeapon)
            {
                if (main.GetPrimaryWieldedItemIndex() != EquipmentIndex.None)
                {
                    main.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.WithAnimation);
                    return;
                }

                if (main.GetOffhandWieldedItemIndex() != EquipmentIndex.None)
                {
                    main.TryToSheathWeaponInHand(Agent.HandIndex.OffHand, Agent.WeaponWieldActionType.WithAnimation);
                    return;
                }

                _shouldSheathWeapon = false;
            }

            if (_currentState == AbilityModeState.Off && _shouldWieldWeapon
                && Mission.CurrentTime >= _postCastSuppressUntil)
            {
                var curMain = main.GetPrimaryWieldedItemIndex();
                var curOff = main.GetOffhandWieldedItemIndex();
                bool mainDone = _mainHand == EquipmentIndex.None || curMain == _mainHand;
                bool offDone = _offHand == EquipmentIndex.None || curOff == _offHand;

                if (mainDone && offDone)
                {
                    _shouldWieldWeapon = false;
                }
                else if (_mainHand != EquipmentIndex.None && !mainDone)
                {
                    main.TryToWieldWeaponInSlot(_mainHand, Agent.WeaponWieldActionType.WithAnimation, false);
                }
                else if (_offHand != EquipmentIndex.None && !offDone)
                {
                    main.TryToWieldWeaponInSlot(_offHand, Agent.WeaponWieldActionType.WithAnimation, false);
                }
            }
        }

        private void ApplyBattleStartWindsPerks()
        {
            try
            {
                var hero = Agent.Main?.GetHero();
                if (hero == null)
                {
                    return;
                }

                if (SOTOR.Extensions.ExtendedInfoSystem.HeroExtendedInfo.TestingMaxWindsOverride >= 0f)
                {
                    hero.SetWindsOfMagic(SOTOR.Extensions.ExtendedInfoSystem.HeroExtendedInfo.TestingMaxWindsOverride);
                    SotorLog.Info($"TESTING: filled Winds of Magic to {SOTOR.Extensions.ExtendedInfoSystem.HeroExtendedInfo.TestingMaxWindsOverride} at battle start.");
                }

                if (SotorPerks.Improvision != null && hero.GetPerkValue(SotorPerks.Improvision) && hero.GetWindsOfMagic() < 25f)
                {
                    hero.SetWindsOfMagic(25f);
                    SotorLog.Info("Improvision: floored Winds of Magic to 25 at battle start.");
                }

                if (SotorPerks.Catalyst != null && hero.GetPerkValue(SotorPerks.Catalyst))
                {
                    int legendaryCount = 0;
                    var eq = hero.BattleEquipment;
                    if (eq != null)
                    {
                        for (var slot = TaleWorlds.Core.EquipmentIndex.WeaponItemBeginSlot; slot < TaleWorlds.Core.EquipmentIndex.ArmorItemEndSlot; slot++)
                        {
                            var element = eq[slot];
                            if (element.Item != null && element.ItemModifier != null
                                && element.ItemModifier.ItemQuality == TaleWorlds.Core.ItemQuality.Legendary)
                            {
                                legendaryCount++;
                            }
                        }
                    }

                    if (legendaryCount > 0)
                    {
                        hero.AddWindsOfMagic(legendaryCount * 5f, allowOverMax: true);
                        SotorLog.Info($"Catalyst: +{legendaryCount * 5} Winds at battle start ({legendaryCount} legendary item(s)).");
                    }
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"ApplyBattleStartWindsPerks failed: {ex.Message}");
            }
        }

        private void SlowDownTime(bool enable)
        {

            if (Mission == null)
            {
                return;
            }

            if (enable && !SotorSettings.EnableCastSlowMotion)
            {
                enable = false;
            }

            bool alreadyRequested = Mission.GetRequestedTimeSpeed(_timeRequestID, out _);
            if (alreadyRequested && !enable)
            {
                Mission.RemoveTimeSpeedRequest(_timeRequestID);
            }
            else if (!alreadyRequested && enable)
            {
                var request = new Mission.TimeSpeedRequest(0.3f, _timeRequestID);
                _timeRequestID = request.RequestID;
                Mission.AddTimeSpeedRequest(request);
            }
        }

        public bool IsCastingMission()
        {
            return !Mission.IsFriendlyMission
                && (int)Mission.CombatType != 1
                && (int)Mission.CombatType != 2;
        }

        private bool IsAbilityModeAvailableForMainAgent()
        {
            return !_battleResultReached
                && Agent.Main != null
                && Agent.Main.IsActive()
                && !ScreenManager.GetMouseVisibility()
                && IsCastingMission()
                && !Mission.IsInPhotoMode
                && !Mission.IsOrderMenuOpen
                && AbilityMissionModeHelper.IsAbilityHudMissionMode(Mission)
                && _abilityComponent != null
                && _abilityComponent.CurrentAbility != null;
        }
    }
}
