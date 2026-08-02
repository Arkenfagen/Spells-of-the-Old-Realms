using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SOTOR.AbilitySystem
{

    [Serializable]
    public class TriggeredEffectTemplate
    {
        [XmlAttribute]
        public string StringID { get; set; } = string.Empty;

        [XmlAttribute]
        public string BurstParticleEffectPrefab { get; set; } = "none";

        [XmlAttribute]
        public bool DoNotAlignParticleEffectPrefabOnImpact { get; set; } = false;

        [XmlAttribute]
        public string SoundEffectId { get; set; } = "none";

        [XmlAttribute]
        public float SoundEffectLength { get; set; } = 2.5f;

        [XmlAttribute]
        public DamageType DamageType { get; set; } = DamageType.Fire;

        [XmlAttribute]
        public int DamageAmount { get; set; } = 50;

        [XmlAttribute]
        public float Radius { get; set; } = 5f;

        [XmlAttribute]
        public bool HasShockWave { get; set; } = false;

        [XmlAttribute]
        public TargetType TargetType { get; set; } = TargetType.Enemy;

        [XmlAttribute]
        public float DamageVariance { get; set; } = 0.2f;

        [XmlAttribute]
        public float ImbuedStatusEffectDuration { get; set; } = 5f;

        [XmlElement("ImbuedStatusEffect")]
        public List<string> ImbuedStatusEffects { get; set; } = new List<string>();

        [XmlAttribute]
        public string ScriptNameToTrigger { get; set; } = "none";

        [XmlAttribute]
        public string TroopIdToSummon { get; set; } = "none";

        [XmlAttribute]
        public int NumberToSummon { get; set; } = 0;
    }
}
