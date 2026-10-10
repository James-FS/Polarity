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
        private void LateUpdate()
        {
            if(turret==null) return;
            Vector2 direction=turret.AimDirection;
            if(barrel!=null) barrel.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            if(warningLine==null) return;
            warningLine.enabled=turret.Phase!=TurretPhase.Cooling;
            if(!warningLine.enabled) return;
            Vector3 origin=turret.MuzzlePosition;
            warningLine.SetPosition(0,origin);
            warningLine.SetPosition(1,origin+(Vector3)direction*8f);
            Color color=turret.Phase==TurretPhase.Locked ? new Color(1f,0.15f,0.1f,0.95f) : new Color(1f,0.65f,0.1f,0.55f+turret.WarningProgress*0.35f);
            warningLine.startColor=warningLine.endColor=color;
        }
        private void OnDisable() { if(warningLine!=null) warningLine.enabled=false; }
    }
}
