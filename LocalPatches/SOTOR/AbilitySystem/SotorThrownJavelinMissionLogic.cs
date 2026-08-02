using System;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorThrownJavelinMissionLogic : MissionLogic
    {
        public static SotorThrownJavelinMissionLogic Instance { get; private set; }

        private const float FullPowerFraction = 1.0f;

        private const float WieldSettleGrace = 0.4f;

        private bool _readied;
        private Agent _readyCaster;
        private EquipmentIndex _readySlot = EquipmentIndex.None;
        private ThrownWeaponAbility _readyAbility;

        private EquipmentIndex _preCastWieldedSlot = EquipmentIndex.None;

        private bool _throwTriggered;

        private float _wieldedNoneSince = -1f;

        public SotorThrownJavelinMissionLogic()
        {

            Instance = this;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Instance = this;
        }

        protected override void OnEndMission()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void OnAmberJavelinReadied(Agent caster, ThrownWeaponAbility ability, EquipmentIndex preCastWieldedSlot)
        {
            try
            {
                Mission?.SetThrowingMissileSpeedModifier(FullPowerFraction);

                _readied = true;
                _throwTriggered = false;
                _wieldedNoneSince = -1f;
                _readyCaster = caster;
                _readyAbility = ability;
                _readySlot = caster?.GetPrimaryWieldedItemIndex() ?? EquipmentIndex.None;

                _preCastWieldedSlot = (preCastWieldedSlot == EquipmentIndex.ExtraWeaponSlot)
                    ? EquipmentIndex.None : preCastWieldedSlot;

                PlaySpearSound("sotor_create_amber_spear", caster);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"OnAmberJavelinReadied failed: {ex.Message}");
            }
        }

        private void PlaySpearSound(string soundName, Agent at)
        {
            try
            {
                if (Mission == null || at == null) return;

                int eventId = TaleWorlds.Engine.SoundEvent.GetEventIdFromString(soundName);
                if (eventId >= 0)
                {
                    Mission.MakeSound(eventId, at.Position, false, true, at.Index, -1);
                }
                else
                {
                    SotorLog.Warn($"Amber spear sound '{soundName}' not registered (eventId=-1).");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"PlaySpearSound('{soundName}') failed: {ex.Message}");
            }
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon,
            in Blow blow, in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData);
            try
            {
                if (!_readied || affectedAgent == null || affectedAgent != _readyCaster)
                {
                    return;
                }

                bool knockedDownOrBack = blow.BlowFlag.HasAnyFlag(BlowFlags.KnockDown)
                    || blow.BlowFlag.HasAnyFlag(BlowFlags.KnockBack);
                if (knockedDownOrBack)
                {
                    CancelReadiedJavelin("stagger/knockdown");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorThrownJavelinMissionLogic.OnAgentHit failed: {ex.Message}");
            }
        }

        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position,
            Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);

            try
            {
                if (shooterAgent == null || !IsAmberJavelinInSlot(shooterAgent, weaponIndex))
                {
                    return;
                }

                if (_readied && shooterAgent == _readyCaster)
                {

                    _readyAbility?.SetCoolDown(_readyAbility.Template.CoolDown);

                    if (Game.Current?.GameType is Campaign)
                    {
                        _readyAbility?.GrantThrowSpellcraftXp(shooterAgent.GetHero());
                    }

                    PlaySpearSound("sotor_throw_amber_spear", shooterAgent);

                    var restore = _preCastWieldedSlot;

                    _readied = false;
                    _throwTriggered = false;
                    _wieldedNoneSince = -1f;
                    _readyCaster = null;
                    _readyAbility = null;
                    _readySlot = EquipmentIndex.None;
                    _preCastWieldedSlot = EquipmentIndex.None;

                    RestoreWieldedWeapon(shooterAgent, restore);
                }

                if (Game.Current?.GameType is Campaign)
                {
                    var hero = shooterAgent.GetHero();
                    var info = hero?.GetExtendedInfo();
                    if (hero != null && info != null)
                    {
                        var template = AbilityFactory.GetTemplate("AmberSpearThrown");
                        int cost = template != null ? hero.GetEffectiveWindsCostForSpell(template) : 3;
                        hero.SetWindsOfMagic(Math.Max(0f, hero.GetWindsOfMagic() - cost));
                    }
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorThrownJavelinMissionLogic.OnAgentShootMissile failed: {ex.Message}");
            }
        }

        public void DriveThrowFlagsFromControlTick()
        {
            if (!_readied || _readyCaster == null)
            {
                return;
            }

            if (!_readyCaster.IsActive() || !IsAmberJavelinInSlot(_readyCaster, _readySlot))
            {
                _readied = false;
                _throwTriggered = false;
                _wieldedNoneSince = -1f;
                _readyCaster = null;
                _readyAbility = null;
                _readySlot = EquipmentIndex.None;
                return;
            }

            var wielded = _readyCaster.GetPrimaryWieldedItemIndex();
            bool swappedToOtherWeapon = wielded != _readySlot
                && wielded >= EquipmentIndex.WeaponItemBeginSlot
                && wielded <= EquipmentIndex.ExtraWeaponSlot;
            if (!_throwTriggered && swappedToOtherWeapon)
            {
                SotorLog.Debug($"Amber javelin: weapon-swap detected (wielded {wielded}); vanishing readied javelin.");
                CancelReadiedJavelin("weapon swap", restoreWeapon: false);
                return;
            }

            if (!_throwTriggered && wielded == EquipmentIndex.None)
            {
                float now = Mission != null ? Mission.CurrentTime : 0f;
                if (_wieldedNoneSince < 0f)
                {
                    _wieldedNoneSince = now;
                    return;
                }
                if (now - _wieldedNoneSince < WieldSettleGrace)
                {
                    return;
                }
                SotorLog.Debug($"Amber javelin: wield did not seat within {WieldSettleGrace:0.0}s; cancelling cleanly.");
                CancelReadiedJavelin("wield stuck (None persisted)");
                return;
            }

            _wieldedNoneSince = -1f;

            if (TaleWorlds.InputSystem.Input.IsKeyPressed(TaleWorlds.InputSystem.InputKey.RightMouseButton))
            {
                CancelReadiedJavelin("right-click");
                return;
            }

            try
            {
                if (!_throwTriggered)
                {

                    if (TaleWorlds.InputSystem.Input.IsKeyPressed(TaleWorlds.InputSystem.InputKey.LeftMouseButton))
                    {
                        _throwTriggered = true;

                        _readyCaster.MovementFlags &= ~Agent.MovementControlFlag.AttackMask;
                    }
                    else
                    {

                        var dir = _readyCaster.AttackDirectionToMovementFlag(_readyCaster.GetAttackDirection());
                        if ((dir & Agent.MovementControlFlag.AttackMask) == 0)
                        {
                            dir = Agent.MovementControlFlag.AttackUp;
                        }
                        var flags = _readyCaster.MovementFlags & ~Agent.MovementControlFlag.AttackMask;
                        _readyCaster.MovementFlags = flags | dir;
                    }
                }
                else
                {

                    _readyCaster.MovementFlags &= ~Agent.MovementControlFlag.AttackMask;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"Javelin cock/throw tick failed: {ex.Message}");
            }
        }

        public void CancelReadiedJavelin(string reason, bool restoreWeapon = true)
        {
            if (!_readied || _readyCaster == null)
            {
                return;
            }

            try
            {
                var caster = _readyCaster;
                var slot = _readySlot;
                var restore = _preCastWieldedSlot;
                SotorLog.Debug($"Amber javelin cancelled ({reason}); restoreWeapon={restoreWeapon}.");

                _readied = false;
                _throwTriggered = false;
                _wieldedNoneSince = -1f;
                _readyCaster = null;
                _readyAbility = null;
                _readySlot = EquipmentIndex.None;
                _preCastWieldedSlot = EquipmentIndex.None;

                caster.MovementFlags &= ~Agent.MovementControlFlag.AttackMask;
                caster.RemoveEquippedWeapon(slot);
                Mission?.SetThrowingMissileSpeedModifier(1f);
                if (restoreWeapon)
                {
                    RestoreWieldedWeapon(caster, restore);
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"Javelin cancel ({reason}) failed: {ex.Message}");
            }
        }

        private void RestoreWieldedWeapon(Agent caster, EquipmentIndex slot)
        {
            if (caster == null || !caster.IsActive() || slot < EquipmentIndex.WeaponItemBeginSlot
                || slot > EquipmentIndex.ExtraWeaponSlot)
            {
                return;
            }
            if (caster.Equipment[slot].IsEmpty)
            {
                return;
            }
            caster.TryToWieldWeaponInSlot(slot, Agent.WeaponWieldActionType.WithAnimation, false);
        }

        public override void OnMissileCollisionReaction(Mission.MissileCollisionReaction collisionReaction,
            Agent attackerAgent, Agent attachedAgent, sbyte attachedBoneIndex)
        {
            base.OnMissileCollisionReaction(collisionReaction, attackerAgent, attachedAgent, attachedBoneIndex);
            try
            {
                if (collisionReaction == Mission.MissileCollisionReaction.PassThrough) return;

                if (Mission?.MissilesList != null)
                {
                    foreach (var missile in Mission.MissilesList)
                    {
                        if (missile == null || missile.ShooterAgent != attackerAgent) continue;
                        if (missile.Weapon.IsEmpty || missile.Weapon.Item?.StringId != ThrownWeaponAbility.AmberJavelinItemId) continue;
                        missile.Entity?.SetVisibilityExcludeParents(false);
                    }
                }

                if (attachedAgent != null)
                {
                    for (int i = attachedAgent.GetAttachedWeaponsCount() - 1; i >= 0; i--)
                    {
                        var w = attachedAgent.GetAttachedWeapon(i);
                        if (!w.IsEmpty && w.Item?.StringId == ThrownWeaponAbility.AmberJavelinItemId)
                        {
                            attachedAgent.DeleteAttachedWeapon(i);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorThrownJavelinMissionLogic.OnMissileCollisionReaction failed: {ex.Message}");
            }
        }

        private static bool IsAmberJavelinInSlot(Agent agent, EquipmentIndex slot)
        {
            if (agent == null || slot < EquipmentIndex.WeaponItemBeginSlot || slot > EquipmentIndex.ExtraWeaponSlot)
            {
                return false;
            }
            var w = agent.Equipment[slot];
            return !w.IsEmpty && w.Item?.StringId == ThrownWeaponAbility.AmberJavelinItemId;
        }
    }
}
