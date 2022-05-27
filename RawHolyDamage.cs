using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;

namespace RawHolyDamage
{
    using HolyDamageManager;
    using InstanceIDs;
    using ParadoxNotion;
    using TinyHelper;
    using UnityEngine;

    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(HolyDamageManager.GUID, HolyDamageManager.VERSION)]
    public class RawHolyDamage : BaseUnityPlugin
    {

        public const string GUID = "com.ehaugw.rawholydamage";
        public const string VERSION = "1.0.0";
        public const string NAME = "Raw Holy Damage";
        
        public static Tag rawDamageBonusTag;

        internal void Awake()
        {
            HolyDamageManager.SetHolyType(DamageType.Types.Raw);
        
            var harmony = new Harmony(GUID);
            harmony.PatchAll();
        }

        //It's a harmony patch, but still placed here because it's a setup function
        [HarmonyPatch(typeof(ResourcesPrefabManager), "Load")]
        public class ResourcesPrefabManager_Load
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (ResourcesPrefabManager.Instance?.Loaded ?? false)
                {
                    SetDivineLightImbueType();

                    //maybe someone use this mod to set divine light imbue type, and then override the damage type. in that case, they shall not have the boon buffs!
                    if (HolyDamageManager.GetDamageType() == DamageType.Types.Raw)
                    {
                        MakeRawDamageBonusTag();
                        addBoonDamageBonus(IDs.disciplineEffectID, 20);
                        addBoonDamageBonus(IDs.disciplineAmplifiedEffectID, 20);
                        updateImbueEffectName(IDs.divineLightImbueID, "Weapon deals some " + HolyDamageManager.GetDamageType().ToString() + " damage and emits light.");
                    }
                }
            }
        }

        //It's a harmony patch, but still placed here because it's a setup function
        [HarmonyPatch(typeof(CharacterStats), "GetStat", new Type[] { typeof(Tag)})]
        public class CharacterStats_GetStat
        {
            [HarmonyPrefix]
            public static bool Prefix(CharacterStats __instance, ref Tag _stat, ref Stat __result, Stat[] ___m_damageTypesModifier)
            {
                if (_stat == rawDamageBonusTag)
                {
                    __result = ___m_damageTypesModifier[HolyDamageManager.GetHolyIndex()];
                    return false;//don't call the original function
                }
                return true;//call the original function
            }

            //[HarmonyPostfix]
            //public static bool Postfix(CharacterStats __instance, ref Tag _stat, out Stat __result, Stat[] ___m_damageTypesModifier)
            //{
            //    __result = null;

            //    if (_stat == rawDamageBonusTag)
            //    {
            //        __result = ___m_damageTypesModifier[HolyDamageManager.GetHolyDamageIndex()];
            //        return false;//don't call the original function
            //    }
            //    return true;//call the original function
            //}
        }

        private static void updateImbueEffectName(int effectID, string description)
        {
            var effectPreset = TinyEffectManager.GetEffectPreset(effectID) as ImbueEffectPreset;
            At.SetValue<string>(description, typeof(ImbueEffectPreset), effectPreset, "m_imbueDescKey");
            TinyEffectManager.SetNameAndDesc(effectPreset, effectPreset.Name, description);
        }

        private static void MakeRawDamageBonusTag()
        {
            rawDamageBonusTag = TinyTagManager.GetOrMakeTag("RawDamageBonus");
            HolyDamageManager.SetHolyTag(rawDamageBonusTag);
        }
        private static void SetDivineLightImbueType()
        {
            TinyEffectManager.ChangeEffectPresetDamageTypeData(TinyEffectManager.GetEffectPreset(IDs.divineLightImbueID), DamageType.Types.Electric, HolyDamageManager.GetDamageType());
        }

        private static void addBoonDamageBonus(int effectID, float damageBonus)
        {
            //Needed objects
            var effectPreset = TinyEffectManager.GetEffectPreset(effectID);
            var activationEffects = effectPreset.transform.FindInAllChildren("ActivationEffects").gameObject;
            var statusEffect = effectPreset.GetComponent<StatusEffect>();
            var statusData = statusEffect.StatusData;

            
            //At.SetValue("", typeof(StatusEffect), statusEffect, "m_descriptionLocKey");

            //Clueless if this is needed, but it adds a new EffectData that the new AffectStat can reference to
            var effectDatas = statusData.EffectsData.ToList();
            effectDatas.Add(new StatusData.EffectData() { Data = new string[] { damageBonus.ToString() } });
            statusData.EffectsData = effectDatas.ToArray();

            //Add the actual Affect Stat, enable it and set it to add thh desired bonus
            var affectStat = activationEffects.AddComponent<AffectStat>();
            affectStat.enabled = true;
            affectStat.Value = damageBonus;
            affectStat.AffectedStat = new TagSourceSelector(rawDamageBonusTag);//TagSourceManager.Instance.GetTag(100.ToString()));
            affectStat.Tags = new TagSourceSelector[] { };
            affectStat.IsModifier = true;
            At.SetValue(2, typeof(AffectStat), affectStat, "m_effectIndex");
            
            //Add teh Affect stat to the signature, which is needed for init or something
            var signature = statusEffect.StatusEffectSignature;
            signature.Effects.Add(affectStat);
            At.SetValue(signature.Effects, typeof(StatusData), statusData, "m_previousSignature");

            TinyEffectManager.SetNameAndDesc(effectPreset, statusEffect.StatusName, statusEffect.Description + " " + HolyDamageManager.GetDamageType().ToString() + " Damage +" + damageBonus.ToString() + "%.");

            //signature.RefreshSignature();
            //statusData = statusEffect.StatusData = new StatusData(statusData);

            //statusData.RefreshData();

        }
    }
}
