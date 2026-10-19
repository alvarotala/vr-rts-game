using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    // Small procedural rig. Shared meshes/materials, no Animator or per-shot objects.
    // Only visual children move: simulation positions, collision and attack cadence stay authoritative.
    public sealed class SoldierAnimation
    {
        private readonly Transform root, body, weapon, leftLeg, rightLeg;
        private readonly Archetype type;
        private readonly Vector3 weaponRest;
        private readonly float scale;
        private readonly Transform[] insectLegs;
        private readonly Quaternion[] insectRest;
        private readonly Mesh insectMesh;
        private readonly Vector3[] limbVertices,limbNormals,legVertices,legNormals;
        private float gaitTime=float.NaN,gaitAmount=-1;
        private Point previousPosition;
        private int previousTick;
        private float speed;
        public Renderer[] Renderers { get; }

        public SoldierAnimation(Transform root, Transform body, Archetype type, UnitVisuals meshes, Material[] materials)
        {
            this.root = root; this.body = body; this.type = type; scale = type.collisionRadius / .45f;
            weaponRest = (type.role == "melee" ? new Vector3(.32f, 1, .02f) : type.role == "ranged" ? new Vector3(.17f, .74f, .15f)
                : type.role == "siege" ? new Vector3(0, .5f, -.06f) : type.role == "caster" ? new Vector3(0,.82f,.24f) : new Vector3(0, .34f, .28f)) * scale;
            if(type.faction=="biological")weaponRest=BiologicalVisuals.WeaponPosition(type.role)*scale;
            weapon = Part("Animated weapon", body, meshes.CombatPart(type, false)); weapon.localPosition = weaponRest;
            if(type.faction=="biological")
            {
                int count=BiologicalVisuals.LegPairs(type)*2;insectLegs=new Transform[count];insectRest=new Quaternion[count];
                var limb=meshes.CombatPart(type,true);limbVertices=limb.vertices;limbNormals=limb.normals;
                legVertices=new Vector3[limbVertices.Length*count];legNormals=new Vector3[legVertices.Length];
                var colors=new Color[legVertices.Length];var limbColors=limb.colors;
                for(int i=0;i<count;i++)
                {
                    int pair=i/2,side=i%2==0?-1:1;
                    var leg=new GameObject(i==0?"Left stride":i==1?"Right stride":"Insect stride "+i).transform;leg.SetParent(root,false);
                    leg.localPosition=BiologicalVisuals.LegPosition(type,pair,side)*scale;
                    leg.localRotation=insectRest[i]=BiologicalVisuals.LegRotation(pair,side);insectLegs[i]=leg;
                    System.Array.Copy(limbColors,0,colors,i*limbVertices.Length,limbVertices.Length);
                }
                // All six/eight limbs share one renderer; do not multiply draw
                // submissions by the number of legs on a full Quest army.
                insectMesh=new Mesh{name="Batched insect legs / "+type.id,subMeshCount=3};insectMesh.MarkDynamic();
                insectMesh.vertices=legVertices;insectMesh.colors=colors;
                for(int sub=0;sub<3;sub++)
                {
                    var indices=limb.GetTriangles(sub);var combined=new int[indices.Length*count];
                    for(int i=0;i<count;i++)for(int j=0;j<indices.Length;j++)combined[i*indices.Length+j]=indices[j]+i*limbVertices.Length;
                    insectMesh.SetTriangles(combined,sub);
                }
                var legs=Part("Insect legs",root,insectMesh);
                Renderers=new[]{body.GetComponent<Renderer>(),weapon.GetComponent<Renderer>(),legs.GetComponent<Renderer>()};
                UpdateInsectMesh();
            }
            else if (type.role == "melee" || type.role == "ranged")
            {
                leftLeg = Part("Left stride", root, meshes.CombatPart(type, true));
                rightLeg = Part("Right stride", root, meshes.CombatPart(type, true));
                float height = type.role == "melee" ? .48f : .44f, spread = type.role == "melee" ? .15f : .12f;
                leftLeg.localPosition = new Vector3(-spread, height, 0) * scale; rightLeg.localPosition = new Vector3(spread, height, 0) * scale;
                Renderers = new[] { body.GetComponent<Renderer>(), weapon.GetComponent<Renderer>(), leftLeg.GetComponent<Renderer>(), rightLeg.GetComponent<Renderer>() };
            }
            else Renderers = new[] { body.GetComponent<Renderer>(), weapon.GetComponent<Renderer>() };
            Transform Part(string name, Transform parent, Mesh mesh)
            {
                var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.transform.SetParent(parent, false);
                part.GetComponent<MeshFilter>().sharedMesh = mesh; var renderer = part.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return part.transform;
            }
        }

        public void Reset(Point position, int tick)
        {
            previousPosition = position; previousTick = tick; speed = 0;
            body.localPosition = Vector3.zero; body.localRotation = Quaternion.identity;
            weapon.localPosition = weaponRest; weapon.localRotation = Quaternion.identity;
            if (leftLeg != null) leftLeg.localRotation = rightLeg.localRotation = Quaternion.identity;
            if(insectLegs!=null)for(int i=0;i<insectLegs.Length;i++)insectLegs[i].localRotation=insectRest[i];
            if(insectMesh!=null){gaitTime=float.NaN;gaitAmount=-1;UpdateInsectMesh();}
        }

        public void Apply(VisibleEntity entity, int tick, float visualTick, AttackEvent? attack, Vector3? aim)
        {
            if (tick != previousTick)
            {
                float elapsed = Mathf.Max(1, tick - previousTick) / (float)Simulation.TickRate;
                speed = entity.position.Distance(previousPosition) / elapsed;
                previousPosition = entity.position; previousTick = tick;
            }
            float moving = Mathf.Clamp01(speed / Mathf.Max(.1f, type.speed));
            float stride = Mathf.Sin(visualTick / Simulation.TickRate * 12 + entity.id * .37f) * moving;
            float bob = Mathf.Abs(stride) * (leftLeg != null ? .055f : .025f);
            float age = attack.HasValue ? (visualTick - attack.Value.tick) / Simulation.TickRate : -1;
            bool melee = type.role == "melee";
            float duration = melee ? .4f : type.role == "siege" ? .55f : .34f;
            bool striking = age >= 0 && age < duration;
            float recoil = striking ? Mathf.Exp(-age * 12) * (1 - Mathf.Exp(-age * 90)) : 0;
            float lunge = striking && melee ? (age < .07f ? Mathf.SmoothStep(0, 1, age / .07f) : 1 - Mathf.SmoothStep(0, 1, (age - .07f) / .33f)) : 0;
            // Upper-body aim stays on the observed target while feet follow the
            // smooth locomotion turn. Never rotate a gun away from its own tracer.
            var aiming = striking && aim.HasValue && aim.Value.sqrMagnitude > .001f
                ? Quaternion.Inverse(root.localRotation) * Quaternion.LookRotation(aim.Value) : Quaternion.identity;
            body.localPosition = aiming * new Vector3(0, bob, melee ? lunge * .28f : -recoil * (type.role == "siege" ? .13f : .07f)) * scale;
            body.localRotation = aiming * Quaternion.Euler(melee ? lunge * 13 : -recoil * 9, 0, -stride * 3);
            weapon.localPosition = weaponRest + Vector3.back * recoil * (melee ? 0 : type.role == "siege" ? .23f : .15f) * scale;
            if (striking && melee)
            {
                float sweep = age < .1f ? Mathf.Lerp(-70, 65, Mathf.SmoothStep(0, 1, age / .1f)) : Mathf.Lerp(65, 0, Mathf.SmoothStep(0, 1, (age - .1f) / .3f));
                weapon.localRotation = Quaternion.Euler(-lunge * 18, sweep, -lunge * 22);
            }
            else weapon.localRotation = Quaternion.Euler(-recoil * (type.role == "siege" ? 13 : 7), 0, melee ? stride * 10 : 0);
            if (leftLeg != null)
            {
                leftLeg.localRotation = Quaternion.Euler(stride * 28 - lunge * 13, 0, 0);
                rightLeg.localRotation = Quaternion.Euler(-stride * 28 + lunge * 18, 0, 0);
            }
            if(insectLegs!=null&&(gaitAmount!=moving||moving>0&&gaitTime!=visualTick))
            {
                for(int i=0;i<insectLegs.Length;i++)
                {
                    float step=Mathf.Sin(visualTick/Simulation.TickRate*12+entity.id*.37f+(i%2+i/2)*Mathf.PI)*moving;
                    insectLegs[i].localRotation=insectRest[i]*Quaternion.Euler(-Mathf.Max(0,step)*28,step*22,0);
                }
                UpdateInsectMesh();gaitTime=visualTick;gaitAmount=moving;
            }
        }

        private void UpdateInsectMesh()
        {
            for(int leg=0;leg<insectLegs.Length;leg++)
            {
                var rotation=insectLegs[leg].localRotation;var position=insectLegs[leg].localPosition;
                for(int v=0;v<limbVertices.Length;v++)
                {
                    int index=leg*limbVertices.Length+v;legVertices[index]=position+rotation*limbVertices[v];legNormals[index]=rotation*limbNormals[v];
                }
            }
            insectMesh.SetVertices(legVertices);insectMesh.SetNormals(legNormals);insectMesh.RecalculateBounds();
        }
        public void Dispose()
        {
            if(insectMesh!=null){if(Application.isPlaying)Object.Destroy(insectMesh);else Object.DestroyImmediate(insectMesh);}
        }

        public Vector3 Muzzle(int shotId)
        {
            var tip = type.role == "ranged" ? new Vector3(0, 0, .57f) : type.role == "siege" ? new Vector3((shotId % 2 == 0 ? -1 : 1) * .15f, .05f, .83f)
                : type.role == "melee" ? new Vector3(.07f, -.22f, .9f) : new Vector3(0, 0, .26f);
            return weapon.TransformPoint(tip * scale);
        }
    }
}
