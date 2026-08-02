using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SOTOR.AbilitySystem.StatusEffects
{

    [Serializable]
    public class StatusEffectTemplate
    {
        public enum EffectType
        {
            HealthOverTime,
            WindsOverTime,
            LanceSteadiness,
            DamageOverTime,
            DamageAmplification,
            Resistance,
            MovementManipulation,
            AttackSpeedManipulation,
            ReloadSpeedManipulation,
            TemporaryAttributeOnly,
            Invalid
        }

        [XmlAttribute("id")]
        public string StringID { get; set; }

        [XmlAttribute("particle_id")]
        public string ParticleId { get; set; }

        [XmlAttribute("apply_particle_to_root_bone_only")]
        public bool ApplyToRootBoneOnly { get; set; } = false;

        [XmlAttribute("apply_particle_to_weapon")]
        public bool ApplyToWeapon { get; set; } = false;

        [XmlAttribute("do_not_attach_to_agent_skeleton")]
        public bool DoNotAttachToSkeleton { get; set; } = false;

        [XmlAttribute("base_effect_value")]
        public float BaseEffectValue { get; set; } = 0f;

        [XmlAttribute("type")]
        public EffectType Type { get; set; } = EffectType.Invalid;

        [XmlAttribute("damage_type")]
        public DamageType DamageType { get; set; } = DamageType.Physical;

        [XmlAttribute("applies_for_attack_type")]
        public AttackTypeMask AttackTypeMask { get; set; } = AttackTypeMask.All;

        [XmlElement("temporary_attribute")]
        public List<string> TemporaryAttributes { get; set; } = new List<string>();

        [XmlIgnore]
        public bool IsBuffEffect => Type != EffectType.Invalid && Type != EffectType.DamageOverTime;
    }
}
