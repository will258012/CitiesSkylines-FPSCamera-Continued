﻿namespace FPSCamera.Cam
{
    using Config;
    using CSkyL;
    using CSkyL.Game.ID;
    using CSkyL.Game.Object;
    using CSkyL.Game.Utils;
    using CSkyL.Transform;
    using System.Linq;

    public class WalkThruCam : FollowCam, ICamUsingTimer
    {
        public override ObjectID TargetID => _currentCam.TargetID;
        public override IObjectToFollow Target => _currentCam.Target;

        public void SwitchTarget() => _SetRandomCam();
        public void ElapseTime(float seconds) => _elapsedTime += seconds;
        public float GetElapsedTime() => _elapsedTime;

        public override bool Validate()
        {
            if (!IsOperating) return false;

            var status = _currentCam?.Validate() ?? false;
            if (!Config.instance.ManualSwitch4Walk &&
                _elapsedTime > Config.instance.Period4Walk) status = false;
            if (!status) {
                _SetRandomCam();
                status = _currentCam?.Validate() ?? false;
                if (!status) {
                    Log.Warn("no target for Walk-Thru mode");
                    ColossalFramework.Singleton<AudioManager>.instance.PlaySound(disabledClickSound);
                }
            }
            return status;
        }
        private readonly UnityEngine.AudioClip disabledClickSound = UnityEngine.Object.FindObjectOfType<ColossalFramework.UI.UIView>().defaultDisabledClickSound;
        public override void SimulationFrame() => _currentCam?.SimulationFrame();
#if DEBUG
        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
            => _currentCam?.RenderOverlay(cameraInfo);
#endif
        public override Positioning GetPositioning() => _currentCam.GetPositioning();
        public override float GetSpeed() => _currentCam.GetSpeed();
        public override void InputOffset(Offset inputOffset)
            => _currentCam.InputOffset(inputOffset);
        public override void InputReset() => _currentCam.InputReset();
        public override string GetTargetStatus() => _currentCam.GetTargetStatus();
        public override GameUtil.Infos GetTargetInfos() => _currentCam.GetTargetInfos();
        public override string SaveOffset() => _currentCam.SaveOffset();

        private void _SetRandomCam()
        {
            _currentCam = null;
            Log.Msg(" -- switching target");

            var list = Vehicle.GetIf((v) => {
                switch (v) {
                case PersonalVehicle _: return Config.instance.SelectDriving;
                case TransitVehicle _: return Config.instance.SelectPublicTransit;
                case ServiceVehicle _:
                case MissionVehicle _:
                    return Config.instance.SelectService;
                case CargoVehicle _: return Config.instance.SelectCargo;
                default:
                    Log.Warn("WalkThru selection: unknow vehicle type:"
                             + v.GetPrefabName());
                    return false;
                }
            }).OfType<Object>().Concat(
                       Pedestrian.GetIf((p) => {
                           if (p.IsHangingAround) return false;
                           switch (Object.Of(p.RiddenVehicleID)) {
                           case TransitVehicle _: return Config.instance.SelectPassenger;
                           case PersonalVehicle _: return false;    // already selected by Vehicle
                           case Vehicle v:
                               Log.Warn("WalkThru selection: unknow pedestrian type: on a "
                                        + v.GetPrefabName());
                               return false;
                           default:
                               return p.IsWaitingTransit ? Config.instance.SelectWaiting
                                                         : Config.instance.SelectPedestrian;
                           }
                       }).OfType<Object>()).ToList();
            if (!list.Any()) return;

            int attempt = 3;
            do _currentCam = Follow(list.GetRandomOne().ID);
            while (!(_currentCam?.Validate() ?? false) && --attempt >= 0);
            _elapsedTime = 0f;
        }

        private FollowCam _currentCam;
        private float _elapsedTime;
    }
}
