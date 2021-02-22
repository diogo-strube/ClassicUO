#region license

// Copyright (C) 2020 ClassicUO Development Community on Github
// 
// This project is an alternative client for the game Ultima Online.
// The goal of this is to develop a lightweight client considering
// new technologies.
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Network;

namespace ClassicUO.Game.Scripting
{
    // Execution descriptor
    public class CommandExecution
    {
        // Command to be executed
        public Command Cmd { get; }

        // Argument list given of this specific execution
        public ArgumentList ArgList { get; }

        // Indicated if the force (!) modifier was used
        public bool Force { get; }

        // Indicated if the quiet (@) modifier was used
        public bool Quiet { get; }

        // Time execution was created/requested
        public DateTime CreationTime { get; }

        // Provide access to the time a given command was last executed
        private static Dictionary<string, DateTime> _lastExecutedRegistry = new Dictionary<string, DateTime>();
        public static DateTime LastExecuted(params string[] commandKeywords)
        {
            DateTime lastExecuted = DateTime.UtcNow.AddHours(-2.0); // 2 hours ago
            foreach(string key in commandKeywords)
            {
                if (_lastExecutedRegistry.ContainsKey(key) && _lastExecutedRegistry[key] > lastExecuted)
                    lastExecuted = _lastExecutedRegistry[key];
            }
            return lastExecuted;
        }

        // Build execution
        public CommandExecution(Command command, ArgumentList argList, bool quiet, bool force)
        {
            Cmd = command;
            ArgList = argList;
            Quiet = quiet;
            Force = force;
            CreationTime = DateTime.UtcNow;
        }

        // This method check if it is time to execute the command and if the time is right, perform the execution
        // Logic inside may be complex, checking for a given target or waiting for a given gump
        public bool Process()
        {
            if (Cmd.WaitLogic(this)) // check if waiting is over (no blocking, we keep checking as Razor does)
            {
                _lastExecutedRegistry[Cmd.Keyword] = DateTime.UtcNow; // store time of this execution (as the last execution)
                return Cmd.ExecutionLogic(this); // execute the command and do the magic
            }
            else return false;
        }
    }

    // Loosely types abstraction of a command using strings to define expected usage and types
    public class Command
    {
        // All commands share access to the execution queues
        public static Dictionary<Attributes, Queue<CommandExecution>> Queues = new Dictionary<Attributes, Queue<CommandExecution>>();

        // Delegate to allow customization of how execution (both action logic and wait logic) are performed
        public delegate bool Handler(CommandExecution execution);

        // Basic usage syntax
        public string Usage { get; }

        // Keyword in the script syntax
        public string Keyword { get; }

        // Type of the arguments used by this command
        public string[] ArgTypes { get; }

        // Handler to perform command logic (action of the command)
        public Handler ExecutionLogic { get; }

        // Handler to perform wait logic (deciding if command can execute)
        public Handler WaitLogic { get; }

        // Defines what to command related to when it comes to game logic
        // Also used to organize execution queues
        [Flags]
        public enum Attributes
        {
            ScriptAction = 0,       // This command is related to a script logic unrelated to the game logic, such as aliases
            ForceBypassQueue = 1,   // Force also means bypassing the queue
            ComplexInterAction = 2, // This command is related to a complex interaction, like drag and drop something in game
            SimpleInterAction = 4,  // This command is related to a simple interaction, like a click or dclick in an object
            StateAction = 8,        // This command is related a game state, such as finding an object in the ground
            PlayerAction = 16,      // This command is related to a player driven action, like moving or attacking
        }
        public Attributes Attribute { get; }

        // Several flavors of constructors for quality of life
        #region Constructors
        public Command(string usage, Handler executionLogic, Handler waitLogic)
            : this(usage, executionLogic, waitLogic, Attributes.ScriptAction)
        {
        }
        public Command(string usage, Handler executionLogic, Handler waitLogic, Attributes attribute)
        {
            Usage = usage;
            ExecutionLogic = executionLogic;
            WaitLogic = waitLogic;
            Attribute = attribute;

            // Make sure a queue exists for this command category
            if (!Queues.ContainsKey(Attribute))
                Queues.Add(Attribute, new Queue<CommandExecution>());

            // Processing keywords and arguments in constructor to avoid logic on every command execution
            if (usage.Count(f => f == ' ') > 0)
            {
                Keyword = usage.Substring(0, usage.IndexOf(' '));
                ArgTypes = String.Join("", usage.Substring(usage.IndexOf(' ') + 1).Split('[', ']', '(', ')')).Split(' '); // keeping just name - same regex [\[\]\(\)]
            }
            else
                Keyword = usage;        
        }
        #endregion

        // Create list for args collected by the Abstract Syntax Tree (AST)
        public ArgumentList ListArgs(Argument[] args)
        {
            return new ArgumentList(args, ArgTypes);
        }

        // Execute the command according to queing rules and provided logic
        public bool Process(string command, Argument[] args, bool quiet, bool force)
        {
            // Build execution
            var execution = CreateExecution(args, quiet, force);
            if ((force && (Attribute & Attributes.ForceBypassQueue) == Attributes.ForceBypassQueue) || // perform logic now if queue should be bypassed
                (Attribute & Attributes.ForceBypassQueue) == Attributes.ScriptAction)  // also perform logic now if action is script logic
                return execution.Process();
            else Queues[Attribute].Enqueue(execution); // otherwise queue it
            return true;
        }

        // Retrieving an execution is showing that the Process logic may not be part of the command,
        // but in the execution... If we add this line of tough to Jaedan feedback on the Queue, we may
        // want to rename Execution to an Action class that allows both implementation of virtual methdods
        // or passing delegates.
        public CommandExecution CreateExecution(Argument[] args, bool quiet, bool force)
        {
            // Parse arguments when called, independent if this execution will be qued or not.
            ArgumentList argList = new ArgumentList(args, ArgTypes);
            return new CommandExecution(this, argList, quiet, force);
        }
    }

    // Class grouping all command related functionality, including implemented handles
    public static class Commands
    {
        // Registry of available commands retrivable by name (keyword)
        public static Dictionary<string, Command> Definitions = new Dictionary<string, Command>();
        private static void AddDefinition(Command cmd)
        {
            Interpreter.RegisterCommandHandler(cmd.Keyword, cmd.Process);
            Definitions.Add(cmd.Keyword, cmd);
        }

        // Registry of queues retrivable by name (keyword). A single queue can be shared by multiple commands
        // Key for the queue is the Attribute of the command, making sure common command share the same queue
        //public static Dictionary<Command.Attributes, Queue<CommandExecution>> Queues = new Dictionary<Command.Attributes, Queue<CommandExecution>>();

        public static void Register()
        {
            // Moving the "local aliases" (aka scope/param aliases) to the ArgumentList class Defaults feature
            // ATTENTION - this may be removed in the future if we noticed there is no common ground between the commands expected defaultas for each type (color, source, etc)
            // Colors
            ArgumentList.AddMap("color", "any", ushort.MaxValue);
            ArgumentList.AddMap("color", (ushort)0, ushort.MaxValue);
            // Source
            //ArgumentList.AddMap("source", "", "backpack");
            // Destination
            //ArgumentList.AddMap("destination", "ground", uint.MaxValue);
            //ArgumentList.AddMap("destination", (uint)0, uint.MaxValue);
            // Directions
            ArgumentList.AddMap("direction", "southeast", "down");
            ArgumentList.AddMap("direction", "southwest", "left");
            ArgumentList.AddMap("direction", "northeast", "right");
            ArgumentList.AddMap("direction", "northwest", "up");

            // Add definitions for all supported commands
            AddDefinition(new Command("setability ('primary'/'secondary'/'stun'/'disarm') ['on'/'off']", SetAbility, WaitForMs(500), Command.Attributes.StateAction));
            AddDefinition(new Command("attack (serial)", Attack, WaitForMs(500), Command.Attributes.SimpleInterAction));
            AddDefinition(new Command("clearhands ('left'/'right'/'both')", ClearHands, WaitForMs(500), Command.Attributes.ComplexInterAction));
            AddDefinition(new Command("clickobject (serial)", ClickObject, WaitForMs(500), Command.Attributes.SimpleInterAction));
            AddDefinition(new Command("bandageself", BandageSelf, WaitForMs(500), Command.Attributes.SimpleInterAction));
            AddDefinition(new Command("usetype (graphic) [color] [source] [range or search level]", UseType, WaitForMs(500), Command.Attributes.SimpleInterAction));
            AddDefinition(new Command("useobject (serial)", UseObject, WaitForMs(500), Command.Attributes.SimpleInterAction));
            AddDefinition(new Command("useonce (graphic) [color]", UseOnce, WaitForMs(500), Command.Attributes.SimpleInterAction));
            AddDefinition(new Command("moveitem (serial) (destination) [(x, y, z)] [amount]", MoveItem, WaitForMs(500), Command.Attributes.ComplexInterAction));
            AddDefinition(new Command("moveitemoffset (serial) 'ground' [(x, y, z)] [amount]", MoveItemOffset, WaitForMs(500), Command.Attributes.ComplexInterAction));
            AddDefinition(new Command("movetype (graphic) (source) (destination) [(x, y, z)] [color] [amount] [range or search level]", MoveType, WaitForMs(500), Command.Attributes.ComplexInterAction));
            AddDefinition(new Command("movetypeoffset (graphic) (source) 'ground' [(x, y, z)] [color] [amount] [range or search level]", MoveTypeOffset, WaitForMs(500), Command.Attributes.ComplexInterAction));
            
            AddDefinition(new Command("walk (direction)", MovementLogic(false), WaitForMovement, Command.Attributes.PlayerAction));
            AddDefinition(new Command("turn (direction)", MovementLogic(false), WaitForMovement, Command.Attributes.PlayerAction));
            AddDefinition(new Command("run (direction)", MovementLogic(true), WaitForMovement, Command.Attributes.PlayerAction));
            AddDefinition(new Command("useskill ('skill name'/'last')", UseSkill, WaitForMs(500), Command.Attributes.SimpleInterAction));
            AddDefinition(new Command("feed (serial) ('food name'/'food group'/'any'/graphic) [color] [amount]", Feed, WaitForMs(500), Command.Attributes.ComplexInterAction));
            AddDefinition(new Command("rename (serial) ('name')", Rename, WaitForMs(500), Command.Attributes.ComplexInterAction));
            AddDefinition(new Command("shownames ['mobiles'/'corpses']", ShowNames, WaitForMs(200), Command.Attributes.SimpleInterAction));
            AddDefinition(new Command("togglehands ('left'/'right')", ToggleHands, WaitForMs(500), Command.Attributes.ComplexInterAction));
            AddDefinition(new Command("equipitem (serial) (layer)", EquipItem, WaitForMs(500), Command.Attributes.ComplexInterAction));

            AddDefinition(new Command("findtype (graphic) [color] [source] [amount] [range or search level]", BandageSelf, WaitForMs(500), Command.Attributes.SimpleInterAction));
            AddDefinition(new Command("findobject (serial) [color] [source] [amount] [range]", FindObject, WaitForMs(100), Command.Attributes.StateAction));
            
            AddDefinition(new Command("poplist ('list name') ('element value'/'front'/'back')", PopList, WaitForMs(25)));
            AddDefinition(new Command("pushlist ('list name') ('element value') ['front'/'back']", PushList, WaitForMs(25)));
            AddDefinition(new Command("createlist ('list name')", CreateList, WaitForMs(25)));
            AddDefinition(new Command("removelist ('list name')", RemoveList, WaitForMs(25)));
            AddDefinition(new Command("msg ('text') [color]", Msg, WaitForMs(25)));
            AddDefinition(new Command("setalias ('alias name') [serial]", SetAlias, WaitForMs(25)));
            AddDefinition(new Command("unsetalias ('alias name')", UnsetAlias, WaitForMs(25)));
            AddDefinition(new Command("promptalias ('alias name')", PromptAlias, WaitForMs(25)));


            ////Interpreter.RegisterCommandHandler("poplist", );
            //Interpreter.RegisterCommandHandler("pushlist", );
            ////Interpreter.RegisterCommandHandler("removelist", );
            ////Interpreter.RegisterCommandHandler("createlist", CreateList);

            //Interpreter.RegisterCommandHandler("findtype", FindType);
            //Interpreter.RegisterCommandHandler("fly", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("land", UnimplementedCommand);

            //Interpreter.RegisterCommandHandler("useobject", UseObject);
            //Interpreter.RegisterCommandHandler("moveitem", MoveItem);






            //Interpreter.RegisterExpressionHandler("findobject", ExpFindObject);

            //#region Deprecated (but supported)
            //Interpreter.RegisterCommandHandler("clearuseonce", Deprecated);
            //#endregion

            //#region Deprecated (not supported)
            //Interpreter.RegisterCommandHandler("autoloot", Deprecated);
            //Interpreter.RegisterCommandHandler("toggleautoloot", Deprecated);
            //#endregion



            //Interpreter.RegisterCommandHandler("msg", Msg);

            //Interpreter.RegisterCommandHandler("pause", Pause);






            //Interpreter.RegisterCommandHandler("shownames", ShowNames);
            //Interpreter.RegisterCommandHandler("togglehands", ToggleHands);
            //Interpreter.RegisterCommandHandler("equipitem", EquipItem);
            ////Interpreter.RegisterCommandHandler("dress", DressCommand);
            ////Interpreter.RegisterCommandHandler("undress", UnDressCommand);
            ////Interpreter.RegisterCommandHandler("dressconfig", DressConfig);
            ////Interpreter.RegisterCommandHandler("togglescavenger", ToggleScavenger);

            ////Interpreter.RegisterCommandHandler("promptalias", PromptAlias);
            ////Interpreter.RegisterCommandHandler("waitforgump", WaitForGump);
            ////Interpreter.RegisterCommandHandler("clearjournal", ClearJournal);
            ////Interpreter.RegisterCommandHandler("waitforjournal", WaitForJournal);

            ////Interpreter.RegisterCommandHandler("clearlist", ClearList);
            ////Interpreter.RegisterCommandHandler("ping", Ping);
            ////Interpreter.RegisterCommandHandler("resync", Resync);
            ////Interpreter.RegisterCommandHandler("messagebox", MessageBox);
            ////Interpreter.RegisterCommandHandler("paperdoll", Paperdoll);
            ////Interpreter.RegisterCommandHandler("headmsg", HeadMsg);
            ////Interpreter.RegisterCommandHandler("sysmsg", SysMsg);
            ////Interpreter.RegisterCommandHandler("cast", Cast);
            ////Interpreter.RegisterCommandHandler("waitfortarget", WaitForTarget);
            ////Interpreter.RegisterCommandHandler("canceltarget", CancelTarget);
            ////Interpreter.RegisterCommandHandler("target", Target);
            ////Interpreter.RegisterCommandHandler("targettype", TargetType);
            ////Interpreter.RegisterCommandHandler("targetground", TargetGround);
            ////Interpreter.RegisterCommandHandler("targettile", TargetTile);
            ////Interpreter.RegisterCommandHandler("targettileoffset", TargetTileOffset);
            ////Interpreter.RegisterCommandHandler("targettilerelative", TargetTileRelative);
            ////Interpreter.RegisterCommandHandler("settimer", SetTimer);
            ////Interpreter.RegisterCommandHandler("removetimer", RemoveTimer);
            ////Interpreter.RegisterCommandHandler("createtimer", CreateTimer);




            //Interpreter.RegisterCommandHandler("info", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("playmacro", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("playsound", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("snapshot", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("hotkeys", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("where", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("mapuo", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("clickscreen", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("helpbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("guildbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("questsbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("logoutbutton", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("virtue", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("partymsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("guildmsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("allymsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("whispermsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("yellmsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("chatmsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("emotemsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("promptmsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("timermsg", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("waitforprompt", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("cancelprompt", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("addfriend", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("removefriend", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("contextmenu", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("waitforcontext", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("ignoreobject", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("clearignorelist", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("setskill", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("waitforproperties", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autocolorpick", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("waitforcontents", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("miniheal", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("bigheal", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("chivalryheal", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("moveitemoffset", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("movetype", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("movetypeoffset", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("togglemounted", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("equipwand", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("buy", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("sell", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("clearbuy", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("clearsell", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("organizer", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("counter", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("replygump", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("closegump", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("cleartargetqueue", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargetlast", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargetself", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargetobject", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargettype", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargettile", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargettileoffset", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargettilerelative", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargetghost", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("autotargetground", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("cancelautotarget", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("getenemy", UnimplementedCommand);
            //Interpreter.RegisterCommandHandler("getfriend", UnimplementedCommand);
        }

        private static bool SetAbility(CommandExecution execution)
        {
            var ability = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory).ToLower();
            var toggle = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory).ToLower();
            if (toggle == "on")
            {
                switch (ability)
                {
                    case "primary":
                        GameActions.UsePrimaryAbility();
                        break;
                    case "secondary":
                        GameActions.UseSecondaryAbility();
                        break;
                    case "stun":
                        GameActions.RequestStun();
                        break;
                    case "disarm":
                        GameActions.RequestDisarm();
                        break;
                }
            }
            else
            {
                GameActions.ClearAbility();
            }
            return true;
        }
        private static bool Attack(CommandExecution execution)
        {
            var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            GameActions.Attack(serial);
            return true;
        }

        private static bool ClearHands(CommandExecution execution)
        {
            var hand = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory).ToLower();
            if (hand == "both")
            {
                GameActions.ClearEquipped(IO.ItemExt_PaperdollAppearance.Left);
                GameActions.ClearEquipped(IO.ItemExt_PaperdollAppearance.Right);
            }
            else GameActions.ClearEquipped((IO.ItemExt_PaperdollAppearance)Enum.Parse(typeof(Direction), hand, true));
            return true;
        }

        private static bool ClickObject(CommandExecution execution)
        {
            var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            GameActions.SingleClick(serial);
            return true;
        }

        private static bool BandageSelf(CommandExecution execution)
        {
            GameActions.BandageSelf();
            // TODO: maybe return false if no badages or other constraints?
            return true;
        }

        private static bool UseType(CommandExecution execution)
        {
            Item item = CmdFindItemByGraphic(
                execution.ArgList.NextAs<ushort>(ArgumentList.Expectation.Mandatory),
                execution.ArgList.NextAs<ushort>(),
                execution.ArgList.NextAs<uint>(),
                0,
                execution.ArgList.NextAs<int>()
                );

            if (item != null)
            {
                GameActions.DoubleClick(item.Serial);
                return true;
            }
            else return false;
        }

        private static bool UseObject(CommandExecution execution)
        {
            var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            GameActions.DoubleClick(serial);
            return true;
        }

        private static bool UseOnce(CommandExecution execution)
        {
            Item item = CmdFindItemByGraphic(
                execution.ArgList.NextAs<ushort>(ArgumentList.Expectation.Mandatory),
                execution.ArgList.NextAs<ushort>()
                );

            if (item != null)
            {
                GameActions.DoubleClick(item.Serial);
                return true;
            }
            else return false;
        }

        private static bool MoveItem(CommandExecution execution)
        {
            var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            var destination = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            var x = execution.ArgList.NextAs<int>();
            var y = execution.ArgList.NextAs<int>();
            var z = execution.ArgList.NextAs<int>();
            var amount = execution.ArgList.NextAs<int>();

            if (destination == 0 || destination == uint.MaxValue)
            {
                GameActions.Print("moveitem: destination not found");
                return false;
            }
            else
            {
                GameActions.PickUp(serial, 0, 0, amount);
                GameActions.DropItem(serial, 0xFFFF, 0xFFFF, 0, destination);
                return true;
            }
           
        }

        private static bool MoveItemOffset(CommandExecution execution)
        {
            var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            var destination = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            if(destination != uint.MaxValue) // GROUND is already defined as MaxValue for a unsigned int inside the GameActions class (for example in the DropItem method)
            {
                throw new ScriptSyntaxError("ops", null);
            }
            var x = World.Player.X + execution.ArgList.NextAs<int>();
            var y = World.Player.Y + execution.ArgList.NextAs<int>() + 1 /*adding temporary due to issue with my dev env*/;
            var z = World.Map.GetTileZ(x, y) + execution.ArgList.NextAs<int>();

            var amount = execution.ArgList.NextAs<int>();

            GameActions.PickUp(serial, 0, 0, amount);
            GameActions.DropItem(serial, x, y, z, destination);

            return true;
        }

        private static bool MoveType(CommandExecution execution)
        {
            var graphic = execution.ArgList.NextAs<ushort>(ArgumentList.Expectation.Mandatory);
            var source = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            var destination = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            var x = execution.ArgList.NextAs<int>();
            var y = execution.ArgList.NextAs<int>();
            var z = execution.ArgList.NextAs<int>();
            var color = execution.ArgList.NextAs<ushort>();
            var amount = execution.ArgList.NextAs<int>();
            var range = execution.ArgList.NextAs<int>();

            Item item = CmdFindItemByGraphic(graphic, color, source, amount, range);
            if (item != null)
            {
                GameActions.PickUp(item.Serial, 0, 0, amount);
                GameActions.DropItem(item.Serial, 0xFFFF, 0xFFFF, 0, destination);
            }
            return true;
        }

        private static bool MoveTypeOffset(CommandExecution execution)
        {
            var graphic = execution.ArgList.NextAs<ushort>(ArgumentList.Expectation.Mandatory);
            var source = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            var destination = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            if (destination != uint.MaxValue) // GROUND is already defined as MaxValue for a unsigned int inside the GameActions class (for example in the DropItem method)
            {
                throw new ScriptSyntaxError("ops", null);
            }

            var x = World.Player.X + execution.ArgList.NextAs<int>();
            var y = World.Player.Y + execution.ArgList.NextAs<int>() + 1 /*adding temporary due to issue with my dev env*/;
            var z = World.Map.GetTileZ(x, y) + execution.ArgList.NextAs<int>();
            var color = execution.ArgList.NextAs<ushort>();
            var amount = execution.ArgList.NextAs<int>();
            var range = execution.ArgList.NextAs<int>();


            Item item = CmdFindItemByGraphic(graphic, color, source, amount, range);
            if (item != null)
            {
                GameActions.PickUp(item.Serial, 0, 0, amount);
                GameActions.DropItem(item.Serial, x, y, z, destination);
            }
            return true;
        }

        public static Command.Handler MovementLogic(bool forceRun = false)
        {
            return (execution) => {
                // Be prepared for multiple directions -> walk "North, East, East, West, South, Southeast"
                var dirArray = execution.ArgList.NextAsArray<string>(ArgumentList.Expectation.Mandatory);

                // At least one is mandatory, so perform walk command on it
                var direction = (Direction)Enum.Parse(typeof(Direction), dirArray[0], true);
                World.Player.Walk(direction, forceRun || ProfileManager.CurrentProfile.AlwaysRun);

                // For all remaining, explode it as one command per single direction
                for (int i = 1; i < dirArray.Length; i++)
                {
                    // So queue it again with one less arg in the list
                    VirtualArgument arg = new VirtualArgument(dirArray[i]);
                    ArgumentList newParams = new ArgumentList(new Argument[1] { arg }, execution.Cmd.ArgTypes);
                    Command.Queues[execution.Cmd.Attribute].Enqueue(new CommandExecution(execution.Cmd, newParams, execution.Quiet, execution.Force));
                }

                return true;
            };
        }

        private static bool UseSkill(CommandExecution execution)
        {
            var skill = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory);
            if (skill == "last")
                GameActions.UseLastSkill();
            else if (!GameActions.UseSkill(skill))
                throw new ScriptRunTimeError(null, "That skill  is not usable");
            return true;
        }

        private static bool Feed(CommandExecution execution)
        {
            try
            {
                var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
                List<ushort> foodList = new List<ushort>();
                //ushort graphic;
                //if (!args.TryAsGraphic(out graphic))
                //{
                //    var source = args.NextAsSource(ParameterList.ArgumentType.Mandatory);
                //    destination = World.Player.FindItemByLayer((Layer)source).Serial;
                //}
                //else foodList.Add(graphic)
                //var color = args.NextAs<ushort>();
                //var amount = args.NextAs<int>();

                //Item item = CmdFindObjectBySerial(serial, color);//, source, range);
                //return item != null && item.Amount > amount;
                return true;
            }
            catch (ScriptRunTimeError ex)
            {
                throw new ScriptSyntaxError("Usage: feed (serial) ('food name'/'food group'/'any'/graphic) [color] [amount]", ex);
            }
        }

        private static bool Rename(CommandExecution execution)
        {
            GameActions.Rename(
                execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory),
                execution.ArgList.NextAs<string>());
            return true;
        }

        private static bool ShowNames(CommandExecution execution)
        {
            var names = execution.ArgList.NextAs<string>();
            if (names == string.Empty)
                names = "all";

            var target = (GameActions.AllNamesTargets)Enum.Parse(typeof(GameActions.AllNamesTargets), names, true);
            GameActions.AllNames(target);
            return true;
        }

        public static bool ToggleHands(CommandExecution execution)
        {
            var hand = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory).ToLower();
            GameActions.ClearEquipped((IO.ItemExt_PaperdollAppearance)Enum.Parse(typeof(Direction), hand, true));
            return true;
        }

        public static bool EquipItem(CommandExecution execution)
        {
            var item = World.GetOrCreateItem(execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory));
            GameActions.PickUp(item, 0, 0, 1);
            // We could make the layer parameter optimal, allowing us to just call GameActions.Equip (but this would be different from UO Steam)
            GameActions.Equip((Layer)execution.ArgList.NextAs<int>(ArgumentList.Expectation.Mandatory));
            return true;
        }

        public static bool FindObject(CommandExecution execution)
        {
            Entity entity = CmdFindEntityBySerial(
                execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory),
                execution.ArgList.NextAs<ushort>(),
                execution.ArgList.NextAs<uint>(),
                execution.ArgList.NextAs<int>(),
                execution.ArgList.NextAs<int>()
                );
            if(entity != null)
            {
                Aliases.Write<uint>("found", entity.Serial);
                return true;
            }
            else return false;
        }

        private static bool PopList(CommandExecution execution)
        {
            var listName = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory);
            var value = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory);

            if (value == "front")
            {
                if (execution.Force)
                    while (Interpreter.PopList(listName, true)) { }
                else
                    Interpreter.PopList(listName, true);
            }
            else if (value == "back")
            {
                if (execution.Force)
                    while (Interpreter.PopList(listName, false)) { }
                else
                    Interpreter.PopList(listName, false);
            }
            else
            {
                if (execution.Force)
                    while (Interpreter.PopList(listName, execution.ArgList[1])) { }
                else
                    Interpreter.PopList(listName, execution.ArgList[1]);
            }

            return true;
        }

        private static bool RemoveList(CommandExecution execution)
        {
            var listName = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory);
            Interpreter.DestroyList(listName);
            return true;
        }

        private static bool CreateList(CommandExecution execution)
        {
            var listName = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory);
            Interpreter.CreateList(listName);
            return true;
        }

        private static bool ClearList(CommandExecution execution)
        {
            var listName = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory);
            Interpreter.ClearList(listName);
            return true;
        }

        private static bool PushList(CommandExecution execution)
        {
            var name = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory);
            var values = execution.ArgList.NextAsArray<string>(ArgumentList.Expectation.Mandatory);
            var pos = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory);
            bool front = (pos == "force");
            foreach (var val in values)
            {
                //Interpreter.PushList(name, new Argument(), front, force);
            }
            return true;
        }

        public static bool Msg(CommandExecution execution)
        {
            GameActions.Say(
                execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory),
                hue: execution.ArgList.NextAs<ushort>()
                );
            return true;
        }

        private static bool SetAlias(CommandExecution execution)
        {
            Aliases.Write<uint>(
                execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory),
                execution.ArgList.NextAs<uint>()
                );
            return true;
        }

        private static bool UnsetAlias(CommandExecution execution)
        {
            Aliases.Remove(typeof(uint), execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory));
            return true;
        }

        private static bool FindType(CommandExecution execution)
        {
            Item item = CmdFindItemByGraphic(
                execution.ArgList.NextAs<ushort>(ArgumentList.Expectation.Mandatory),
                execution.ArgList.NextAs<ushort>(),
                execution.ArgList.NextAs<uint>(),
                execution.ArgList.NextAs<int>(),
                execution.ArgList.NextAs<int>()
                );

            if (item != null)
            {
                Aliases.Write<uint>("found", item.Serial);
                return true;
            }
            else return false;
        }

        private static bool PromptAlias(CommandExecution execution)
        {
            // ATTENTION
            // This method needs to change as Sleep is unacceptable. The caveat is the current Wait logic does not support part of execution running before it.
            // Meaning that for the full Prompt logic, we would like to start the targeting process, than wait until a target is selected, and only than update the Alias.
            //But current wait logic fully proceed the execution of the command… something for us to talk more.

            Interpreter.Pause(60000);
            if (TargetManager.IsTargeting)
            {
                TargetManager.CancelTarget();
            }
            var alias = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory);
            GameActions.Print("Select the object '" + alias  + "'");
            Task.Run(() =>
            {
                var currentTarget = TargetManager.LastTargetInfo.Serial;
                DateTime targetingStarted = DateTime.UtcNow;
                TargetManager.SetTargeting(CursorTarget.Object, 0, TargetType.Neutral);
                while (TargetManager.LastTargetInfo.Serial == currentTarget && DateTime.UtcNow - targetingStarted < TimeSpan.FromSeconds(50))
                {
                    Thread.Sleep(25);
                }
                Aliases.Write<uint>(
                    alias,
                    TargetManager.LastTargetInfo.Serial
                    );
                Interpreter.Unpause();
            });
           

            //if (!_hasPrompt)
            //{
            //    _hasPrompt = true;
            //    Targeting.OneTimeTarget((location, serial, p, gfxid) =>
            //    {
                    
            //    });
            //    return false;
            //}

            //_hasPrompt = false;
            return true;
        }

        //private static bool UnimplementedCommand(string command, ParameterList args)
        //{
        //    GameActions.Print($"Unimplemented command: '{command}'", type: MessageType.System);
        //    return true;
        //}

        //private static bool Deprecated(string command, ParameterList args)
        //{
        //    GameActions.Print($"Deprecated command: '{command}'", type: MessageType.System);
        //    return true;
        //}



        ////private static bool ExpFindObject(string expression, ParameterList args, bool quiet)
        ////{
        ////    return FindObject(expression, args);
        ////}



        ////private static bool UseItem(Item cont, ushort find)
        ////{
        ////    for (int i = 0; i < cont.Contains.Count; i++)
        ////    {
        ////        Item item = cont.Contains[i];

        ////        if (item.ItemID == find)
        ////        {
        ////            PlayerData.DoubleClick(item);
        ////            return true;
        ////        }
        ////        else if (item.Contains != null && item.Contains.Count > 0)
        ////        {
        ////            if (UseItem(item, find))
        ////                return true;
        ////        }
        ////    }

        ////    return false;
        ////}





        ////private static bool PromptAlias(string command, ParameterList args)
        ////{
        ////    Interpreter.Pause(60000);

        ////    if (!_hasPrompt)
        ////    {
        ////        _hasPrompt = true;
        ////        Targeting.OneTimeTarget((location, serial, p, gfxid) =>
        ////        {
        ////            Interpreter.SetAlias(args[0].As<string>(), serial);
        ////            Interpreter.Unpause();
        ////        });
        ////        return false;
        ////    }

        ////    _hasPrompt = false;
        ////    return true;
        ////}

        ////private static bool WaitForGump(string command, ParameterList args)
        ////{
        ////    if (args.Length < 2)
        ////        throw new RunTimeError(null, "Usage: waitforgump (gump id/'any') (timeout)");

        ////    bool any = args[0].As<string>() == "any";

        ////    if (any)
        ////    {
        ////        if (World.Player.HasGump || World.Player.HasCompressedGump)
        ////            return true;
        ////    }
        ////    else
        ////    {
        ////        uint gumpId = args[0].AsSerial();

        ////        if (World.Player.CurrentGumpI == gumpId)
        ////            return true;
        ////    }

        ////    Interpreter.Timeout(args[1].AsUInt(), () => { return true; });
        ////    return false;
        ////}

        ////private static bool ClearJournal(string command, ParameterList args)
        ////{
        ////    Journal.Clear();

        ////    return true;
        ////}

        ////private static bool WaitForJournal(string command, ParameterList args)
        ////{
        ////    if (args.Length < 2)
        ////        throw new RunTimeError(null, "Usage: waitforjournal ('text') (timeout) ['author'/'system']");

        ////    if (!Journal.ContainsSafe(args[0].As<string>()))
        ////    {
        ////        Interpreter.Timeout(args[1].AsUInt(), () => { return true; });
        ////        return false;
        ////    }

        ////    return true;
        ////}

        ////public static bool ToggleScavenger(string command, ParameterList args)
        ////{
        ////    ScavengerAgent.Instance.ToggleEnabled();

        ////    return true;
        ////}

        //private static bool Pause(string command, ParameterList args)
        //{
        //    if (args.Length == 0)
        //        throw new ScriptRunTimeError(null, "Usage: pause (timeout)");

        //    Interpreter.Pause(args[0].As<uint>());
        //    return true;
        //}

        ////private static bool Ping(string command, ParameterList args)
        ////{
        ////    Assistant.Ping.StartPing(5);

        ////    return true;
        ////}

        ////private static bool Resync(string command, ParameterList args)
        ////{
        ////    Client.Instance.SendToServer(new ResyncReq());

        ////    return true;
        ////}

        ////private static bool MessageBox(string command, ParameterList args)
        ////{
        ////    if (args.Length != 2)
        ////        throw new RunTimeError(null, "Usage: messagebox ('title') ('body')");

        ////    System.Windows.Forms.MessageBox.Show(args[0].As<string>(), args[1].As<string>());

        ////    return true;
        ////}



        //private static bool Paperdoll(string command, ParameterList args)
        //{
        //    if (args.Length > 1)
        //        throw new RunTimeError(null, "Usage: paperdoll [serial]");

        //    uint serial = args.Length == 0 ? World.Player.Serial.Value : args[0].AsSerial();
        //    Client.Instance.SendToServer(new DoubleClick(serial));

        //    return true;
        //}

        //public static bool Cast(string command, ParameterList args)
        //{
        //    if (args.Length == 0)
        //        throw new RunTimeError(null, "Usage: cast 'spell' [serial]");

        //    Spell spell;

        //    if (int.TryParse(args[0].As<string>(), out int spellnum))
        //        spell = Spell.Get(spellnum);
        //    else
        //        spell = Spell.GetByName(args[0].As<string>());
        //    if (spell != null)
        //    {
        //        if (args.Length > 1)
        //        {
        //            Serial s = args[1].AsSerial();
        //            if (force)
        //                Targeting.ClearQueue();
        //            if (s > Serial.Zero && s != Serial.MinusOne)
        //            {
        //                Targeting.Target(s);
        //            }
        //            else if (!quiet)
        //                throw new RunTimeError(null, "cast - invalid serial or alias");
        //        }
        //    }
        //    else if (!quiet)
        //        throw new RunTimeError(null, "cast - spell name or number not valid");

        //    return true;
        //}

        //private static bool WaitForTarget(string command, ParameterList args)
        //{
        //    if (args.Length != 1)
        //        throw new RunTimeError(null, "Usage: waitfortarget (timeout)");

        //    if (Targeting.HasTarget)
        //        return true;

        //    Interpreter.Timeout(args[0].AsUInt(), () => { return true; });
        //    return false;
        //}

        //private static bool CancelTarget(string command, ParameterList args)
        //{
        //    if (args.Length != 0)
        //        throw new RunTimeError(null, "Usage: canceltarget");

        //    if (Targeting.HasTarget)
        //        Targeting.CancelOneTimeTarget();

        //    return true;
        //}

        //private static bool Target(string command, ParameterList args)
        //{
        //    if (args.Length != 1)
        //        throw new RunTimeError(null, "Usage: target (serial)");

        //    if (!Targeting.HasTarget)
        //        ScriptManager.Error(quiet, command, "No target cursor available. Consider using waitfortarget.");
        //    else
        //        Targeting.Target(args[0].AsSerial());

        //    return true;
        //}

        //private static bool TargetType(string command, ParameterList args)
        //{
        //    if (args.Length < 1 || args.Length > 3)
        //        throw new RunTimeError(null, "Usage: targettype (graphic) [color] [range]");

        //    if (!Targeting.HasTarget)
        //    {
        //        ScriptManager.Error(quiet, command, "No target cursor available. Consider using waitfortarget.");
        //        return true;
        //    }

        //    var graphic = args[0].AsInt();

        //    uint serial = Serial.MinusOne;

        //    switch (args.Length)
        //    {
        //        case 1:
        //            // Only graphic
        //            serial = World.FindItemByType(graphic).Serial;
        //            break;
        //        case 2:
        //            {
        //                // graphic and color
        //                var color = args[1].AsUShort();
        //                foreach (var item in World.Items.Values)
        //                {
        //                    if (item.ItemID.Value == graphic && item.Hue == color)
        //                    {
        //                        serial = item.Serial;
        //                        break;
        //                    }
        //                }
        //                break;
        //            }
        //        case 3:
        //            {
        //                // graphic, color, range
        //                var color = args[1].AsUShort();
        //                var range = args[2].AsInt();
        //                foreach (var item in World.Items.Values)
        //                {
        //                    if (item.ItemID.Value == graphic && item.Hue == color && Utility.Distance(item.Position, World.Player.Position) < range)
        //                    {
        //                        serial = item.Serial;
        //                        break;
        //                    }
        //                }
        //                break;
        //            }

        //    }

        //    if (serial == Serial.MinusOne)
        //        throw new RunTimeError(null, "Unable to find suitable target");

        //    Targeting.Target(serial);
        //    return true;
        //}

        //private static bool TargetGround(string command, ParameterList args)
        //{
        //    if (args.Length < 1 || args.Length > 3)
        //        throw new RunTimeError(null, "Usage: targetground (graphic) [color] [range]");

        //    throw new RunTimeError(null, $"Unimplemented command {command}");
        //}

        //private static bool TargetTile(string command, ParameterList args)
        //{
        //    if (!(args.Length == 1 || args.Length == 3))
        //        throw new RunTimeError(null, "Usage: targettile ('last'/'current'/(x y z))");

        //    if (!Targeting.HasTarget)
        //    {
        //        ScriptManager.Error(quiet, command, "No target cursor available. Consider using waitfortarget.");
        //        return true;
        //    }

        //    Point3D position = Point3D.MinusOne;

        //    switch (args.Length)
        //    {
        //        case 1:
        //            {
        //                var alias = args[0].As<string>();
        //                if (alias == "last")
        //                {
        //                    if (Targeting.LastTargetInfo.Type != 1)
        //                        throw new RunTimeError(null, "Last target was not a ground target");

        //                    position = new Point3D(Targeting.LastTargetInfo.X, Targeting.LastTargetInfo.Y, Targeting.LastTargetInfo.Z);
        //                }
        //                else if (alias == "current")
        //                {
        //                    position = World.Player.Position;
        //                }
        //                break;
        //            }
        //        case 3:
        //            position = new Point3D(args[0].AsInt(), args[1].AsInt(), args[2].AsInt());
        //            break;
        //    }

        //    if (position == Point3D.MinusOne)
        //        throw new RunTimeError(null, "Usage: targettile ('last'/'current'/(x y z))");

        //    Targeting.Target(position);
        //    return true;
        //}

        //private static bool TargetTileOffset(string command, ParameterList args)
        //{
        //    if (args.Length != 3)
        //        throw new RunTimeError(null, "Usage: targettileoffset (x y z)");

        //    if (!Targeting.HasTarget)
        //    {
        //        ScriptManager.Error(quiet, command, "No target cursor available. Consider using waitfortarget.");
        //        return true;
        //    }

        //    var position = World.Player.Position;

        //    position.X += args[0].AsInt();
        //    position.Y += args[1].AsInt();
        //    position.Z += args[2].AsInt();

        //    Targeting.Target(position);
        //    return true;
        //}

        //private static bool TargetTileRelative(string command, ParameterList args)
        //{
        //    if (args.Length != 2)
        //        throw new RunTimeError(null, "Usage: targettilerelative (serial) (range). Range may be negative.");

        //    if (!Targeting.HasTarget)
        //    {
        //        ScriptManager.Error(quiet, command, "No target cursor available. Consider using waitfortarget.");
        //        return true;
        //    }

        //    var serial = args[0].AsSerial();
        //    var range = args[1].AsInt();

        //    var mobile = World.FindMobile(serial);

        //    if (mobile == null)
        //    {
        //        /* TODO: Search items if mobile not found. Although this isn't very useful. */
        //        ScriptManager.Error(quiet, command, "item or mobile not found.");
        //        return true;
        //    }

        //    var position = mobile.Position;

        //    switch (mobile.Direction)
        //    {
        //        case Direction.North:
        //            position.Y -= range;
        //            break;
        //        case Direction.Right:
        //            position.X += range;
        //            position.Y -= range;
        //            break;
        //        case Direction.East:
        //            position.X += range;
        //            break;
        //        case Direction.Down:
        //            position.X += range;
        //            position.Y += range;
        //            break;
        //        case Direction.South:
        //            position.Y += range;
        //            break;
        //        case Direction.Left:
        //            position.X -= range;
        //            position.Y += range;
        //            break;
        //        case Direction.West:
        //            position.X -= range;
        //            break;
        //        case Direction.Up:
        //            position.X -= range;
        //            position.Y -= range;
        //            break;
        //    }

        //    Targeting.Target(position);

        //    return true;
        //}

        //public static bool HeadMsg(string command, ParameterList args)
        //{
        //    switch (args.Length)
        //    {
        //        case 1:
        //            World.Player.OverheadMessage(Config.GetInt("SysColor"), args[0].As<string>());
        //            break;
        //        case 2:
        //            World.Player.OverheadMessage(args[1].AsInt(), args[0].As<string>());
        //            break;
        //        case 3:
        //            Mobile m = World.FindMobile(args[2].AsSerial());

        //            if (m != null)
        //                m.OverheadMessage(args[1].AsInt(), args[0].As<string>());
        //            break;
        //        default:
        //            throw new RunTimeError(null, "Usage: headmsg (text) [color] [serial]");
        //    }

        //    return true;
        //}

        //public static bool SysMsg(string command, ParameterList args)
        //{
        //    switch (args.Length)
        //    {
        //        case 1:
        //            World.Player.SendMessage(Config.GetInt("SysColor"), args[0].As<string>());
        //            break;
        //        case 2:
        //            World.Player.SendMessage(args[1].AsInt(), args[0].As<string>());
        //            break;
        //        default:
        //            throw new RunTimeError(null, "Usage: sysmsg ('text') [color]");
        //    }

        //    return true;
        //}

        //public static bool DressCommand(string command, ParameterList args)
        //{
        //    //we're using a named dresslist or a temporary dresslist?
        //    if (args.Length == 0)
        //    {
        //        if (DressList._Temporary != null)
        //            DressList._Temporary.Dress();
        //        else if (!quiet)
        //            throw new RunTimeError(null, "No dresslist specified and no temporary dressconfig present - usage: dress ['dresslist']");
        //    }
        //    else
        //    {
        //        var d = DressList.Find(args[0].As<string>());
        //        if (d != null)
        //            d.Dress();
        //        else if (!quiet)
        //            throw new RunTimeError(null, $"dresslist {args[0].As<string>()} not found");
        //    }

        //    return true;
        //}

        //public static bool UnDressCommand(string command, ParameterList args)
        //{
        //    //we're using a named dresslist or a temporary dresslist?
        //    if (args.Length == 0)
        //    {
        //        if (DressList._Temporary != null)
        //            DressList._Temporary.Undress();
        //        else if (!quiet)
        //            throw new RunTimeError(null, "No dresslist specified and no temporary dressconfig present - usage: undress ['dresslist']");
        //    }
        //    else
        //    {
        //        var d = DressList.Find(args[0].As<string>());
        //        if (d != null)
        //            d.Undress();
        //        else if (!quiet)
        //            throw new RunTimeError(null, $"dresslist {args[0].As<string>()} not found");
        //    }

        //    return true;
        //}

        //public static bool DressConfig(string command, ParameterList args)
        //{
        //    if (DressList._Temporary == null)
        //        DressList._Temporary = new DressList("dressconfig");

        //    DressList._Temporary.Items.Clear();
        //    for (int i = 0; i < World.Player.Contains.Count; i++)
        //    {
        //        Item item = World.Player.Contains[i];
        //        if (item.Layer <= Layer.LastUserValid && item.Layer != Layer.Backpack && item.Layer != Layer.Hair &&
        //            item.Layer != Layer.FacialHair)
        //            DressList._Temporary.Items.Add(item.Serial);
        //    }

        //    return true;
        //}

        //private static bool SetTimer(string command, ParameterList args)
        //{
        //    if (args.Length != 2)
        //        throw new RunTimeError(null, "Usage: settimer (timer name) (value)");


        //    Interpreter.SetTimer(args[0].As<string>(), args[1].AsInt());
        //    return true;
        //}

        //private static bool RemoveTimer(string command, ParameterList args)
        //{
        //    if (args.Length != 1)
        //        throw new RunTimeError(null, "Usage: removetimer (timer name)");

        //    Interpreter.RemoveTimer(args[0].As<string>());
        //    return true;
        //}

        //private static bool CreateTimer(string command, ParameterList args)
        //{
        //    if (args.Length != 1)
        //        throw new RunTimeError(null, "Usage: createtimer (timer name)");

        //    Interpreter.CreateTimer(args[0].As<string>());
        //    return true;
        //}

        // HELPER FUNCTIONS FOR THE COMMANDS

        private static Entity CmdFindEntityBySerial(uint serial, ushort color = ushort.MaxValue, uint source = 0, int amount = 0, int range = 0)
        {
            // Try fetching and Item from Serial
            Entity entity = null;
            var graphic = World.GetOrCreateItem(serial).Graphic;

            if (source == 0 /*Zero value is Any*/)
            {
                // Any also look at the ground if not found in player belongings
                entity = World.Player.FindItem(graphic, color);
                if (entity != null)
                    entity = World.Player.FindItemByTypeOnGroundWithHueInRange(graphic, color, range);
            }
            else if (source == uint.MaxValue /*Max value is Ground*/)
                entity = World.Player.FindItemByTypeOnGroundWithHueInRange(graphic, color, range);
            else
            {
                //var layer = (Layer)Enum.Parse(typeof(Layer), source, true);
                //entity = World.Player.FindItemByLayer(layer)?.FindItem(graphic, color);
                entity = World.GetOrCreateItem(source).FindItem(graphic, color);
            }

            // If we have an item, check ammount
            if (entity != null && entity is Item item && item.Amount < amount)
                return null;

            // Try fetching a Mobile from Serial
            if (entity == null)
                entity = World.GetOrCreateMobile(serial);
            
            return entity;
        }

        private static Item CmdFindItemByGraphic(ushort graphic, ushort color = ushort.MaxValue, uint source = 0, int amount = 0, int range = 0)
        {
            Item item = null;
            if (source == 0 /*Zero value is Any*/)
            {
                item = World.Player.FindItem(graphic, color);
                if (item != null) // For Any, also look at the ground if not found in player belongings
                    item = World.Player.FindItemByTypeOnGroundWithHueInRange(graphic, color, range);
            }
            else if (source == uint.MaxValue /*Max value is Ground*/)
                item = World.Player.FindItemByTypeOnGroundWithHueInRange(graphic, color, range);
            else
            {
                item = World.GetOrCreateItem(source).FindItem(graphic, color);
                //var layer = (Layer)Enum.Parse(typeof(Layer), source, true);
                //item = World.Player.FindItemByLayer(layer)?.FindItem(graphic, color);
            }

            if (item != null && item.Amount < amount)
                return null;
            else return item;
        }

        // Wait for a given amount of milliseconds (using "curry" technique for param reduction)
        public static Command.Handler WaitForMs(int waitTime)
        {
            return (execution) => { return (execution.CreationTime - DateTime.Now > TimeSpan.FromMilliseconds(waitTime)); };
        }

        public static bool WaitForMovement(CommandExecution execution)
        { 
            // Based on command and settings we select the expected delay
            var movementDelay = Constants.PLAYER_WALKING_DELAY * 3.0;    // walk default delay
            if (World.Player.FindItemByLayer(Layer.Mount) != null)
                movementDelay = Constants.PLAYER_WALKING_DELAY * 2.0;    // mount default delay
            else if (ProfileManager.CurrentProfile.AlwaysRun || execution.Cmd.Keyword == "run")
                movementDelay = Constants.PLAYER_WALKING_DELAY * 2.0;    // run default delay
            else if (execution.Cmd.Keyword == "turn")
                movementDelay = Constants.TURN_DELAY * 2.0;              // turn default delay

            return ((DateTime.UtcNow - CommandExecution.LastExecuted(execution.Cmd.Keyword)).TotalMilliseconds > movementDelay);
        }
    }
}
