﻿using RoR2;
using RoR2.Projectile;
using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;

namespace EntityStates.Enforcer
{
    public class ShieldBash : BaseSkillState
    {
        public static float baseDuration = 0.8f;
        public static float damageCoefficient = 2.5f;
        public static float procCoefficient = 1f;
        public static float knockbackForce = 0.2f;
        public static float blastRadius = 6f;
        public static float deflectRadius = 3f;
        public static string hitboxString = "ShieldHitbox"; //transform where the hitbox is fired
        public static float beefDurationNoShield = 0.4f;
        public static float beefDurationShield = 0.8f;

        private float attackStopDuration;
        private float duration;
        private float fireDuration;
        private float deflectDuration;
        private Ray aimRay;
        private BlastAttack blastAttack;
        private ChildLocator childLocator;
        private bool hasFired;
        private bool usingBash;
        private bool hasDeflected;

        private List<CharacterBody> victimList = new List<CharacterBody>();

        public override void OnEnter()
        {
            base.OnEnter();

            this.duration = ShieldBash.baseDuration / this.attackSpeedStat;
            this.fireDuration = this.duration * 0.15f;
            this.deflectDuration = this.duration * 0.45f;
            this.aimRay = base.GetAimRay();
            this.hasFired = false;
            this.hasDeflected = false;
            this.usingBash = false;
            this.childLocator = base.GetModelTransform().GetComponent<ChildLocator>();
            base.StartAimMode(aimRay, 2f, false);

            //yep cock
            if (base.characterBody.isSprinting)
            {
                this.hasDeflected = true;
                this.usingBash = true;
                this.hasFired = true;
                base.skillLocator.secondary.skillDef.activationStateMachineName = "Body";
                this.outer.SetNextState(new ShoulderBash());
                return;
            }

            if (base.HasBuff(EnforcerPlugin.EnforcerPlugin.jackBoots))
            {
                base.PlayAnimation("Gesture, Override", "Bash", "ShieldBash.playbackRate", this.duration);
                this.attackStopDuration = ShieldBash.beefDurationShield / this.attackSpeedStat;
            }
            else
            {
                base.PlayAnimation("FullBody, Override", "ShieldBash", "ShieldBash.playbackRate", this.duration);
                this.attackStopDuration = ShieldBash.beefDurationNoShield / this.attackSpeedStat;
            }

            Util.PlayScaledSound(EnforcerPlugin.Sounds.ShieldBash, base.gameObject, this.attackSpeedStat);
        }

        private void FireBlast()
        {
            if (!this.hasFired)
            {
                this.hasFired = true;

                if (base.isAuthority)
                {
                    Vector3 center = childLocator.FindChild(hitboxString).position;

                    blastAttack = new BlastAttack();
                    blastAttack.radius = ShieldBash.blastRadius;
                    blastAttack.procCoefficient = ShieldBash.procCoefficient;
                    blastAttack.position = center;
                    blastAttack.attacker = base.gameObject;
                    blastAttack.crit = Util.CheckRoll(base.characterBody.crit, base.characterBody.master);
                    blastAttack.baseDamage = base.characterBody.damage * ShieldBash.damageCoefficient;
                    blastAttack.falloffModel = BlastAttack.FalloffModel.None;
                    blastAttack.baseForce = 3f;
                    blastAttack.teamIndex = TeamComponent.GetObjectTeam(blastAttack.attacker);
                    blastAttack.damageType = DamageType.Stun1s;
                    blastAttack.attackerFiltering = AttackerFiltering.NeverHit;
                    blastAttack.impactEffect = EntityStates.ImpBossMonster.GroundPound.hitEffectPrefab.GetComponent<EffectComponent>().effectIndex;

                    blastAttack.Fire();

                    KnockBack();
                }
            }
        }

        public override void OnExit()
        {
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (base.fixedAge < this.attackStopDuration)
            {
                if (base.characterMotor)
                {
                    base.characterMotor.moveDirection = Vector3.zero;
                }
            }

            if (base.fixedAge >= this.fireDuration)
            {
                this.FireBlast();
            }

            if (base.fixedAge < this.deflectDuration)
            {
                this.Deflect();
            }

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        private void KnockBack()
        {
            Collider[] array = Physics.OverlapSphere(childLocator.FindChild(hitboxString).position, ShieldBash.blastRadius, LayerIndex.defaultLayer.mask);
            for (int i = 0; i < array.Length; i++)
            {
                HealthComponent component = array[i].GetComponent<HealthComponent>();
                if (component)
                {
                    TeamComponent component2 = component.GetComponent<TeamComponent>();
                    if (component2.teamIndex != TeamIndex.Player)
                    {
                        AddToList(component.gameObject);

                        Util.PlaySound(EnforcerPlugin.Sounds.BashHitEnemy, component.gameObject);
                    }
                }
            }

            victimList.ForEach(Push);
        }

        private void AddToList(GameObject affectedObject)
        {
            CharacterBody component = affectedObject.GetComponent<CharacterBody>();
            if (!this.victimList.Contains(component))
            {
                this.victimList.Add(component);
            }
        }

        void Push(CharacterBody charb)
        {
            Vector3 velocity = ((aimRay.origin + 200 * aimRay.direction) - childLocator.FindChild(hitboxString).position + (75 * Vector3.up)) * ShieldBash.knockbackForce;

            
            if (charb.characterMotor)
            {
                charb.characterMotor.velocity += velocity;
            }
            else
            {
                Rigidbody component2 = charb.GetComponent<Rigidbody>();
                if (component2)
                {
                    component2.velocity += velocity;
                }
            }
        }

        private void Deflect()
        {
            if (this.usingBash) return;

            Collider[] array = Physics.OverlapSphere(childLocator.FindChild(hitboxString).position, ShieldBash.deflectRadius, LayerIndex.projectile.mask);

            for (int i = 0; i < array.Length; i++)
            {
                ProjectileController pc = array[i].GetComponentInParent<ProjectileController>();
                if (pc)
                {
                    if (pc.teamFilter.teamIndex != TeamIndex.Player)
                    {
                        Ray aimRay = base.GetAimRay();
                        Vector3 aimSpot = (aimRay.origin + 100 * aimRay.direction) - pc.gameObject.transform.position;
                        FireProjectileInfo info = new FireProjectileInfo()
                        {
                            projectilePrefab = pc.gameObject,
                            position = pc.gameObject.transform.position,
                            rotation = base.characterBody.transform.rotation * Quaternion.FromToRotation(new Vector3(0, 0, 1), aimSpot),
                            owner = base.characterBody.gameObject,
                            damage = base.characterBody.damage * 10f,
                            force = 200f,
                            crit = base.RollCrit(),
                            damageColorIndex = DamageColorIndex.Default,
                            target = null,
                            speedOverride = 120f,
                            fuseOverride = -1f
                        };
                        ProjectileManager.instance.FireProjectile(info);

                        Util.PlayScaledSound(EnforcerPlugin.Sounds.BashDeflect, base.gameObject, UnityEngine.Random.Range(0.9f, 1.1f));

                        Destroy(pc.gameObject);

                        if (!this.hasDeflected)
                        {
                            this.hasDeflected = true;
                            Util.PlaySound(EnforcerPlugin.Sounds.SirenSpawn, base.gameObject);

                            base.characterBody.GetComponent<EnforcerLightController>().FlashLights(8);
                        }
                    }
                }
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }

    public class ShoulderBash : BaseSkillState
    {
        [SerializeField]
        public float baseDuration = 0.65f;
        [SerializeField]
        public float speedMultiplier = 1.025f;
        public static float chargeDamageCoefficient = 4.5f;
        public static float knockbackDamageCoefficient = 7f;
        public static float massThresholdForKnockback = 150;
        public static float knockbackForce = 3400;
        public static float smallHopVelocity = 16f;

        private float duration;
        private float hitPauseTimer;
        private Vector3 idealDirection;
        private OverlapAttack attack;
        private bool inHitPause;
        private List<HealthComponent> victimsStruck = new List<HealthComponent>();

        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = this.baseDuration;

            base.characterBody.GetComponent<EnforcerLightController>().FlashLights(4);

            if (base.isAuthority)
            {
                if (base.inputBank)
                {
                    this.idealDirection = base.inputBank.aimDirection;
                    this.idealDirection.y = 0f;
                }
                this.UpdateDirection();
            }

            if (base.characterDirection)
            {
                base.characterDirection.forward = this.idealDirection;
            }

            base.characterBody.isSprinting = true;

            Util.PlayScaledSound(Croco.Leap.leapSoundString, base.gameObject, 1.75f);

            base.PlayAnimation("FullBody, Override", "ShoulderBash");//, "ShoulderBash.playbackRate", this.duration

            HitBoxGroup hitBoxGroup = null;
            Transform modelTransform = base.GetModelTransform();

            if (modelTransform)
            {
                hitBoxGroup = Array.Find<HitBoxGroup>(modelTransform.GetComponents<HitBoxGroup>(), (HitBoxGroup element) => element.groupName == "Charge");
            }

            this.attack = new OverlapAttack();
            this.attack.attacker = base.gameObject;
            this.attack.inflictor = base.gameObject;
            this.attack.teamIndex = base.GetTeam();
            this.attack.damage = ShoulderBash.chargeDamageCoefficient * this.damageStat;
            this.attack.hitEffectPrefab = Loader.SwingChargedFist.overchargeImpactEffectPrefab;
            this.attack.forceVector = Vector3.up * Toolbot.ToolbotDash.upwardForceMagnitude;
            this.attack.pushAwayForce = Toolbot.ToolbotDash.awayForceMagnitude;
            this.attack.hitBoxGroup = hitBoxGroup;
            this.attack.isCrit = base.RollCrit();
        }

        public override void OnExit()
        {
            if (base.characterBody)
            {
                //eat shit
                base.characterBody.isSprinting = true;
            }

            if (base.characterMotor && !base.characterMotor.disableAirControlUntilCollision)
            {
                base.characterMotor.velocity += this.GetIdealVelocity();
            }

            if (base.skillLocator) base.skillLocator.secondary.skillDef.activationStateMachineName = "Weapon";

            base.PlayAnimation("FullBody, Override", "BufferEmpty");

            base.OnExit();
        }

        private void UpdateDirection()
        {
            if (base.inputBank)
            {
                Vector2 vector = Util.Vector3XZToVector2XY(base.inputBank.moveVector);
                if (vector != Vector2.zero)
                {
                    vector.Normalize();
                    this.idealDirection = new Vector3(vector.x, 0f, vector.y).normalized;
                }
            }
        }

        private Vector3 GetIdealVelocity()
        {
            return base.characterDirection.forward * base.characterBody.moveSpeed * this.speedMultiplier;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            base.characterBody.isSprinting = true;

            if (base.fixedAge >= this.duration)
            {
                this.outer.SetNextStateToMain();
                return;
            }

            if (base.isAuthority)
            {
                if (!this.inHitPause)
                {
                    if (base.characterDirection)
                    {
                        base.characterDirection.moveVector = this.idealDirection;
                        if (base.characterMotor && !base.characterMotor.disableAirControlUntilCollision)
                        {
                            base.characterMotor.rootMotion += this.GetIdealVelocity() * Time.fixedDeltaTime;
                        }
                    }

                    this.attack.damage = this.damageStat * ShoulderBash.chargeDamageCoefficient;

                    if (this.attack.Fire(this.victimsStruck))
                    {
                        Util.PlaySound(EnforcerPlugin.Sounds.ShoulderBashHit, base.gameObject);
                        this.inHitPause = true;
                        this.hitPauseTimer = Toolbot.ToolbotDash.hitPauseDuration;
                        base.AddRecoil(-0.5f * Toolbot.ToolbotDash.recoilAmplitude, -0.5f * Toolbot.ToolbotDash.recoilAmplitude, -0.5f * Toolbot.ToolbotDash.recoilAmplitude, 0.5f * Toolbot.ToolbotDash.recoilAmplitude);

                        for (int i = 0; i < this.victimsStruck.Count; i++)
                        {
                            float num = 0f;
                            HealthComponent healthComponent = this.victimsStruck[i];
                            CharacterMotor component = healthComponent.GetComponent<CharacterMotor>();
                            if (component)
                            {
                                num = component.mass;
                            }
                            else
                            {
                                Rigidbody component2 = healthComponent.GetComponent<Rigidbody>();
                                if (component2)
                                {
                                    num = component2.mass;
                                }
                            }
                            if (num >= ShoulderBash.massThresholdForKnockback)
                            {
                                this.outer.SetNextState(new ShoulderBashImpact
                                {
                                    victimHealthComponent = healthComponent,
                                    idealDirection = this.idealDirection,
                                    isCrit = this.attack.isCrit
                                });
                                return;
                            }
                        }
                        return;
                    }
                }
                else
                {
                    base.characterMotor.velocity = Vector3.zero;
                    this.hitPauseTimer -= Time.fixedDeltaTime;
                    if (this.hitPauseTimer < 0f)
                    {
                        this.inHitPause = false;
                    }
                }
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Frozen;
        }
    }

    public class ShoulderBashImpact : BaseState
    {
        public HealthComponent victimHealthComponent;
        public Vector3 idealDirection;
        public bool isCrit;

        public float duration = 0.2f;

        public override void OnEnter()
        {
            base.OnEnter();

            base.PlayAnimation("FullBody, Override", "BufferEmpty");

            base.SmallHop(base.characterMotor, ShoulderBash.smallHopVelocity);

            if (NetworkServer.active)
            {
                if (this.victimHealthComponent)
                {
                    DamageInfo damageInfo = new DamageInfo
                    {
                        attacker = base.gameObject,
                        damage = this.damageStat * ShoulderBash.knockbackDamageCoefficient,
                        crit = this.isCrit,
                        procCoefficient = 1f,
                        damageColorIndex = DamageColorIndex.Item,
                        damageType = DamageType.Stun1s,
                        position = base.characterBody.corePosition
                    };

                    this.victimHealthComponent.TakeDamage(damageInfo);
                    GlobalEventManager.instance.OnHitEnemy(damageInfo, this.victimHealthComponent.gameObject);
                    GlobalEventManager.instance.OnHitAll(damageInfo, this.victimHealthComponent.gameObject);
                }

                base.healthComponent.TakeDamageForce(this.idealDirection * -ShoulderBash.knockbackForce, true, false);
            }

            if (base.isAuthority)
            {
                base.AddRecoil(-0.5f * Toolbot.ToolbotDash.recoilAmplitude * 3f, -0.5f * Toolbot.ToolbotDash.recoilAmplitude * 3f, -0.5f * Toolbot.ToolbotDash.recoilAmplitude * 8f, 0.5f * Toolbot.ToolbotDash.recoilAmplitude * 3f);
                EffectManager.SimpleImpactEffect(Loader.SwingZapFist.overchargeImpactEffectPrefab, base.characterBody.corePosition, base.characterDirection.forward, true);
                this.outer.SetNextStateToMain();
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (base.fixedAge >= this.duration)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(this.victimHealthComponent ? this.victimHealthComponent.gameObject : null);
            writer.Write(this.idealDirection);
            writer.Write(this.isCrit);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            GameObject gameObject = reader.ReadGameObject();
            this.victimHealthComponent = (gameObject ? gameObject.GetComponent<HealthComponent>() : null);
            this.idealDirection = reader.ReadVector3();
            this.isCrit = reader.ReadBoolean();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Frozen;
        }
    }
}