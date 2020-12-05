﻿using System;
using UnityEngine;
using UnityEngine.Networking;
using RoR2;

namespace EntityStates.Nemforcer
{
    public class MinigunToggle : BaseSkillState
    {
        public static float enterDuration = 0.6f;
        public static float exitDuration = 0.9f;

        private bool ye;
        private float duration;
        private Animator animator;
        private ChildLocator childLocator;
        private NemforcerController nemController;

        public override void OnEnter()
        {
            base.OnEnter();
            this.animator = GetModelAnimator();
            this.childLocator = base.GetModelChildLocator();
            this.nemController = base.GetComponent<NemforcerController>();

            if (base.HasBuff(EnforcerPlugin.EnforcerPlugin.minigunBuff))
            {
                this.duration = MinigunToggle.exitDuration / this.attackSpeedStat;

                base.PlayAnimation("Gesture, Override", "MinigunDown", "MinigunUp.playbackRate", this.duration);
                base.PlayAnimation("FullBody, Override", "BufferEmpty");
                this.ye = false;
                //base.PlayAnimation("FullBody, Override", "MinigunDown", "MinigunUp.playbackRate", this.duration);
                //base.PlayAnimation("Minigun", "Empty");

                if (base.skillLocator)
                {
                    base.skillLocator.special.SetBaseSkill(EnforcerPlugin.NemforcerPlugin.minigunDownDef);

                    base.skillLocator.primary.UnsetSkillOverride(base.skillLocator.primary, EnforcerPlugin.NemforcerPlugin.minigunFireDef, GenericSkill.SkillOverridePriority.Replacement);

                    float cooldown = base.skillLocator.secondary.rechargeStopwatch;
                    int stock = base.skillLocator.secondary.stock;

                    base.skillLocator.secondary.SetBaseSkill(EnforcerPlugin.NemforcerPlugin.hammerChargeDef);

                    base.skillLocator.secondary.stock = stock;
                    base.skillLocator.secondary.rechargeStopwatch = cooldown;
                }

                if (NetworkServer.active)
                {
                    base.characterBody.RemoveBuff(EnforcerPlugin.EnforcerPlugin.minigunBuff);
                    base.characterBody.AddBuff(EnforcerPlugin.EnforcerPlugin.tempLargeSlowDebuff);
                }

                this.animator.SetFloat("Minigun.spinSpeed", 0);
                this.animator.SetBool("minigunActive", false);

                string soundString = EnforcerPlugin.Sounds.ShieldDown;
                Util.PlaySound(soundString, base.gameObject);
            }
            else
            {
                this.duration = MinigunToggle.enterDuration / this.attackSpeedStat;

                base.PlayAnimation("Gesture, Override", "MinigunUp", "MinigunUp.playbackRate", this.duration);
                base.PlayAnimation("FullBody, Override", "BufferEmpty");
                this.ye = true;
                //base.PlayAnimation("Minigun", "MinigunUp", "MinigunUp.playbackRate", this.duration);

                if (base.skillLocator)
                {
                    base.skillLocator.special.SetBaseSkill(EnforcerPlugin.NemforcerPlugin.minigunUpDef);

                    base.skillLocator.primary.SetSkillOverride(base.skillLocator.primary, EnforcerPlugin.NemforcerPlugin.minigunFireDef, GenericSkill.SkillOverridePriority.Replacement);

                    float cooldown = base.skillLocator.secondary.rechargeStopwatch;
                    int stock = base.skillLocator.secondary.stock;

                    base.skillLocator.secondary.SetBaseSkill(EnforcerPlugin.NemforcerPlugin.hammerSlamDef);

                    base.skillLocator.secondary.stock = stock;
                    base.skillLocator.secondary.rechargeStopwatch = cooldown;
                }

                if (NetworkServer.active)
                {
                    base.characterBody.AddBuff(EnforcerPlugin.EnforcerPlugin.minigunBuff);
                    base.characterBody.AddBuff(EnforcerPlugin.EnforcerPlugin.tempLargeSlowDebuff);
                }

                this.animator.SetBool("minigunActive", true);

                string soundString = EnforcerPlugin.Sounds.ShieldUp;
                Util.PlaySound(soundString, base.gameObject);
            }

            base.characterBody.SetAimTimer(this.duration + 0.2f);

            if (this.nemController) this.nemController.ResetCrosshair();
        }

        public override void OnExit()
        {
            base.OnExit();

            if (NetworkServer.active) base.characterBody.RemoveBuff(EnforcerPlugin.EnforcerPlugin.tempLargeSlowDebuff);

            if (this.ye) this.animator.SetLayerWeight(this.animator.GetLayerIndex("Minigun"), 1);
            else this.animator.SetLayerWeight(this.animator.GetLayerIndex("Minigun"), 0);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (base.characterMotor.isGrounded)
            {
                base.characterMotor.velocity = Vector3.zero;
            }

            float progress = Mathf.Clamp01(base.fixedAge / this.duration);

            if (this.ye) this.animator.SetLayerWeight(this.animator.GetLayerIndex("Minigun"), progress);
            else this.animator.SetLayerWeight(this.animator.GetLayerIndex("Minigun"), 1 - progress);

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            if (base.HasBuff(EnforcerPlugin.EnforcerPlugin.minigunBuff)) return InterruptPriority.PrioritySkill;
            else return InterruptPriority.Skill;
        }
    }
}