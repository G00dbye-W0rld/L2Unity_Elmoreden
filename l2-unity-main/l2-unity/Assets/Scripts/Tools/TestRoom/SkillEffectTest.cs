#if (UNITY_EDITOR)
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class SkillEffectTest : MonoBehaviour
{

    public Entity caster;
    public Entity target;
    public float spawnDelay = 1f;
    public int skillId = 1177;
    public int hitTime = 3000;

    // Liste parcourue au clavier. skillId suit l'element courant.
    public List<int> skillIds = new List<int> { 1177, 1011, 1040, 3, 1092, 16 };
    public bool autoLoop = false;

    private int _index = 0;
    private string _report = "";

    void Awake()
    {
        if (caster == null)
        {
            caster = GameObject.Find("Caster").GetComponent<Entity>();
            caster.GetComponent<NewHumanoidAnimationController>().Initialize();
            caster.GetComponent<Gear>().Initialize(0);
        }
        if (target == null)
        {
            target = GameObject.Find("Target").GetComponent<Entity>();
            target.GetComponent<NewHumanoidAnimationController>().Initialize();
            target.GetComponent<Gear>().Initialize(0);
        }
    }

    void Start()
    {
        if (skillIds != null && skillIds.Count > 0)
        {
            _index = Mathf.Max(0, skillIds.IndexOf(skillId));
            skillId = skillIds[_index];
        }
        StartCoroutine(DebugCoroutine());
    }


    private int flags = 0;
    public bool soulshot = false;
    public bool spiritshot = false;
    public bool crit = true;
    public int shld = 0;
    public bool miss = false;
    public int ssGrade = (int)EtcEffectInfo.EEP_GRADENONE;
    private int HITFLAG_USESS = 0x10;
    private int HITFLAG_CRIT = 0x20;
    private int HITFLAG_SHLD = 0x40;
    private int HITFLAG_MISS = 0x80;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) Cast();
        if (Input.GetKeyDown(KeyCode.RightArrow)) Step(1);
        if (Input.GetKeyDown(KeyCode.LeftArrow)) Step(-1);
        if (Input.GetKeyDown(KeyCode.L)) autoLoop = !autoLoop;
    }

    private void Step(int dir)
    {
        if (skillIds == null || skillIds.Count == 0) return;
        _index = (_index + dir + skillIds.Count) % skillIds.Count;
        skillId = skillIds[_index];
        Cast();
    }

    // L'attente couvre l'incantation : relancer avant la fin coupe l'effet.
    private IEnumerator DebugCoroutine()
    {
        yield return new WaitForSeconds(2f);
        caster.ReferenceHolder.NewAnimationController.Initialize();
        Cast();
        while (true)
        {
            if (autoLoop) Cast();
            yield return new WaitForSeconds(hitTime / 1000f + spawnDelay);
        }
    }

    private void Cast()
    {
        caster.Combat.Target = target;
        caster.Combat.AttackTarget = target;
        Describe();
        WorldCombat.Instance.EntityCastSkill(caster, target, skillId, hitTime, 3);
        if (spiritshot) WorldCombat.Instance.EntityCastSkill(caster, 2047);
        if (soulshot) WorldCombat.Instance.EntityCastSkill(caster, 2039);
    }

    // Dit ce que le sort declare et si le prefab correspondant est charge :
    // sans ca, un effet absent et un effet invisible se ressemblent.
    private void Describe()
    {
        StringBuilder sb = new StringBuilder();
        Skill skill = SkillTable.Instance.GetSkill(skillId);
        if (skill == null)
        {
            _report = $"Skill {skillId} : INTROUVABLE dans SkillTable";
            Debug.LogWarning(_report);
            return;
        }

        string name = (skill.SkillNameDatas != null && skill.SkillNameDatas.Length > 0)
            ? skill.SkillNameDatas[0].Name : "?";
        // EffectId lit Skillgrps[0] : sans garde, un skill sans grp fait lever Describe.
        string effectId = (skill.Skillgrps != null && skill.Skillgrps.Length > 0)
            ? skill.EffectId.ToString() : "(pas de skillgrp)";
        sb.AppendLine($"[{_index + 1}/{(skillIds == null ? 0 : skillIds.Count)}] skill {skillId} \"{name}\"  effet {effectId}");

        if (skill.SkillEffect == null)
        {
            sb.AppendLine("  aucun L2SkillEffect : pas de json, ou json illisible");
        }
        else
        {
            Line(sb, "incantation", skill.SkillEffect.CastingActions);
            Line(sb, "tir", skill.SkillEffect.ShotActions);
            Line(sb, "explosion", skill.SkillEffect.ExplosionActions);
        }

        _report = sb.ToString();
        Debug.Log(_report);
    }

    private void Line(StringBuilder sb, string label, List<EffectEmitter> actions)
    {
        if (actions == null || actions.Count == 0) return;
        foreach (EffectEmitter e in actions)
        {
            bool loaded = e.EffectClass != null
                && ParticleEffectTable.Instance.ParticleEffects != null
                && ParticleEffectTable.Instance.ParticleEffects.ContainsKey(e.EffectClass);
            sb.AppendLine($"  {label} : {e.EffectClass ?? "(null)"} {(loaded ? "OK" : "ABSENT")}"
                + $" attache {e.AttachOn} echelle {e.ScaleSize}");
        }
    }

    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 620, 150), "");
        GUI.Label(new Rect(20, 15, 600, 20),
            "ESPACE rejouer   FLECHE DROITE/GAUCHE changer de skill   L boucle auto : "
            + (autoLoop ? "ON" : "OFF"));
        GUI.Label(new Rect(20, 40, 600, 115), _report);
    }

    private void DebugHit()
    {
        flags = 0;
        if (soulshot)
        {
            flags |= HITFLAG_USESS | ssGrade;
        }
        if (crit)
        {
            flags |= HITFLAG_CRIT;
        }
        if (shld > 0)
        {
            flags |= HITFLAG_SHLD;
        }
        if (miss)
        {
            flags |= HITFLAG_MISS;
        }
        Hit hit = new Hit(target.Identity.Id, 10, flags);
        WorldCombat.Instance.EntityCastSkill(caster, 2122);
    }
}
#endif
