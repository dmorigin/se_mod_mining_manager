using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // the max fill value. If this value is arraived mining will stopped.
        private const float maxFillValue = 0.95f; // max 95%

        // the value below the max value, before restart mining
        private const float reastartFillRatio = 0.70f;

        // max speed to drop down the drills
        private const float maxDropDownSpeed = 0.01f;

        // max drill rotation speed
        private const float maxDrillRotationSpeed = 0.45f;

        // search for blocks that containe this name
        private const string nameTag = "[MiningManager]";

        // the name of that motor that rotate the drills directly. Not the name
        // of a motor that rotate the hole arm or other things.
        private const string drillRotorTag = "Drill Rotor";


        // Status Variables - Changed by this script - Don't change it!
        List<PistonController> pistons_ = null;
        List<IMyShipDrill> drills_ = null;
        Inventory inventory_ = null;
        RotorController drillRotor_ = null;
        IMyTextSurface lcd_ = null;
        ProductionUnit production_ = null;


        private bool doInit_ = true;
        private bool stopped_ = true;
        private bool stoppedByUser_ = false;
        private float dropDownSpeed_ = maxDropDownSpeed;
        private float dropDownSpeedPerPiston_ = 0f;
        private float drillRotationSpeed_ = maxDrillRotationSpeed;
        private string messageLine_ = string.Empty;


        // property names
        const string PropertyOnOff = "OnOff";
        const string PropertyInertiaTensor = "ShareInertiaTensor";
        const string PropertyLowerLimit = "LowerLimit";
        const string PropertyUpperLimit = "UpperLimit";
        const string PropertyRotorLock = "RotorLock";
        const string PropertyVelocity = "Velocity";
        const string PropertyTorque = "Torque";
        const string PropertyBrakingTorque = "BrakingTorque";


        public class PistonController
        {
            Program parent_ = null;
            IMyPistonBase piston_ = null;
            Base6Directions.Direction orientation_;


            public PistonController(Program parent, IMyPistonBase piston)
            {
                parent_ = parent;
                piston_ = piston;

                setup();
            }


            public void setup()
            {
                piston_.SetValue<float>(Program.PropertyUpperLimit, 10f);
                piston_.SetValue<float>(Program.PropertyLowerLimit, 0f);
                piston_.SetValue<bool>(Program.PropertyInertiaTensor, true);
                piston_.Velocity = 0f;

                // calculate orientation
                Vector3D myselfM = piston_.WorldMatrix.Up;
                myselfM = Vector3D.Rotate(myselfM, Matrix.Invert(parent_.Me.WorldMatrix));
                orientation_ = Base6Directions.GetDirection(myselfM);
            }


            #region Properties
            public IMyPistonBase Piston
            {
                get
                {
                    return piston_;
                }
            }


            public string Name
            {
                get
                {
                    return piston_.CustomName;
                }
            }


            public bool IsDown
            {
                get
                {
                    return orientation_ == Base6Directions.Direction.Down;
                }
            }


            public bool IsUp
            {
                get
                {
                    return orientation_ == Base6Directions.Direction.Up;
                }
            }


            public float CurrentExtendValue
            {
                get
                {
                    return piston_.CurrentPosition;
                }
            }


            public float MaxExtendValue
            {
                get
                {
                    return piston_.GetValue<float>(Program.PropertyUpperLimit);
                }
            }


            public float MinExtendValue
            {
                get
                {
                    return piston_.GetValue<float>(Program.PropertyLowerLimit);
                }
            }


            public float CurrentVelocity
            {
                get
                {
                    return piston_.Velocity;
                }
                set
                {
                    if (IsDown)
                        piston_.Velocity = value;
                    else
                        piston_.Velocity = -value;
                }
            }


            public PistonStatus Status
            {
                get
                {
                    return piston_.Status;
                }
            }


            public bool Activate
            {
                get
                {
                    return piston_.GetValue<bool>(Program.PropertyOnOff);
                }
                set
                {
                    piston_.SetValue<bool>(Program.PropertyOnOff, value);
                }
            }
            #endregion
        }

        public class RotorController
        {
            Program parent_ = null;
            IMyMotorStator stator_ = null;


            public RotorController(Program parent, IMyMotorStator stator)
            {
                parent_ = parent;
                stator_ = stator;

                setup();
            }

            public void setup()
            {
                stator_.SetValue<float>(Program.PropertyLowerLimit, float.MinValue);
                stator_.SetValue<float>(Program.PropertyUpperLimit, float.MaxValue);
                stator_.TargetVelocityRPM = 0f;
                stator_.SetValue<bool>(Program.PropertyInertiaTensor, true);
                RotorLock = true;
            }

            #region Properties
            public bool Activate
            {
                get
                {
                    return stator_.GetValue<bool>(Program.PropertyOnOff);
                }
                set
                {
                    stator_.SetValue<bool>(Program.PropertyOnOff, value);
                }
            }


            public bool RotorLock
            {
                get
                {
                    return stator_.GetValue<bool>(Program.PropertyRotorLock);
                }
                set
                {
                    stator_.SetValue<bool>(Program.PropertyRotorLock, value);
                }
            }


            public float Velocity
            {
                get
                {
                    return stator_.TargetVelocityRPM;
                }
                set
                {
                    stator_.TargetVelocityRPM = value;
                }
            }
            #endregion
        }

        public class Inventory
        {
            Program parent_ = null;
            List<IMyCargoContainer> containers_ = new List<IMyCargoContainer>();


            public Inventory(Program parent)
            {
                parent_ = parent;
            }


            public void scanInventoryBlocks()
            {
                containers_.Clear();
                parent_.GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers_, (container) =>
                {
                    if (!container.IsSameConstructAs(parent_.Me))
                        return false;

                    return true;
                });
            }

            public double FillRatio
            {
                get
                {
                    double max = 0d;
                    double cur = 0d;

                    foreach (var container in containers_)
                    {
                        var inv = container.GetInventory();
                        max += (double)inv.MaxVolume;
                        cur += (double)inv.CurrentVolume;
                    }

                    return cur / max;
                }
            }


            public int Count
            {
                get
                {
                    return containers_.Count;
                }
            }
        }

        public class ProductionUnit
        {
            Program parent_ = null;
            IMyProductionBlock production_ = null;


            public ProductionUnit(Program parent, IMyProductionBlock production)
            {
                parent_ = parent;
                production_ = production;

                production_.UseConveyorSystem = true;
            }

            public void checkQueue()
            {
                MyDefinitionId ingots = MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/StoneOreToIngotBasic");

                // no Ingots in queue?
                List<MyProductionItem> queue = new List<MyProductionItem>();
                production_.GetQueue(queue);
                foreach (var item in queue)
                {
                    if (item.BlueprintId == ingots && item.Amount > 0)
                        return;
                }

                // add Ingots to queue
                production_.InsertQueueItem(0, ingots, 2000d);
            }
        }

        #region Utility methods
        public bool init()
        {
            // init pistons
            pistons_.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(null, (piston) =>
            {
                PistonController controller = new PistonController(this, piston);
                pistons_.Add(controller);
                return false;
            });

            // init inventory
            inventory_.scanInventoryBlocks();

            // init rotor
            IMyMotorStator stator = GridTerminalSystem.GetBlockWithName(drillRotorTag) as IMyMotorStator;
            if (stator != null)
                drillRotor_ = new RotorController(this, stator);

            // init drills
            drills_.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(drills_);
            if (drills_.Count == 0)
            {
                addMessageLine("Error: No drills found!");
                return false;
            }

            // production block
            GridTerminalSystem.GetBlocksOfType<IMyProductionBlock>(null, (block) =>
            {
                production_ = new ProductionUnit(this, block);
                return false;
            });

            return true;
        }


        public void setupPistonSpeed()
        {
            // calculate piston speed
            int activePistons = 0;
            foreach (var piston in pistons_)
            {
                if (piston.IsDown)
                    activePistons += piston.CurrentExtendValue < piston.MaxExtendValue ? 1 : 0;
                else if (piston.IsUp)
                    activePistons += piston.CurrentExtendValue > piston.MinExtendValue ? 1 : 0;
            }

            // setup speed
            if (activePistons > 0)
            {
                dropDownSpeedPerPiston_ = dropDownSpeed_ / activePistons;
                foreach (var piston in pistons_)
                    piston.CurrentVelocity = dropDownSpeedPerPiston_;
            }
        }


        public void resume()
        {
            // start drills
            foreach (var drill in drills_)
                drill.SetValue<bool>(Program.PropertyOnOff, true);

            // restart drill rotor
            if (drillRotor_ != null)
            {
                drillRotor_.Velocity = drillRotationSpeed_;
                drillRotor_.RotorLock = false;
            }

            // restart pistons
            setupPistonSpeed();

            stopped_ = false;
        }


        public void stop()
        {
            // stop all pistons
            foreach (var piston in pistons_)
                piston.CurrentVelocity = 0f;

            // stop drill rotor
            if (drillRotor_ != null)
                drillRotor_.RotorLock = true;

            // stop drills
            foreach (var drill in drills_)
                drill.SetValue<bool>(Program.PropertyOnOff, false);

            stopped_ = true;
        }


        public void reset()
        {
            // stop everything
            stop();

            // pistons back to start position
            foreach (var piston in pistons_)
                piston.CurrentVelocity = -0.25f;
        }


        public float currentMinigDepth()
        {
            float depth = 0f;
            foreach (var piston in pistons_)
                depth += piston.IsDown ? piston.CurrentExtendValue : (piston.MaxExtendValue - piston.CurrentExtendValue); ;

            return depth;
        }


        public void addMessageLine(string message)
        {
            messageLine_ += message + "\n";
        }


        public void flushMessages(IMyTextSurface surface)
        {
            surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
            surface.TextPadding = 0f;
            surface.BackgroundColor = new Color(0, 7, 3);
            surface.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;

            surface.Font = "Debug";
            surface.FontSize = 1f;
            surface.FontColor = new Color(0, 165, 0);

            surface.WriteText(messageLine_, false);
            Echo(messageLine_);
            messageLine_ = string.Empty;
        }
        #endregion // Utility methods


        #region SE Programmable Block methods
        public Program()
        {
            // init application
            pistons_ = new List<PistonController>();
            inventory_ = new Inventory(this);
            drills_ = new List<IMyShipDrill>();

            // setup lcd
            lcd_ = Me.GetSurface(0);

            Runtime.UpdateFrequency = UpdateFrequency.Once;
        }


        public void Save()
        {
        }


        string[] ticker_ = { ".", "..", "...", "...." };
        int tickerCount_ = 0;


        public void Main(string argument, UpdateType updateSource)
        {
            addMessageLine("Automatic Minig Manager");
            addMessageLine("============================");
            addMessageLine((stopped_ ? "Stopped " : (doInit_ ? "Initilizing " : "Running ")) + ticker_[tickerCount_++]);
            if (tickerCount_ >= 4)
                tickerCount_ = 0;

            addMessageLine("Pistons:             " + pistons_.Count);
            addMessageLine("Piston Velocity: " + dropDownSpeedPerPiston_.ToString("#0.00###") + "m/s (Per Piston)");
            addMessageLine("Minig Depth:      " + currentMinigDepth().ToString("###0.00") + "m");
            addMessageLine("Drill Rotor:         " + (drillRotor_ != null ? "Found" : "Not Found"));
            addMessageLine("Production Unit: " + (production_ != null ? "Found" : "Not Found"));

            // fill ratio
            if (inventory_ != null)
                addMessageLine("Filled:                 " + (inventory_.FillRatio * 100d).ToString("##0.00") + "%");

            if (stopped_)
                addMessageLine("Restart:              " + (stoppedByUser_ ? "Manually" : "Automatic"));
            else
                addMessageLine("Restart:              Currently Running");


            if (doInit_)
            {
                if (init())
                {
                    doInit_ = false;
                    stopped_ = true;
                    stoppedByUser_ = true;
                    Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    flushMessages(lcd_);
                    return;
                }

                // stop automatic update
                addMessageLine("\nError on initializing.... script stopped!");
                Runtime.UpdateFrequency = UpdateFrequency.None;
                flushMessages(lcd_);
                return;
            }

            // normal tick
            if ((updateSource & UpdateType.Update100) != 0)
            {
                if (!stopped_ && inventory_.FillRatio >= maxFillValue)
                {
                    stop();
                    stopped_ = true;
                }
                else if (stopped_ && inventory_.FillRatio <= reastartFillRatio)
                {
                    if (!stoppedByUser_)
                    {
                        resume();
                        stopped_ = false;
                    }
                }

                // manage production
                if (production_ != null)
                    production_.checkQueue();
            }

            // executed by trigger
            if ((updateSource & UpdateType.Terminal) != 0 || (updateSource & UpdateType.Trigger) != 0)
            {
                MyCommandLine cl = new MyCommandLine();
                if (cl.TryParse(argument))
                {
                    if (cl.Switch("start"))
                    {
                        stoppedByUser_ = false;
                        resume();
                    }
                    else if (cl.Switch("stop"))
                    {
                        stoppedByUser_ = true;
                        stopped_ = true;
                        stop();
                    }
                    else if (cl.Switch("reset"))
                    {
                        stopped_ = true;
                        stoppedByUser_ = true;
                        reset();
                    }
                }
            }

            flushMessages(lcd_);
        }
        #endregion // SE Programmable Block methods
    } // script end
}