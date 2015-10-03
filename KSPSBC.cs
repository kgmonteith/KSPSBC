/* Kerbal Space Program SBC config file
 * https://github.com/kgmonteith/KSPSBC
 *
 * For use with Steel-Batallion-64 driver/wrapper by HackNFly
 * https://sourceforge.net/projects/steel-batallion-64/
 * 
 * Changelog:
 * 20150515     Initial release, KSP 1.0.2 / SBC 3.0 compatible 
 */

using SBC;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SBC {
	public class DynamicClass	{
        // REQUIRED: Set this to your KSP installation path!
        string kspPath = @"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program";

        // Modify these values to your liking
        const bool warnOnMissingStateFile = true;
        const bool getDeadzonesFromSettingsFile = true;
        const bool enableStageLockWarning = true;
        const bool useExtendedFunctionKeys = false; // Not implemented properly this release, wait for it

        DeadzoneAlertMode displayDeadzoneAlerts = DeadzoneAlertMode.both; // options: none, toggleOn, toggleOff, both

        const bool randomFlashingLights = true; // turn on random flashing in flight mode, for TOTAL IMMERSION
        const int timeBetweenFlashesLow = 20 * 1000; // milliseconds
        const int timeBetweenFlashesHigh = 40 * 1000; // milliseconds

        

        /*
         * 
         * You shouldn't have to change anything below here, unless you really want to
         * 
         */


        // Common switch bindings, rearrange at will
        public Dictionary<string, ButtonEnum> bindings = new Dictionary<string, ButtonEnum>
        {
            {"steeringLock", ButtonEnum.ToggleFuelFlowRate},
            {"throttleLock", ButtonEnum.ToggleBufferMaterial},
            {"swapYawRoll", ButtonEnum.ToggleVTLocation},
            {"docking", ButtonEnum.ToggleOxygenSupply},
            {"precisionControls", ButtonEnum.ToggleFilterControl},

            // I really wouldn't touch this stuff, though
            {"functionShift", ButtonEnum.Extinguisher}
        };


        string stateFile = @"KSPSBC_state.txt";
        string settingsFile = @"settings.cfg";
        const int statePollTime = 500; // milliseconds


        static SteelBattalionController controller;
        vJoy joystick;
        bool acquired;
        static String debugString = "";
        const int refreshRate = 15; // Number of milliseconds between call to mainLoop


        int statePoll = statePollTime;
		const int maxAxisValue = 32768;

        static int lowIntensity = 0;
        static int baseLineIntensity = 15; // MAX BRIGHT
        static int funcShiftIntensity = 8;

        bool sasDepressed = false;
        bool sasToggled = false;
        bool mapView = false;
        bool stateFileDetected = !warnOnMissingStateFile;
        bool throttleDepressed = false;
        bool brakeDepressed = false;
        //bool rcsActive = false;
        bool dockingMode = false; // false = staging, true = docking
        bool steeringActive = false;
        bool funcShiftSaved = false;
        int tunerValueSaved = 0;
        const int tunerChangeCountdownDuration = 45; // milliseconds
        int tunerChangeCountdown = tunerChangeCountdownDuration;
        static Random random = new Random();
        int timeUntilRandomFlash = random.Next(timeBetweenFlashesLow, timeBetweenFlashesHigh);

        ButtonEnum[] randomLightCandidates = {
            ButtonEnum.Washing,
            ButtonEnum.Extinguisher,
            ButtonEnum.WeaponConSub,
            ButtonEnum.FunctionOverride,
            ButtonEnum.FunctionManipulator,
            ButtonEnum.WeaponConMagazine,
            ButtonEnum.MultiMonModeSelect,
            ButtonEnum.MultiMonSubMonitor,
            ButtonEnum.MainMonZoomIn,
            ButtonEnum.MainMonZoomOut
        };
        
        // Global flashers
        FlashingLight sasLight;
        FlashingLight stagingLight;
        FlashingLight noStateFileLight;

        FlashingLight aimingXWarning;
        bool aimingXWarnState = false;
        double aimingXDeadzone = 0.15;
        FlashingLight aimingYWarning;
        bool aimingYWarnState = false;
        double aimingYDeadzone = 0.15;
        FlashingLight rightMiddlePedalWarning;
        bool rightMiddlePedalWarnState = false;
        double rightMiddlePedalDeadzone = 0.15;
        FlashingLight leftPedalWarning;
        bool leftPedalWarnState = false;
        double leftPedalDeadzone = 0.01;
        FlashingLight rAxisWarning;
        bool rAxisWarnState = false;
        double rAxisDeadzone = 0.15;

        enum DeadzoneAlertMode {
            none = 0,
            toggleOff = 1,
            toggleOn  = 2,
            both = 3
        }

        enum ControlMode
        {
            flying = -1,
            building = -2,
            eva = 1,
            automatic = 5
        };
        ControlMode controlMode;
        int savedGearLever = -1;
        enum StateMode
        {
            automatic,
            manual
        }
        StateMode stateMode;

        // Bindings for state-aware lights
        Dictionary<string, ControllerLEDEnum> automodeLights = new Dictionary<string, ControllerLEDEnum>() {
            {"gears", ControllerLEDEnum.LineColorChange},
            {"lights", ControllerLEDEnum.NightScope},
            {"brakes", ControllerLEDEnum.F3},
            {"mapView", ControllerLEDEnum.OpenClose},
            {"rcs", ControllerLEDEnum.MainWeaponControl},
            {"navBall", ControllerLEDEnum.MapZoomInOut},
            {"aeroOverlay", ControllerLEDEnum.ForecastShootingSystem},
            {"thermalOverlay", ControllerLEDEnum.TankDetach},
            {"thermalGauges", ControllerLEDEnum.F1},
            {"stageLock2", ControllerLEDEnum.Chaff},
            {"evaHeadlamp", ControllerLEDEnum.Chaff},
            {"evaJetpack", ControllerLEDEnum.MainWeaponControl},
        };


        // Completely awful function key implementation.
        // I hate it, but it works.
        const int basicFunctionButtonCt = 8;
        SBC.Key[] functionKeys = {
            // Basic function keys
			SBC.Key.D1,
			SBC.Key.D2,
			SBC.Key.D3,
			SBC.Key.D4,
			SBC.Key.D5,
			SBC.Key.F10,
			SBC.Key.F11,
			SBC.Key.F12,
            // Extended function keys
            /*
            SBC.Key.B,
			SBC.Key.U,
			SBC.Key.G,
             */
            // Basic function keys, with function shift depressed
            
			SBC.Key.D6,
			SBC.Key.D7,
			SBC.Key.D8,
			SBC.Key.D9,
			SBC.Key.D0,
			SBC.Key.LeftBracket,
			SBC.Key.RightBracket,
			SBC.Key.F1,
            // Extended function keys, with function shift depressed
            /*
			SBC.Key.F6,
			SBC.Key.F7,
			SBC.Key.F8,
             */
		};

        public struct FunctionLight
        {
            public bool toggle;
            public int savedState;

            public FunctionLight(bool t, int ss)
            {
                toggle = t;
                savedState = ss;
            }
        }

        FunctionLight[] functionLights = {
            // Basic function key lights
            new FunctionLight(true, 0),
            new FunctionLight(true, 0),
            new FunctionLight(true, 0),
            new FunctionLight(true, 0),
            new FunctionLight(true, 0),
            new FunctionLight(false, 15),
            new FunctionLight(false, 0),
            new FunctionLight(false, 0),
            // Extended function key lights
            /*
            new FunctionLight(false, 0),
            new FunctionLight(false, 0),
            new FunctionLight(false, 15),
             * */
        };

        ButtonEnum[] functionButtons = {
            // Basic function buttons
			ButtonEnum.Comm1,
			ButtonEnum.Comm2,
			ButtonEnum.Comm3,
			ButtonEnum.Comm4, 
			ButtonEnum.Comm5,
			ButtonEnum.FunctionF1, 
			ButtonEnum.FunctionTankDetach,
			ButtonEnum.FunctionFSS,
            // Extended function buttons
            /*
            ButtonEnum.FunctionF3, 
			ButtonEnum.FunctionNightScope,
			ButtonEnum.FunctionLineColorChange,
             */
		};

        List<SBC.Key> funcShiftSavedKeys = new List<SBC.Key>();

        ControllerLEDEnum[] gearLights = {
            ControllerLEDEnum.GearR,
            ControllerLEDEnum.GearN,
            ControllerLEDEnum.Gear1,
            ControllerLEDEnum.Gear2,
            ControllerLEDEnum.Gear3,
            ControllerLEDEnum.Gear4,
		    ControllerLEDEnum.Gear5
        };

        // Container for active flashers
        static List<FlashingLight> flashingLights = new List<FlashingLight>();

        // Unthreaded flashing light class, requires polling to update state
        // Now supports multi-level flashing lights! Give it a list of <intensity, duration> tuples
        // Check the sasLight initialization in Initiliaze() for an example.
        public class FlashingLight
        {
            public ControllerLEDEnum light;
            public ButtonEnum button;
            public int iterations;
            public int lowLevel;
            public int highLevel;
            public List<Tuple<int, int>> lightStates;  // index 0 is lowest state -- tuple order: <intensity, duration>

            int duration;
            int lightLevel = 0; // index of lightState; 0 == lowest
            public FlashingLight(ButtonEnum b, List<Tuple<int, int>> ls, int i)
            {
                this.light = controller.GetLightForButton(b);
                this.button = b;
                this.lightStates = ls;
                this.iterations = i;

                this.duration = this.lightStates[0].Item2;
                controller.SetLEDState(this.light, this.lightStates[0].Item1);
            }

            public FlashingLight(ButtonEnum b, int ll, int ld, int hl, int hd, int i)
            {
                this.light = controller.GetLightForButton(b);
                this.button = b;
                this.lightStates = new List<Tuple<int, int>>() {
                    new Tuple<int, int>(ll, ld),
                    new Tuple<int, int>(hl, hd)
                };
                this.iterations = i;

                this.duration = this.lightStates[0].Item2;
                controller.SetLEDState(this.light, this.lightStates[0].Item1);
            }

            public bool poll(int elapsed)
            {
                this.duration -= elapsed;
                if (this.duration <= 0)
                {
                    if (this.iterations > 0 && this.lightLevel == this.lightStates.Count - 1)
                    {
                        --this.iterations;
                    }
                    else if (this.iterations == 0)
                    {
                        controller.SetLEDState(this.light, this.lightStates[0].Item1);
                        return true;
                    }
                    // Change LED state
                    if (!controller.GetButtonState((int)button))
                    {
                        this.lightLevel++;
                        if (this.lightLevel >= this.lightStates.Count)
                        {
                            this.lightLevel = 0;
                        }
                        controller.SetLEDState(this.light, this.lightStates[lightLevel].Item1);
                        this.duration = this.lightStates[lightLevel].Item2;
                    }
                }
                return false;
            }

            public void reset()
            {
                this.duration = this.lightStates[0].Item2;
                controller.SetLEDState(this.light, this.lightStates[0].Item1);
            }
        }


        // This gets called once by main program
        public void Initialize() {
            controller = new SteelBattalionController();
			controller.Init(refreshRate); // 50 is refresh rate in milliseconds

            if (!kspPath[kspPath.Length - 1].Equals(@"\"))
            {
                // Because even I've screwed this up a bunch of times...
                kspPath += @"\";
            }
            // Initialize flashing lights
            sasLight = new FlashingLight(ButtonEnum.CockpitHatch, new List<Tuple<int, int>>()
            {
                new Tuple<int,int>(8, 500),
                new Tuple<int,int>(12, 500),
                // ... etc ...
            } , -1);
            stagingLight = new FlashingLight(ButtonEnum.Start, 8, 500, 12, 500, -1);
            noStateFileLight = new FlashingLight(ButtonEnum.WeaponConMagazine, 4, 250, 15, 250, -1);
            aimingXWarning = new FlashingLight(ButtonEnum.Comm5, 15, 200, 0, 200, -1);
            aimingYWarning = new FlashingLight(ButtonEnum.Comm4, 15, 200, 0, 200, -1);
            rightMiddlePedalWarning = new FlashingLight(ButtonEnum.Comm3, 15, 200, 0, 200, -1);
            leftPedalWarning = new FlashingLight(ButtonEnum.Comm2, 15, 200, 0, 200, -1);
            rAxisWarning = new FlashingLight(ButtonEnum.Comm1, 15, 200, 0, 200, -1);

            if (File.Exists(kspPath + stateFile))
            {
                stateFileDetected = true;
            }

            // Start with flight mode controls, because we'll detect a change on update anyway
            flyingInit();
            tunerValueSaved = controller.TunerDial;
            int gearLever = controller.GearLever;
            if (gearLever == 5)
            {
                stateMode = StateMode.automatic;
            } else {
                stateMode = StateMode.manual;
            }

            // Figure out deadzones, if we can
            if (getDeadzonesFromSettingsFile)
            {
                readSettingsFile();
            }

			joystick = new vJoy();
			acquired = joystick.acquireVJD(1);
			joystick.resetAll();
		}

        // Common keys used in both flight and EVA
        public void flyingEvaCommonKeys(bool resetFunctionButtons)
        {

            // Right block
            controller.AddButtonKeyLightMapping(ButtonEnum.MultiMonOpenClose, false, 15, SBC.Key.M, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.WeaponConSub, true, 15, SBC.Key.V, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.MainMonZoomIn, true, 15, SBC.Key.NumPadPlus, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.MainMonZoomOut, true, 15, SBC.Key.NumPadMinus, true);
            controller.AddButtonKeyMapping(ButtonEnum.RightJoyFire, SBC.Key.F, true);
            controller.AddButtonKeyMapping(ButtonEnum.RightJoyMainWeapon, SBC.Key.Space, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.MultiMonModeSelect, true, 15, SBC.Key.F5, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.MultiMonSubMonitor, true, 15, SBC.Key.F9, true);

            // Center block
            controller.AddButtonKeyLightMapping(ButtonEnum.WeaponConMain, false, 15, SBC.Key.R, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.WeaponConMagazine, true, 15, SBC.Key.LeftAlt, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Chaff, false, 15, SBC.Key.L, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Washing, true, 15, SBC.Key.Escape, true);

            // Left block
            controller.AddButtonKeyMapping(ButtonEnum.LeftJoySightChange, SBC.Key.C, true);

            if (resetFunctionButtons)
            {
                for (int i = 0; i < functionButtons.Length; i++)
                {
                    controller.AddButtonKeyLightMapping(functionButtons[i], functionLights[i].toggle, 15, functionKeys[i], true);
                    controller.SetLEDState(controller.GetLightForButton(functionButtons[i]), functionLights[i].savedState);
                }
            }
        }

        // Initialize keys for flying mode
        public void flyingInit()
        {
            bool resetFunctionButtons = !(controlMode == ControlMode.flying || controlMode == ControlMode.eva);
            controlMode = ControlMode.flying;
            removeAllButtonAssignments(resetFunctionButtons);
            flashingLights.Clear();
            sasToggled = false;

            flyingEvaCommonKeys(resetFunctionButtons);

            // Simple button bindings
            // Right block
            controller.AddButtonKeyLightMapping(ButtonEnum.Eject, true, 15, SBC.Key.BackSlash, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.CockpitHatch, false, 15, SBC.Key.T, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Ignition, true, 15, SBC.Key.Z, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Start, true, 15, SBC.Key.Space, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.MultiMonMapZoomInOut, false, 15, SBC.Key.NumPadPeriod, true);
            controller.AddButtonKeyMapping(ButtonEnum.RightJoyLockOn, SBC.Key.X, true);

            // Center block
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionLineColorChange, false, 15, SBC.Key.G, true);
            controller.SetLEDState(controller.GetLightForButton(ButtonEnum.FunctionLineColorChange), 15); // Landing gears begin deployed 
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionNightScope, false, 15, SBC.Key.U, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionF3, true, 15, SBC.Key.B, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionF2, true, 15, SBC.Key.F2, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionOverride, true, 15, SBC.Key.F3, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionManipulator, true, 15, SBC.Key.F4, true);
            // Weapons

            flashingLights.Add(sasLight);
        }

        public void evaInit() {
            bool resetFunctionButtons = !(controlMode == ControlMode.flying || controlMode == ControlMode.eva);
            controlMode = ControlMode.eva;
            removeAllButtonAssignments(resetFunctionButtons);
            flashingLights.Clear();
            flyingEvaCommonKeys(resetFunctionButtons);

            // Center block
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionLineColorChange, true, 15, SBC.Key.D, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionNightScope, true, 15, SBC.Key.S, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionF3, true, 15, SBC.Key.A, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionOverride, true, 15, SBC.Key.W, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionManipulator, true, 15, SBC.Key.E, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionF2, true, 15, SBC.Key.Q, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Washing, true, 15, SBC.Key.Escape, true);
            controller.AddButtonKeyMapping(ButtonEnum.RightJoyLockOn, SBC.Key.B, true);
        }

        public void buildingInit()
        {
            controlMode = ControlMode.building;
            removeAllButtonAssignments(true);
            flashingLights.Clear();
            
            // Center block
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionLineColorChange, true, 15, SBC.Key.D, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionNightScope, true, 15, SBC.Key.S, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionF3, true, 15, SBC.Key.A, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionOverride, true, 15, SBC.Key.W, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionManipulator, true, 15, SBC.Key.E, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionF2, true, 15, SBC.Key.Q, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Comm1, true, 15, SBC.Key.D1, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Comm2, true, 15, SBC.Key.D2, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Comm3, true, 15, SBC.Key.D3, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Comm4, true, 15, SBC.Key.D4, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Chaff, true, 15, SBC.Key.LeftShift, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionF1, true, 15, SBC.Key.X, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.FunctionTankDetach, true, 15, SBC.Key.C, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Washing, true, 15, SBC.Key.Escape, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.WeaponConMagazine, true, 15, SBC.Key.LeftAlt, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.Extinguisher, true, 15, SBC.Key.Space, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.WeaponConMain, true, 15, SBC.Key.LeftControl, SBC.Key.Z, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.WeaponConSub, true, 15, SBC.Key.LeftControl, SBC.Key.Y, true);

            // Right block
            controller.AddButtonKeyLightMapping(ButtonEnum.MultiMonOpenClose, true, 15, SBC.Key.R, true);
            controller.AddButtonKeyLightMapping(ButtonEnum.MultiMonModeSelect, true, 15, SBC.Key.F, true);
        }

        public void removeAllButtonAssignments(bool includeFunctionButtons)
        {
            foreach (ButtonEnum button in Enum.GetValues(typeof(ButtonEnum)))
            {
                if (includeFunctionButtons || (Array.IndexOf(functionButtons, button) == -1))
                {
                    controller.RemoveButtonKeyMapping(button);
                }
                try
                {
                    if (Array.IndexOf(gearLights, controller.GetLightForButton(button)) == -1)
                    {
                        // Not a gear lever light
                        if (includeFunctionButtons || !funcShiftSaved || (funcShiftSaved && (Array.IndexOf(functionButtons, button) == -1 && button!=bindings["functionShift"])))
                            controller.SetLEDState(controller.GetLightForButton(button), 0);
                    }
                }
                catch (System.ArgumentException)
                {
                    // button isn't a lit button, ignore
                }
            }
        }

		// this is necessary, as main program calls this to know how often to call mainLoop
		public int getRefreshRate() {
			return refreshRate;
		}
                		
		private int reverse(int val) {
			return (maxAxisValue - val);
		}
		
        // Get joystick deadzones from KSP settings file
        private void readSettingsFile()
        {
            string[] validInputs = {
                "AXIS_PITCH",
                "AXIS_ROLL",
                "AXIS_YAW",
                "AXIS_THROTTLE",
                "AXIS_THROTTLE_INC"
            };
            if (File.Exists(kspPath + settingsFile))
            {
                bool vjoystr = false;
                string axis = "";
                string input = "";
                // Open the file to read from. 
                try {
                    using (StreamReader sr = File.OpenText(kspPath + settingsFile))
                    {
                        string s = "";
                        while ((s = sr.ReadLine()) != null)
                        {
                            if (s.Contains("AXIS_"))
                            {
                                input = s.Trim();
                            } 
                            else if (s.Contains("vJoy"))
                            {
                                vjoystr = true;
                            }
                            else if (vjoystr == true && s.Contains("axis"))
                            {
                                string[] t = s.Split('=');
                                axis = t[1].TrimStart();
                            }
                            else if (vjoystr == true && !axis.Equals("") && s.Contains("deadzone") && Array.IndexOf(validInputs, input) != -1)
                            {
                                string[] t = s.Split('=');
                                double dz = Double.Parse(t[1].TrimStart());
                                switch (axis)
                                {
                                    case "0": // yaw == axis 0
                                        aimingXDeadzone = dz;
                                        break;
                                    case "1": // pitch == axis 1
                                        aimingYDeadzone = dz;
                                        break;
                                    case "2": // roll == axis 2
                                        rightMiddlePedalDeadzone = dz;
                                        break;
                                    /* Continuous throttle is glitched, let's wait for a fix...
                                    case "4": // continuous thrust == axis 5
                                        leftPedalDeadzone = dz;
                                        break;
                                     */
                                    case "5": // incremental thrust == axis 5
                                        rAxisDeadzone = dz;
                                        break;
                                }
                                vjoystr = false;
                                axis = "";
                                input = "";
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Dunno why we coulda failed to read, but ehh, better than crashing
                }
            }
        }

        // Read state file generated by the KSPSBC plugin
        private bool readStateFile() {
            if (File.Exists(kspPath + stateFile))
            {
                // Turn off the warning blinker
                if (!stateFileDetected) {
                    stateFileDetected = true;
                    flashingLights.Remove(noStateFileLight);
                }

                try
                {
                    using (StreamReader sr = File.OpenText(kspPath + stateFile))
                    {
                        string s = "";
                        char[] delimiter = { '=' };
                        while ((s = sr.ReadLine()) != null)
                        {
                            string[] v = s.Split(delimiter);
                            string key = v[0];
                            string value = v[1];
                            switch (key)    // uuuuuuuugh
                            {
                                case "mode":
                                    if (value.Equals("building") && controlMode != ControlMode.building)
                                    {
                                        buildingInit();
                                    }
                                    else if (value.Equals("flying") && controlMode != ControlMode.flying)
                                    {
                                        flyingInit();
                                    }
                                    else if (value.Equals("eva") && controlMode != ControlMode.eva)
                                    {
                                        evaInit();
                                    }
                                    break;
                                case "sas":
                                    {
                                        bool newState = Boolean.Parse(value);
                                        if (sasToggled != newState && controlMode == ControlMode.flying)
                                        {
                                            sasToggled = newState;
                                            if (sasToggled)
                                            {
                                                // Turn on the lights
                                                flashingLights.Remove(sasLight);
                                                controller.SetLEDState(controller.GetLightForButton(ButtonEnum.CockpitHatch), baseLineIntensity);
                                            }
                                            else
                                            {
                                                sasLight.reset();
                                                flashingLights.Add(sasLight);
                                            }
                                        }
                                    }
                                    break;
                                case "stageLock":
                                    {
                                        if (enableStageLockWarning)
                                        {
                                            bool newState = Boolean.Parse(value);
                                            if (newState && controlMode == ControlMode.flying)
                                            {
                                                // Staging lock is turned on
                                                if (!flashingLights.Contains(stagingLight))
                                                {
                                                    flashingLights.Add(stagingLight);
                                                }
                                            }
                                            else
                                            {
                                                flashingLights.Remove(stagingLight);
                                                controller.SetLEDState(controller.GetLightForButton(ButtonEnum.Start), lowIntensity);
                                            }
                                        }
                                    }
                                    break;  // seriously, falling through is an error? really c#?
                                case "gears":
                                case "lights":
                                case "brakes":
                                case "mapView":
                                case "rcs":
                                case "navBall":
                                case "stageLock2":  // oh god why did i do this
                                    if (controlMode == ControlMode.flying)
                                    {
                                        if (!key.Equals("stageLock2") || enableStageLockWarning)
                                        {
                                            bool bVal = Boolean.Parse(value);
                                            if (bVal == true && controller.GetLEDState(automodeLights[key]) != baseLineIntensity)
                                            {
                                                controller.SetLEDState(automodeLights[key], baseLineIntensity);
                                            }
                                            else if (bVal == false && controller.GetLEDState(automodeLights[key]) != lowIntensity)
                                            {
                                                controller.SetLEDState(automodeLights[key], lowIntensity);
                                            }
                                        }
                                    }
                                    break; 
                                case "evaHeadlamp":
                                case "evaJetpack":  
                                    if (controlMode == ControlMode.eva)
                                    {
                                        bool bVal = Boolean.Parse(value);
                                        if (bVal == true && controller.GetLEDState(automodeLights[key]) != baseLineIntensity)
                                        {
                                            controller.SetLEDState(automodeLights[key], baseLineIntensity);
                                        }
                                        else if (bVal == false && controller.GetLEDState(automodeLights[key]) != lowIntensity)
                                        {
                                            controller.SetLEDState(automodeLights[key], lowIntensity);
                                        }
                                    }
                                    break;
                                case "aeroOverlay":
                                case "thermalOverlay":
                                case "thermalGauges":
                                    if (controlMode == ControlMode.flying || controlMode == ControlMode.eva)
                                    {
                                        if (!funcShiftSaved)
                                        {
                                            bool bVal = Boolean.Parse(value);
                                            if (bVal == true && controller.GetLEDState(automodeLights[key]) != baseLineIntensity)
                                            {
                                                controller.SetLEDState(automodeLights[key], baseLineIntensity);
                                            }
                                            else if (bVal == false && controller.GetLEDState(automodeLights[key]) != lowIntensity)
                                            {
                                                controller.SetLEDState(automodeLights[key], lowIntensity);
                                            }
                                        }
                                        else
                                        {
                                        
                                            // Lights affected by function-shifting... Maybe I shouldn't hardcode this, but ehh
                                            // Update the saved state
                                            bool bVal = Boolean.Parse(value);
                                            if (key.Equals("aeroOverlay"))
                                            {
                                                functionLights[7].savedState = (bVal) ? baseLineIntensity : lowIntensity;
                                            }
                                            else if (key.Equals("thermalOverlay"))
                                            {
                                                functionLights[6].savedState = (bVal) ? baseLineIntensity : lowIntensity;
                                            }
                                            else if (key.Equals("thermalGauges"))
                                            {
                                                functionLights[5].savedState = (bVal) ? baseLineIntensity : lowIntensity;
                                            }
                                            /* Think about how to do this better...
                                            else if (key.Equals("brakes"))
                                            {
                                                functionLights[8].savedState = (bVal) ? baseLineIntensity : lowIntensity;
                                            }
                                            else if (key.Equals("lights"))
                                            {
                                                functionLights[9].savedState = (bVal) ? baseLineIntensity : lowIntensity;
                                            }
                                            else if (key.Equals("gears"))
                                            {
                                                functionLights[10].savedState = (bVal) ? baseLineIntensity : lowIntensity;
                                            }
                                            */
                                        }
                                    }
                                break;
                            }
                            if (key.Equals("mapView"))
                            {
                                // Control mode shouldn't matter, this will always be 'false' in building mode
                                mapView = Boolean.Parse(value);
                            }
                        }
                    }
                    File.Delete(kspPath + stateFile);
                    return true;
                }
                catch (Exception)
                {
                    // pass... read/write lock problem, we'll poll again a few milliseconds.
                }
             }
            return false;
        }


        // Functions for lighting up warning keys when the sticks leave the deadzone
        public bool checkWarningLight(FlashingLight flash, bool sendingInput, int value, double deadzone, bool warning)
        {
            return checkWarningLight(flash, sendingInput, value, deadzone, warning, true);
        }
        public bool checkWarningLight(FlashingLight flash, bool sendingInput, int value, double deadzone, bool warning, bool fromCenter)
        {
            // The toggle switch state change, turn off the warning light maybe
            bool shutOffWarning = false;
            if ((displayDeadzoneAlerts == DeadzoneAlertMode.toggleOff && warning && sendingInput)
                || (displayDeadzoneAlerts == DeadzoneAlertMode.toggleOn && warning && !sendingInput))
            {
                shutOffWarning = true;
            }
            // If controls are active, light warning lights
            if (!shutOffWarning && 
                (fromCenter && (value > (maxAxisValue * (0.5 + (deadzone / 2))) || value < (maxAxisValue * (0.5 - (deadzone / 2)))) || 
                (!fromCenter && (value > (maxAxisValue * deadzone)))))
            {
                if (!sendingInput)
                {
                    if ((displayDeadzoneAlerts == DeadzoneAlertMode.toggleOff || displayDeadzoneAlerts == DeadzoneAlertMode.both) && !flashingLights.Contains(flash))
                    {
                        flashingLights.Add(flash);
                    }
                }
                else
                {
                    if ((displayDeadzoneAlerts == DeadzoneAlertMode.toggleOn || displayDeadzoneAlerts == DeadzoneAlertMode.both))
                    {
                        flashingLights.Remove(flash);
                        controller.SetLEDState(flash.light, baseLineIntensity);
                    }
                }
                return true;
            }
            else
            {
                if (warning)
                {
                    flashingLights.Remove(flash);
                    if (controller.GetButtonState((int)flash.button) == false)
                    {
                        // It's safe to assume this is a function button.
                        int level = (funcShiftSaved) ? funcShiftIntensity : lowIntensity;
                        if (controller.GetLEDState(flash.light) != level)
                        {
                            controller.SetLEDState(flash.light, level);
                        }
                    }
                }
                return false;
            }
        }

        // optional function used for debugging purposes, comment out when running in game as it crashes a lot
        /*
        public String getDebugString()
        {
            return debugString;
        }
        */

		// This gets called once every refreshRate milliseconds by main program
		public void mainLoop() {
            //debugString = "";

            // Flash flashing lights
            for (int i = flashingLights.Count - 1; i >= 0; i--)
            {
                bool finished = flashingLights[i].poll(refreshRate);
                if (finished)
                {
                    flashingLights.RemoveAt(i);
                }
            }

            // Determine if control mode changed state
            int gearLever = controller.Scaled.GearLever;
            if (gearLever != savedGearLever)
            {
                switch (gearLever) {
                    case (int)ControlMode.building:
                        stateMode = StateMode.manual;
                        buildingInit();
                        break;
                    case (int)ControlMode.flying:
                        stateMode = StateMode.manual;
                        flyingInit();
                        break;
                    case (int)ControlMode.eva:
                        stateMode = StateMode.manual;
                        evaInit();
                        break;
                    case (int)ControlMode.automatic:
                        stateMode = StateMode.automatic;
                        statePoll = 0;
                        if (!stateFileDetected && !flashingLights.Contains(noStateFileLight))
                        {
                            flashingLights.Add(noStateFileLight);
                        };
                        break;
                }
                savedGearLever = gearLever;
                // controlMode updating is handled by the init functions
            }

            // Check for new state file in automatic mode
            if (stateMode == StateMode.automatic)
            {
                statePoll -= refreshRate;
                if (statePoll <= 0)
                {
                    readStateFile();
                    statePoll = statePollTime;
                }
            }

            // Flight & EVA mode effects and actions
            bool steeringLock = (bool)controller.GetButtonState((int)bindings["steeringLock"]);
            bool throttleLock = (bool)controller.GetButtonState((int)bindings["throttleLock"]);
            bool swapYawRoll = (bool)controller.GetButtonState((int)bindings["swapYawRoll"]);
            if (controlMode == ControlMode.flying || controlMode == ControlMode.eva)
            {
                // Steering lock
                if (steeringLock)
                {
                    int aimingX = controller.Scaled.AimingX;
                    int rightMiddlePedal = controller.Scaled.RightMiddlePedal;
                    if (swapYawRoll)
                    {
                        // Swap yaw and roll on toggle. Useful for planes.
                        rightMiddlePedal = controller.Scaled.AimingX;
                        aimingX = controller.Scaled.RightMiddlePedal;
                    }
                    joystick.setAxis(1, aimingX, HID_USAGES.HID_USAGE_X); // aim stick X
                    joystick.setAxis(1, controller.Scaled.AimingY, HID_USAGES.HID_USAGE_Y); // aim stick Y
                    joystick.setAxis(1, rightMiddlePedal, HID_USAGES.HID_USAGE_Z);//throttle
                }
                else
                {
                    // Send dummy values; returns controls to neutral when the switch is disabled
                    // (Prevents a command from repeating until the switch is reenabled)
                    joystick.setAxis(1, maxAxisValue / 2, HID_USAGES.HID_USAGE_X); // aim stick X
                    joystick.setAxis(1, maxAxisValue / 2, HID_USAGES.HID_USAGE_Y); // aim stick Y
                    joystick.setAxis(1, maxAxisValue / 2, HID_USAGES.HID_USAGE_Z);
                }
                // Throttle lock
                if (throttleLock)
                {
                    joystick.setAxis(1, controller.Scaled.RotationLever, HID_USAGES.HID_USAGE_RZ);
                    // Hacky fix until the deadzone problem is solved
                    int val = controller.Scaled.LeftPedal;
                    if (val > (maxAxisValue * leftPedalDeadzone))
                    {
                        joystick.setAxis(1, val, HID_USAGES.HID_USAGE_RY);
                    }
                    else
                    {
                        joystick.setAxis(1, 0, HID_USAGES.HID_USAGE_RY);
                    }
                }
                else
                {
                    joystick.setAxis(1, maxAxisValue / 2, HID_USAGES.HID_USAGE_RZ);
                    joystick.setAxis(1, 0, HID_USAGES.HID_USAGE_RY);
                }
                
                // Other stick bindings
                joystick.setAxis(1, controller.Scaled.SightChangeX, HID_USAGES.HID_USAGE_SL0);
                joystick.setAxis(1, controller.Scaled.SightChangeY, HID_USAGES.HID_USAGE_RX);
                joystick.setAxis(1, controller.Scaled.GearLever, HID_USAGES.HID_USAGE_SL1);

                // Warp speed, tuner dial
                int tunerValue = controller.TunerDial;
                if (tunerValue != tunerValueSaved)
                {
                    // The tuner dial is kind of touchy, it likes to sway between values at some settings.
                    // We set a delay countdown to try to prevent short sways from changing the warp speed.
                    // (Note: Figure out how and whether it's worth sending more than one keypress,
                    // in case someone really cranks the dial... sampling problem, in case someone cranks it
                    // more than halfway around. hmm.)
                    if (tunerChangeCountdown <= 0)
                    {
                        int detla = tunerValue - tunerValueSaved;
                        if ((tunerValue > tunerValueSaved && !(tunerValue == 15 && tunerValueSaved == 0)) || (tunerValue == 0 && tunerValueSaved == 15))
                        {
                            // Increase warp speed
                            controller.sendKeyPress(SBC.Key.Period);
                        }
                        else
                        {
                            // Decrease warp speed
                            controller.sendKeyPress(SBC.Key.Comma);
                        }
                        tunerValueSaved = tunerValue;
                    }
                    else
                    {
                        tunerChangeCountdown -= refreshRate;
                    }
                }
                else if (tunerChangeCountdown != tunerChangeCountdownDuration)
                {
                    // Reset countdown timer
                    tunerChangeCountdown = tunerChangeCountdownDuration;
                }

                // Precise controls switch
                bool precisionSwitch = (bool)controller.GetButtonState((int)bindings["precisionControls"]);
                if (precisionSwitch != Control.IsKeyLocked(Keys.CapsLock))
                {
                    controller.sendKeyPress(SBC.Key.CapsLock);
                }


                // Function shift asserted, key is unlit... Relight it!
                if (funcShiftSaved)
                {
                    int functionButtonCt = (useExtendedFunctionKeys) ? functionButtons.Length : basicFunctionButtonCt;
                    for (int i = 0; i < functionButtonCt; i++)
                    {
                        if (controller.GetLEDState(controller.GetLightForButton(functionButtons[i])) == 0)
                        {
                            controller.SetLEDState(controller.GetLightForButton(functionButtons[i]), funcShiftIntensity);
                        }
                    }
                }
                // "Function shift", change function key bindings if shift key changes
                bool funcShift = (bool)controller.GetButtonState((int)bindings["functionShift"]);
                if (funcShift != funcShiftSaved)
                {
                    // State changed, set new bindings
                    int offset = 0;
                    int functionButtonCt = (useExtendedFunctionKeys) ? functionButtons.Length : basicFunctionButtonCt;
                    if (funcShift)
                    {
                        controller.SetLEDState(controller.GetLightForButton(bindings["functionShift"]), baseLineIntensity);
                        offset = functionKeys.Length / 2;
                        for (int i = 0; i < functionButtonCt; i++)
                        {
                            functionLights[i].savedState = controller.GetLEDState(controller.GetLightForButton(functionButtons[i]));
                            controller.SetLEDState(controller.GetLightForButton(functionButtons[i]), funcShiftIntensity);
                        }
                    }
                    else
                    {
                        controller.SetLEDState(controller.GetLightForButton(bindings["functionShift"]), lowIntensity);
                        for (int i = 0; i < functionButtonCt; i++)
                        {
                            controller.SetLEDState(controller.GetLightForButton(functionButtons[i]), functionLights[i].savedState);
                        }
                    }
                    for (int i = 0; i < functionButtonCt; i++)
                    {
                        bool toggle = (funcShift) ? true : functionLights[i].toggle;
                        // Extra special alternate bindings in map mode
                        if (mapView && functionKeys[offset + i] == SBC.Key.LeftBracket)
                        {
                            controller.AddButtonKeyLightMapping(functionButtons[i], toggle, 15, SBC.Key.Tab, true);
                        }
                        else if (mapView && functionKeys[offset + i] == SBC.Key.RightBracket)
                        {
                            controller.AddButtonKeyLightMapping(functionButtons[i], toggle, 15, SBC.Key.BackSlash, true);
                        }
                        else
                        {
                            controller.AddButtonKeyLightMapping(functionButtons[i], toggle, 15, functionKeys[offset + i], true);
                        }
                    }
                    funcShiftSaved = funcShift;
                }

                // RCS state detect (legacy, switched RCS to be a button, left here in case I change my mind)
                /*
                bool rcsSwitch = (bool)controller.GetButtonState(ButtonEnum.ToggleFilterControl);
                if (rcsSwitch != rcsActive)
                {
                    controller.sendKeyPress(SBC.Key.R);
                    rcsActive = rcsSwitch;
                }
                */

            }

            // Flight-specific actions
            if(controlMode == ControlMode.flying) {
                // Check input warnings
                if (displayDeadzoneAlerts != DeadzoneAlertMode.none)
                {
                    aimingXWarnState = checkWarningLight(aimingXWarning, steeringLock, controller.Scaled.AimingX, aimingXDeadzone, aimingXWarnState);
                    aimingYWarnState = checkWarningLight(aimingYWarning, steeringLock, controller.Scaled.AimingY, aimingYDeadzone, aimingYWarnState);
                    rightMiddlePedalWarnState = checkWarningLight(rightMiddlePedalWarning, steeringLock, controller.Scaled.RightMiddlePedal, rightMiddlePedalDeadzone, rightMiddlePedalWarnState);
                    leftPedalWarnState = checkWarningLight(leftPedalWarning, throttleLock, controller.Scaled.LeftPedal, leftPedalDeadzone, leftPedalWarnState, false);
                    rAxisWarnState = checkWarningLight(rAxisWarning, throttleLock, controller.Scaled.RotationLever, rAxisDeadzone, rAxisWarnState);
                }

                // Docking state detect
                bool dockingSwitch = controller.GetButtonState((int)bindings["docking"]);
                if (dockingSwitch != dockingMode)
                {
                    // State changed
                    dockingMode = dockingSwitch;
                    if (dockingMode == true)
                    {
                        controller.sendKeyPress(SBC.Key.Delete);
                    }
                    else
                    {
                        controller.sendKeyPress(SBC.Key.Insert);
                    }
                }

                // Handle SAS
                if ((bool)controller.GetButtonState((int)ButtonEnum.CockpitHatch))
                {
                    sasDepressed = true;
                    flashingLights.Remove(sasLight);
                    controller.SetLEDState(controller.GetLightForButton(ButtonEnum.CockpitHatch), 15);
                }
                else if (sasDepressed && (bool)controller.GetButtonState((int)ButtonEnum.CockpitHatch) == false)
                {
                    // key released
                    sasToggled = !sasToggled;
                    sasDepressed = false;
                    if (!sasToggled)
                    {
                        sasLight.reset();
                        flashingLights.Add(sasLight);
                    }
                }

                // Random flashing lights
                if(randomFlashingLights) {
                    timeUntilRandomFlash -= refreshRate;
                    if (timeUntilRandomFlash <= 0)
                    {
                        //randomLight.change);
                        flashingLights.Add(new FlashingLight(randomLightCandidates[random.Next(randomLightCandidates.Length)], 0, random.Next(250, 1000), 6, random.Next(250, 1000), random.Next(1, 5)));
                        timeUntilRandomFlash = random.Next(timeBetweenFlashesLow, timeBetweenFlashesHigh);
                    }
                }
            }
            joystick.sendUpdate(1);
		}


		// This gets called at the end of the program and must be present, as it cleans up resources
		public void shutDown()
		{
			controller.UnInit();
			joystick.Release(1);
		}
	}
}