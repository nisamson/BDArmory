using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;
using BDArmory.Core.Utils;
using BDArmory.FX;
using BDArmory.Misc;
using System;
using System.Text;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using System.Collections;

namespace BDArmory.Modules
{
    class ModuleCASE : PartModule, IPartMassModifier, IPartCostModifier
    {
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => CASEmass;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float baseCost, ModifierStagingSituation situation) => CASEcost;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        private double ammoMass = 0;
        private double ammoQuantity = 0;
        private double ammoExplosionYield = 0;

        private string explModelPath = "BDArmory/Models/explosion/explosion";
        private string explSoundPath = "BDArmory/Sounds/explode1";

        private string limitEdexploModelPath = "BDArmory/Models/explosion/30mmExplosion";
        private string shuntExploModelPath = "BDArmory/Models/explosion/CASEexplosion";

        public string SourceVessel = "";
        public bool hasDetonated = false;
        private float blastRadius = 0;
        int explosionLayerMask = (int)(LayerMasks.Parts | LayerMasks.Scenery | LayerMasks.EVA | LayerMasks.Unknown19 | LayerMasks.Unknown23);

        public bool externallyCalled = false;

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.explosionPotential = 1.0f;
                part.force_activate();
            }
        }
        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_AddedMass")]//CASE mass

        public float CASEmass = 0f;

        private float CASEcost = 0f;
        // private float origCost = 0;
        private float origMass = 0f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_CASE"),//Cellular Ammo Storage Equipment Tier
        UI_FloatRange(minValue = 0f, maxValue = 2f, stepIncrement = 1f, scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public float CASELevel = 0; //tier of ammo storage. 0 = nothing, ammosplosion; 1 = base, ammosplosion contained(barely), 2 = blast safely shunted outside, minimal damage to surrounding parts

        private float oldCaseLevel = 0;

        private List<double> resourceAmount = new List<double>();
        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                var internalmag = part.FindModuleImplementing<ModuleWeapon>();
                if (internalmag != null)
                {
                    Fields["CASELevel"].guiActiveEditor = false;
                    Fields["CASEmass"].guiActiveEditor = false;
                }
                else
                {
                    using (IEnumerator<PartResource> resource = part.Resources.GetEnumerator())
                        while (resource.MoveNext())
                        {
                            if (resource.Current == null) continue;
                            resourceAmount.Add(resource.Current.maxAmount);
                        }
                    UI_FloatRange ATrangeEditor = (UI_FloatRange)Fields["CASELevel"].uiControlEditor;
                    ATrangeEditor.onFieldChanged = CASESetup;
                    origMass = part.mass;
                    //origScale = part.rescaleFactor;
                    CASESetup(null, null);
                }
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                SourceVessel = part.vessel.GetName(); //set default to vesselname for cases where no attacker, i.e. Ammo exploding on destruction cooking off adjacent boxes
                GameEvents.onGameSceneSwitchRequested.Add(HandleSceneChange);
            }
        }

        public void HandleSceneChange(GameEvents.FromToAction<GameScenes, GameScenes> fromTo)
        {
            if (fromTo.from == GameScenes.FLIGHT)
            { hasDetonated = true; } // Don't trigger explosions on scene changes.
        }

        void CASESetup(BaseField field, object obj)
        {
            if (externallyCalled) return;
            //CASEmass = ((origMass / 2) * CASELevel);
            CASEmass = (0.05f * CASELevel); //+50kg per level
            //part.mass = CASEmass;
            CASEcost = (CASELevel * 1000);
            //part.transform.localScale = (Vector3.one * (origScale + (CASELevel/10)));
            //Debug.Log("[BDArmory.ModuleCASE] part.mass = " + part.mass + "; CASElevel = " + CASELevel + "; CASEMass = " + CASEmass + "; Scale = " + part.transform.localScale);

            if (oldCaseLevel == 2 && CASELevel != oldCaseLevel)
            {
                int i = 0;
                using (IEnumerator<PartResource> resource = part.Resources.GetEnumerator())
                    while (resource.MoveNext())
                    {
                        if (resource.Current == null) continue;
                        //if (resource.Current.maxAmount < 80) //original value < 100, at risk of fractional amount
                        {
                            resource.Current.maxAmount = resourceAmount[i];
                        }
                        //else resource.Current.maxAmount = Math.Floor(resource.Current.maxAmount * 1.25);
                        i++;
                    }
            }
            if (oldCaseLevel != 2 && CASELevel == 2)
            {
                using (IEnumerator<PartResource> resource = part.Resources.GetEnumerator())
                    while (resource.MoveNext())
                    {
                        if (resource.Current == null) continue;
						resource.Current.maxAmount *= 0.8;
                        resource.Current.maxAmount = Math.Floor(resource.Current.maxAmount);
                        resource.Current.amount = Math.Min(resource.Current.amount, resource.Current.maxAmount);
                    }
            }
            using (List<Part>.Enumerator pSym = part.symmetryCounterparts.GetEnumerator())
                while (pSym.MoveNext())
                {
                    if (pSym.Current == null) continue;

                    var CASE = pSym.Current.FindModuleImplementing<ModuleCASE>();
                    if (CASE == null) continue;
                    CASE.externallyCalled = true;
                    CASE.CASELevel = CASELevel;
                    CASE.CASEmass = CASEmass;
                    CASE.CASEcost = CASEcost;

                    if (CASE.oldCaseLevel == 2 && CASE.CASELevel != CASE.oldCaseLevel)
                    {
                        using (IEnumerator<PartResource> resource = pSym.Current.Resources.GetEnumerator())
                            while (resource.MoveNext())
                            {
                                if (resource.Current == null) continue;
                                resource.Current.maxAmount = Math.Floor(resource.Current.maxAmount * 1.25);
                                resource.Current.amount = Math.Min(resource.Current.amount, resource.Current.maxAmount);
                            }
                    }
                    if (CASE.oldCaseLevel != 2 && CASE.CASELevel == 2)
                    {
                        using (IEnumerator<PartResource> resource = pSym.Current.Resources.GetEnumerator())
                            while (resource.MoveNext())
                            {
                                if (resource.Current == null) continue;
                                resource.Current.maxAmount *= 0.8;
                                resource.Current.amount = Math.Min(resource.Current.amount, resource.Current.maxAmount);
                            }
                    }
                    CASE.oldCaseLevel = CASELevel;
                    CASE.externallyCalled = false;
                    Utils.RefreshAssociatedWindows(pSym.Current);
                }
            oldCaseLevel = CASELevel;
            Utils.RefreshAssociatedWindows(part);
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;
            var internalmag = part.FindModuleImplementing<ModuleWeapon>();
            if (internalmag == null)
            {
                CASESetup(null, null); //don't apply mass/cost to weapons with integral ammo protection, assume it's baked into weapon mass/cost
            }
        }

        private List<PartResource> GetResources()
        {
            List<PartResource> resources = new List<PartResource>();

            foreach (PartResource resource in part.Resources)
            {
                if (!resources.Contains(resource)) { resources.Add(resource); }
            }
            return resources;
        }
        private void CalculateBlast()
        {
            foreach (PartResource resource in GetResources())
            {
                var resources = part.Resources.ToList();
                using (IEnumerator<PartResource> ammo = resources.GetEnumerator())
                    while (ammo.MoveNext())
                    {
                        if (ammo.Current == null) continue;
                        if (ammo.Current.resourceName == resource.resourceName)
                        {
                            ammoMass = ammo.Current.info.density;
                            ammoQuantity = ammo.Current.amount;
                            ammoExplosionYield += (((ammoMass * 1000) * ammoQuantity) / 10);
                        }
                    }
            }
            blastRadius = BlastPhysicsUtils.CalculateBlastRange(ammoExplosionYield * BDArmorySettings.EXP_DMG_MOD_BATTLE_DAMAGE);
        }
        public float GetBlastRadius()
        {
            CalculateBlast();
            return blastRadius;
        }
        public void DetonateIfPossible()
        {
            if (hasDetonated || part == null || part.vessel == null || !part.vessel.loaded || part.vessel.packed) return;
            hasDetonated = true; // Set hasDetonated here to avoid recursive calls due to ammo boxes exploding each other.
            var vesselName = vessel != null ? vessel.vesselName : null;
            Vector3 direction = default(Vector3);
            GetBlastRadius();
            if (ammoExplosionYield <= 0) return;
            if (CASELevel != 2) //a considerable quantity of explosives and propellants just detonated inside your ship
            {
                if (CASELevel == 0)
                {
                    ExplosionFx.CreateExplosion(part.transform.position, (float)ammoExplosionYield, explModelPath, explSoundPath, ExplosionSourceType.BattleDamage, 120, part, SourceVessel, "Ammunition (CASE-0)", direction, -1, false, part.mass + ((float)ammoExplosionYield * 10f), 1200 * BDArmorySettings.EXP_DMG_MOD_BATTLE_DAMAGE);
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ModuleCASE] CASE 0 explosion, tntMassEquivilent: " + ammoExplosionYield);
                }
                else
                {
                    direction = part.transform.up;
                    ExplosionFx.CreateExplosion(part.transform.position, ((float)ammoExplosionYield / 2), limitEdexploModelPath, explSoundPath, ExplosionSourceType.BattleDamage, 60, part, SourceVessel, "Ammunition (CASE-I)", direction, -1, false, part.mass + ((float)ammoExplosionYield * 10f), 600 * BDArmorySettings.EXP_DMG_MOD_BATTLE_DAMAGE);
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ModuleCASE] CASE I explosion, tntMassEquivilent: " + ammoExplosionYield + ", part: " + part + ", vessel: " + vesselName);
                }
            }
            else //if (CASELevel == 2) //blast contained, shunted out side of hull, minimal damage
            {
                ExplosionFx.CreateExplosion(part.transform.position, (float)ammoExplosionYield / 4f, shuntExploModelPath, explSoundPath, ExplosionSourceType.BattleDamage, 30, part, SourceVessel, "Ammunition (CASE-II)", direction, -1, true);
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ModuleCASE] CASE II explosion, tntMassEquivilent: " + ammoExplosionYield);
                Ray BlastRay = new Ray(part.transform.position, part.transform.up);
                var hits = Physics.RaycastAll(BlastRay, blastRadius, explosionLayerMask);
                if (hits.Length > 0)
                {
                    var orderedHits = hits.OrderBy(x => x.distance);
                    using (var hitsEnu = orderedHits.GetEnumerator())
                    {
                        while (hitsEnu.MoveNext())
                        {
                            RaycastHit hit = hitsEnu.Current;
                            Part hitPart = null;
                            KerbalEVA hitEVA = null;

                            if (FlightGlobals.currentMainBody == null || hit.collider.gameObject != FlightGlobals.currentMainBody.gameObject)
                            {
                                try
                                {
                                    hitPart = hit.collider.gameObject.GetComponentInParent<Part>();
                                    hitEVA = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                                }
                                catch (NullReferenceException e)
                                {
                                    Debug.LogError("[BDArmory.ModuleCASE]: NullReferenceException for AmmoExplosion Hit: " + e.Message + "\n" + e.StackTrace);
                                    continue;
                                }

                                if (hitPart == null || hitPart == part) continue;
                                if (ProjectileUtils.IsIgnoredPart(hitPart)) continue; // Ignore ignored parts.


                                if (hitEVA != null)
                                {
                                    hitPart = hitEVA.part;
                                    if (hitPart.rb != null)
                                        ApplyDamage(hitPart, hit);
                                    break;
                                }

                                if (hitPart.vessel != part.vessel)
                                {
                                    Vector3 dist = part.transform.position - hitPart.transform.position;

                                    Ray LoSRay = new Ray(part.transform.position, hitPart.transform.position - part.transform.position);
                                    RaycastHit LOShit;
                                    if (Physics.Raycast(LoSRay, out LOShit, dist.magnitude, explosionLayerMask))
                                    {
                                        if (FlightGlobals.currentMainBody == null || LOShit.collider.gameObject != FlightGlobals.currentMainBody.gameObject)
                                        {
                                            KerbalEVA eva = LOShit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                                            Part p = eva ? eva.part : LOShit.collider.gameObject.GetComponentInParent<Part>();
                                            if (p == hitPart)
                                            {
                                                ProjectileUtils.CalculateShrapnelDamage(hitPart, hit, 200, (float)ammoExplosionYield, dist.magnitude, this.part.vessel.GetName(), ExplosionSourceType.BattleDamage, part.mass);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    ApplyDamage(hitPart, hit);
                                }
                            }
                        }
                    }
                }
            }
            if (part.vessel != null) // Already in the process of being destroyed.
                part.Destroy();
        }
        private void ApplyDamage(Part hitPart, RaycastHit hit)
        {
            //hitting a vessel Part
            //No struts, they cause weird bugs :) -BahamutoD
            if (hitPart == null) return;
            if (hitPart.partInfo.name.Contains("Strut")) return;
            float explDamage;
            if (BDArmorySettings.BULLET_HITS)
            {
                BulletHitFX.CreateBulletHit(hitPart, hit.point, hit, hit.normal, false, 200, 3, null);
            }

            explDamage = 100;
            explDamage = Mathf.Clamp(explDamage, 0, ((float)ammoExplosionYield * 10));
            explDamage *= BDArmorySettings.EXP_DMG_MOD_BATTLE_DAMAGE;
            hitPart.AddDamage(explDamage);
            float armorToReduce = hitPart.GetArmorThickness() * 0.25f;
            hitPart.ReduceArmor(armorToReduce);

            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.ModuleCASE]" + hitPart.name + " damaged, armor reduced by " + armorToReduce);

            BDACompetitionMode.Instance.Scores.RegisterBattleDamage(SourceVessel, hitPart.vessel, explDamage);
        }

        void OnDestroy()
        {
            if (BDArmorySettings.BATTLEDAMAGE && BDArmorySettings.BD_AMMOBINS && BDArmorySettings.BD_VOLATILE_AMMO && HighLogic.LoadedSceneIsFlight && !(VesselSpawner.Instance != null && VesselSpawner.Instance.vesselsSpawning))
            {
                if (!hasDetonated) DetonateIfPossible();
            }
            GameEvents.onGameSceneSwitchRequested.Remove(HandleSceneChange);
        }

        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.Append(Environment.NewLine);
            var internalmag = part.FindModuleImplementing<ModuleWeapon>();
            if (internalmag != null)
            {
                output.AppendLine($" Has Intrinsic C.A.S.E. Type {CASELevel}");
            }
            else
            {
                output.AppendLine($"Can add Cellular Ammo Storage Equipment to reduce ammo explosion damage");
            }

            output.AppendLine("");

            return output.ToString();
        }
        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && !vessel.packed)
            {
                if (BDArmorySettings.BD_FIRES_ENABLED && BDArmorySettings.BD_FIRE_HEATDMG)
                {
                    if (this.part.temperature > 900) //ammo cooks off, part is too hot
                    {
                        if (!hasDetonated) DetonateIfPossible();
                    }
                }
            }
        }
    }
}

