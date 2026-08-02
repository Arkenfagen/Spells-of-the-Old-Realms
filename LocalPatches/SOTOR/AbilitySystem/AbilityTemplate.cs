using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using TaleWorlds.Library;

namespace SOTOR.AbilitySystem
{
    [Serializable]
    public class AbilityTemplate
    {
        private float _maxDistance = 25f;

        [XmlAttribute]
        public string StringID { get; set; } = string.Empty;

        [XmlAttribute]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute]
        public string SpriteName { get; set; } = string.Empty;

        [XmlAttribute]
        public int CoolDown { get; set; } = 10;

        [XmlAttribute]
        public int WindsOfMagicCost { get; set; }

        [XmlAttribute]
        public AbilityType AbilityType { get; set; } = AbilityType.Spell;

        [XmlAttribute]
        public AbilityEffectType AbilityEffectType { get; set; } = AbilityEffectType.Missile;

        [XmlAttribute]
        public CastType CastType { get; set; } = CastType.Instant;

        [XmlAttribute]
        public float CastTime { get; set; }

        [XmlAttribute]
        public AbilityTargetType AbilityTargetType { get; set; } = AbilityTargetType.EnemiesInAOE;

        [XmlAttribute]
        public CrosshairType CrosshairType { get; set; } = CrosshairType.Self;

        [XmlAttribute]
        public string BelongsToLoreID { get; set; } = string.Empty;

        [XmlAttribute]
        public int SpellTier { get; set; }

        [XmlAttribute]
        public string TooltipDescription { get; set; } = string.Empty;

        [XmlAttribute]
        public string ParticleEffectPrefab { get; set; } = "none";

        [XmlAttribute]
        public string TriggeredEffectID { get; set; } = string.Empty;

        [XmlAttribute]
        public string ShipTag { get; set; } = "untagged";

        [XmlAttribute]
        public float Duration { get; set; } = 4f;

        [XmlAttribute]
        public float BaseMovementSpeed { get; set; } = 20f;

        [XmlAttribute]
        public float Offset { get; set; }

        [XmlAttribute]
        public float Radius { get; set; } = 1f;

        [XmlAttribute]
        public float TickInterval { get; set; } = 0.1f;

        [XmlAttribute]
        public TriggerType TriggerType { get; set; } = TriggerType.OnCollision;

        [XmlAttribute]
        public bool UseGravity { get; set; } = false;

        [XmlAttribute]
        public bool Piercing { get; set; } = false;

        [XmlAttribute]
        public string SoundEffectToPlay { get; set; } = "none";

        [XmlAttribute]
        public bool ShouldSoundLoopOverDuration { get; set; } = false;

        [XmlAttribute]
        public string AnimationActionName { get; set; } = "act_release_heavy_thrown";

        [XmlAttribute]
        public float MaxRandomDeviation { get; set; }

        [XmlAttribute]
        public bool ShouldRotateVisuals { get; set; }

        [XmlAttribute]
        public float VisualsRotationVelocity { get; set; }

        [XmlAttribute]
        public float MinDistance { get; set; }

        [XmlAttribute]
        public float TargetCapturingRadius { get; set; } = 1f;

        [XmlAttribute]
        public float MaxDistance
        {
            get => _maxDistance;
            set
            {
                _maxDistance = value;
                MaxDistanceSpecified = true;
            }
        }

        [XmlIgnore]
        public bool MaxDistanceSpecified { get; set; }

        [XmlElement]
        public SeekerParameters SeekerParameters { get; set; }

        [XmlIgnore]
        public bool IsSpell => AbilityType == AbilityType.Spell;
    }

    [Serializable]
    public class SeekerParameters
    {
        [XmlAttribute]
        public float Proportional = 0.5f;

        [XmlAttribute]
        public float Derivative;

        [XmlAttribute]
        public float MaxDistance = float.MaxValue;

        [XmlAttribute]
        public float MinDistance = float.MinValue;

        [XmlAttribute]
        public float DisableDistance = float.MinValue;
    }
}
