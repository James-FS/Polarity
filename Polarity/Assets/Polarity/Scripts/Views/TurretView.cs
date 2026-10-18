using Polarity.Entities;
using UnityEngine;
namespace Polarity.Views
{
    /// <summary>Reads aim/lock state; rotating only the barrel never changes the footprint.</summary>
    [DisallowMultipleComponent]
    public sealed class TurretView : MonoBehaviour
    {
        [SerializeField] private Turret turret;
        [SerializeField] private Transform barrel;
        [SerializeField] private LineRenderer warningLine;
        [SerializeField] private SpriteRenderer rotatingHead;
        [Tooltip("East, northeast, north, northwest, west, southwest, south, southeast.")]
        [SerializeField] private Sprite[] directionalHeads = new Sprite[0];
        [SerializeField] private SpriteRenderer muzzleGlow;
        private int observedShots;
        private float flashRemaining;

        private void OnEnable()
        {
            observedShots = turret != null ? turret.ShotsFired : 0;
            flashRemaining = 0f;
        }

        private void LateUpdate()
        {
            if(turret==null) return;
            Vector2 direction=turret.AimDirection;
            float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
            if(barrel!=null) barrel.localRotation=Quaternion.Euler(0,0,angle);
            if(rotatingHead!=null && directionalHeads!=null && directionalHeads.Length==8)
            {
                int facing=(Mathf.RoundToInt(angle/45f)+8)%8;
                if(directionalHeads[facing]!=null)
                {
                    rotatingHead.sprite=directionalHeads[facing];
                    // Artwork preserves its upright perspective; only the residual angle rotates.
                    // Each sprite pivot is authored so its muzzle is 1.1D along its facing.
                    rotatingHead.transform.localRotation=Quaternion.Euler(0,0,-facing*45f);
                }
            }
            if(muzzleGlow!=null)
            {
                flashRemaining=Mathf.Max(0f,flashRemaining-Time.deltaTime);
                if(turret.ShotsFired!=observedShots)
                {
                    if(turret.ShotsFired>observedShots) flashRemaining=0.08f;
                    observedShots=turret.ShotsFired;
                }
                muzzleGlow.transform.position=turret.MuzzlePosition;
                float size=flashRemaining>0f ? 0.22f : 0.09f+turret.WarningProgress*0.04f;
                muzzleGlow.transform.localScale=new Vector3(size,size,1f);
                muzzleGlow.color=new Color(0.3f,0.85f,1f,flashRemaining>0f ? 0.95f : 0.35f+turret.WarningProgress*0.3f);
            }
            if(warningLine==null) return;
            warningLine.enabled=turret.Phase!=TurretPhase.Cooling;
            if(!warningLine.enabled) return;
            Vector3 origin=turret.MuzzlePosition;
            warningLine.SetPosition(0,origin);
            warningLine.SetPosition(1,origin+(Vector3)direction*8f);
            Color color=turret.Phase==TurretPhase.Locked ? new Color(1f,0.15f,0.1f,0.95f) : new Color(1f,0.65f,0.1f,0.55f+turret.WarningProgress*0.35f);
            warningLine.startColor=warningLine.endColor=color;
        }
        private void OnDisable()
        {
            if(warningLine!=null) warningLine.enabled=false;
            flashRemaining=0f;
        }
    }
}
