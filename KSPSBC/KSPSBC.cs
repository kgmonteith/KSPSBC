/* KSPSBC, a simple mod for publishing game state
 * for the Steel Battalion controller script.
 * https://github.com/kgmonteith/KSPSBC
 * 
 * Changelog:
 * 20150515     Initial release, KSP 1.0.2 compatible
 */

using System;
using System.IO;
using System.IO.Pipes;
using System.Collections.Generic;
using UnityEngine;

namespace KSPSBC
{
    public class State
    {
        public static float idlePushFrequency = 3.0f;   // seconds
        public float lastPush = Time.time;
        public string mode = "flying";
        public Dictionary<string, bool> bools = new Dictionary<string, bool>
        {
            {"sas", false},
            {"rcs", false},
            {"lights", false},
            {"brakes", false},
            {"gears", false},
            {"stageLock", false},
            {"stageLock2", false},
            {"mapView", false},
            {"navBall", false},
            {"aeroOverlay", false},
            {"thermalOverlay", false},
            {"thermalGauges", false},
            {"evaHeadlamp", false},
            {"evaJetpack", false}
        };

        public State(string m)
        {
            this.mode = m;
        }

        public void initVessel(Vessel v)
        {
            this.lastPush = Time.time;
            if (v.isActiveVessel)
            {
                if (v.isEVA)
                {
                    this.mode = "eva";
                    KerbalEVA k = v.FindPartModulesImplementing<KerbalEVA>()[0];
                    this.bools["evaHeadlamp"] = k.lampOn;
                    this.bools["evaJetpack"] = k.JetpackDeployed;
                }
                else
                {
                    this.mode = "flying";
                }
                this.bools["sas"] = v.ActionGroups[KSPActionGroup.SAS];
                this.bools["rcs"] = v.ActionGroups[KSPActionGroup.RCS];
                this.bools["lights"] = v.ActionGroups[KSPActionGroup.Light];
                this.bools["gears"] = v.ActionGroups[KSPActionGroup.Gear];
                this.bools["brakes"] = v.ActionGroups[KSPActionGroup.Brakes];
                this.bools["mapView"] = MapView.MapIsEnabled;
                this.bools["stageLock"] = InputLockManager.IsLocked(ControlTypes.STAGING);
                this.bools["stageLock2"] = this.bools["stageLock"]; // oh god i'm so lazy
                this.bools["aeroOverlay"] = PhysicsGlobals.AeroForceDisplay;
                this.bools["thermalGauges"] = TemperatureGagueSystem.Instance.showGagues;
                this.bools["thermalOverlay"] = PhysicsGlobals.ThermalColorsDebug;
                
            }
            this.bools["navBall"] = FlightUIModeController.Instance.navBall.expanded;
            this.writeToFile();
        }

        public void initBuilding()
        {
            this.mode = "building";
            this.writeToFile();
        }
        
        public void writeToFile() {
            // File-based IPC, because Mono .NET subset don't do named pipes
            // (and i'll be damned if i'm screwing around with sockets for something this light)
            string path = @".KSPSBC_state.txt";
            using (StreamWriter sw = File.CreateText(path))
            {
                sw.WriteLine("mode=" + this.mode);
                foreach (KeyValuePair<string, bool> entry in this.bools)
                {
                    sw.WriteLine(entry.Key + "=" + entry.Value);
                }
            }
            try
            {
                File.Copy(path, @"KSPSBC_state.txt", true);
            }
            catch (Exception)
            {
                // Collision... meh. try again in a few seconds.
            }
            
        }
    }
    

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class KSPSBC_flight : MonoBehaviour
    {
        State state = new State("flying");
        public void Start()
        {
            print("KSPSBC: Plugin launched");
            GameEvents.onVesselChange.Add(state.initVessel);
        }

        public void Update()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v.ActionGroups[KSPActionGroup.SAS] != state.bools["sas"] ||
                v.ActionGroups[KSPActionGroup.Light] != state.bools["lights"] ||
                v.ActionGroups[KSPActionGroup.Gear] != state.bools["gears"] ||
                v.ActionGroups[KSPActionGroup.Brakes] != state.bools["brakes"] ||
                v.ActionGroups[KSPActionGroup.RCS] != state.bools["rcs"] ||
                MapView.MapIsEnabled != state.bools["mapView"] ||
                FlightUIModeController.Instance.navBall.expanded != state.bools["navBall"] ||
                InputLockManager.IsLocked(ControlTypes.STAGING) != state.bools["stageLock"] ||
                PhysicsGlobals.AeroForceDisplay != state.bools["aeroOverlay"] ||
                TemperatureGagueSystem.Instance.showGagues != state.bools["thermalGauges"] ||
                PhysicsGlobals.ThermalColorsDebug!= state.bools["thermalOverlay"] ||
                ((Time.time - state.lastPush) > State.idlePushFrequency))
            {
                state.initVessel(v);
            }
            else if (v.isEVA)
            {
                KerbalEVA k = v.FindPartModulesImplementing<KerbalEVA>()[0];
                if (k.lampOn != state.bools["evaHeadlamp"] ||
                    k.JetpackDeployed != state.bools["evaJetpack"])
                {
                    state.initVessel(v);
                }
            }
        }
    }


    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    class KSPSBC_vab : MonoBehaviour
    {
        State state = new State("building");

        private void Start()
        {
            print("KSPSBC: Started building mode...");
            // Switch to editor controls
            state.initBuilding();
        }
    }

}
