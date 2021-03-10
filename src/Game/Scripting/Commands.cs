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
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Network;
using SDL2;

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

        // Provide access to the time a given command was last executed
        private static Dictionary<string, uint> _lasCheckedRegistry = new Dictionary<string, uint>();

        public uint LastChecked
        {
            get
            {
                if (!_lasCheckedRegistry.ContainsKey(Cmd.Keyword))
                    _lasCheckedRegistry[Cmd.Keyword] = ClassicUO.Time.Ticks;
                return _lasCheckedRegistry[Cmd.Keyword];
            }
        }

        // Build execution
        public CommandExecution(Command command, ArgumentList argList, bool quiet, bool force)
        {
            Cmd = command;
            ArgList = argList;
            Quiet = quiet;
            Force = force;
           
        }

        // This method check if it is time to execute the command and if the time is right, perform the execution
        // Logic inside may be complex, checking for a given target or waiting for a given gump
        public bool Process()
        {
            if (Cmd.WaitLogic(this)) // check if waiting is over (no blocking, we keep checking as Razor does)
            {
                try
                {
                    _lasCheckedRegistry[Cmd.Keyword] = ClassicUO.Time.Ticks; // store time of this execution (as the last execution)
                    return Cmd.ExecutionLogic(this); // execute the command and do the magic
                }
                catch(ScriptRunTimeError ex)
                {
                    GameActions.Print(Cmd.Keyword + ": " + ex.Message, type: MessageType.System);
                    Interpreter.ClearTimeout();
                    return true;
                }
            }
            else return false;
        }
    }

    // Loosely types abstraction of a command using strings to define expected usage and types
    public class Command
    {
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

        // Several flavors of constructors for quality of life
        #region Constructors
        public Command(string usage, Handler executionLogic, Handler waitLogic)
        {
            Usage = usage;
            ExecutionLogic = executionLogic;
            WaitLogic = waitLogic;

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
            return execution.Process();
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
        // Agent related storages (to be moved to Profile)?
        // Profiles are very similar to lists, but instead of string containg serials
        // Commands such as Organizer and Dress use profiles
        static public Dictionary<string, List<uint>> Profiles = new Dictionary<string, List<uint>>();

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

            // Adding default profiles
            Profiles.Add("dressconfig-temporary", new List<uint>());
            Profiles.Add("temporary-generic", new List<uint>());

            // Add definitions for all supported commands
            AddDefinition(new Command("setability ('primary'/'secondary'/'stun'/'disarm') ['on'/'off']", SetAbility, WaitForMs()));
            AddDefinition(new Command("attack (serial)", Attack, WaitForMs()));
            AddDefinition(new Command("clearhands ('left'/'right'/'both')", ClearHands, WaitForMs()));
            AddDefinition(new Command("clickobject (serial)", ClickObject, WaitForMs()));
            AddDefinition(new Command("bandageself", BandageSelf, WaitForMs()));
            AddDefinition(new Command("usetype (graphic) [color] [source] [range or search level]", UseType, WaitForMs()));
            AddDefinition(new Command("useobject (serial)", UseObject, WaitForMs()));
            AddDefinition(new Command("useonce (graphic) [color]", UseOnce, WaitForMs()));
            AddDefinition(new Command("moveitem (serial) (destination) [(x, y, z)] [amount]", MoveItem, WaitForMs()));
            AddDefinition(new Command("moveitemoffset (serial) 'ground' [(x, y, z)] [amount]", MoveItemOffset, WaitForMs()));
            AddDefinition(new Command("movetype (graphic) (source) (destination) [(x, y, z)] [color] [amount] [range or search level]", MoveType, WaitForMs()));
            AddDefinition(new Command("movetypeoffset (graphic) (source) 'ground' [(x, y, z)] [color] [amount] [range or search level]", MoveTypeOffset, WaitForMs()));        
            AddDefinition(new Command("walk (direction)", MovementLogic(false), WaitForMovement));
            AddDefinition(new Command("turn (direction)", MovementLogic(false), WaitForMovement));
            AddDefinition(new Command("run (direction)", MovementLogic(true), WaitForMovement));
            AddDefinition(new Command("useskill ('skill name'/'last')", UseSkill, WaitForMs()));
            AddDefinition(new Command("feed (serial) ('food name'/'food group'/'any'/graphic) [color] [amount]", Feed, WaitForMs()));
            AddDefinition(new Command("rename (serial) ('name')", Rename, WaitForMs()));
            AddDefinition(new Command("shownames ['mobiles'/'corpses']", ShowNames, WaitForMs()));
            AddDefinition(new Command("togglehands ('left'/'right')", ToggleHands, WaitForMs()));
            // UO Steam: we are making it different from  UO Steam and allowing the layer to be optional
            AddDefinition(new Command("equipitem (serial) [layer]", EquipItem, WaitForMs()));
            AddDefinition(new Command("togglemounted", ToggleMounted, WaitForMs()));
            //AddDefinition(new Command("equipwand ('spell name'/'any'/'undefined') [minimum charges]", EquipWand, WaitForMs(500), Command.Attributes.ComplexInterAction));
            //AddDefinition(new Command("buy ('list name')", Buy, WaitForMs(500), Command.Attributes.ComplexInterAction));
            //AddDefinition(new Command("sell ('list name')", Sell, WaitForMs(500), Command.Attributes.ComplexInterAction));
            //AddDefinition(new Command("clearbuy", ClearBuy, WaitForMs(500), Command.Attributes.ComplexInterAction));
            //AddDefinition(new Command("clearsell", ClearSell, WaitForMs(500), Command.Attributes.ComplexInterAction));
            //AddDefinition(new Command("organizer ('profile name') [source] [destination]", Organizer, WaitForMs(500), Command.Attributes.ComplexInterAction));
            //AddDefinition(new Command("organizing", Organizing, WaitForMs(500), Command.Attributes.ComplexInterAction));
            AddDefinition(new Command("autoloot", UnsupportedCmd, WaitForMs()));
            AddDefinition(new Command("dress ['profile name']", Dress, WaitForMs()));
            AddDefinition(new Command("undress ['profile name']", Undress, WaitForMs()));
            AddDefinition(new Command("dressconfig", Dressconfig, WaitForMs()));
            AddDefinition(new Command("toggleautoloot", UnsupportedCmd, WaitForMs()));
            //AddDefinition(new Command("togglescavenger", UnsupportedCmd, WaitForMs(25)));
            AddDefinition(new Command("clickscreen (x) (y) ['single'/'double'] ['left'/'right']", ClickScreen, WaitForMs()));
            AddDefinition(new Command("findtype (graphic) [color] [source] [amount] [range or search level]", FindType, WaitForMs()));
            AddDefinition(new Command("findobject (serial) [color] [source] [amount] [range]", FindObject, WaitForMs()));
            AddDefinition(new Command("poplist ('list name') ('element value'/'front'/'back')", PopList, WaitForMs()));
            AddDefinition(new Command("pushlist ('list name') ('element value') ['front'/'back']", PushList, WaitForMs()));
            AddDefinition(new Command("createlist ('list name')", CreateList, WaitForMs()));
            AddDefinition(new Command("removelist ('list name')", RemoveList, WaitForMs()));
            AddDefinition(new Command("msg ('text') [color]", Msg, WaitForMs()));
            AddDefinition(new Command("setalias ('alias name') [serial]", SetAlias, WaitForMs()));
            AddDefinition(new Command("unsetalias ('alias name')", UnsetAlias, WaitForMs()));
            AddDefinition(new Command("promptalias ('alias name')", PromptAlias, WaitForMs()));
            AddDefinition(new Command("findalias ('alias name')", FindAlias, WaitForMs()));
            AddDefinition(new Command("waitforgump (gump id/'any') (timeout)", WaitForGump, WaitForMs()));
            AddDefinition(new Command("waitfortarget (timeout)", WaitForTarget, WaitForMs()));
            AddDefinition(new Command("pause (timeout)", Pause, WaitForMs()));
            AddDefinition(new Command("target (serial)", ClickTarget, WaitForMs()));
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

        private static string SetAbility_LastAbility = "";
        private static bool SetAbility(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - If changed, ability needs to be printed in green                                |
            // +====================================================================================+
            var ability = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory).ToLower();
            var toggle = execution.ArgList.NextAs<string>().ToLower();
            if (toggle == String.Empty || toggle == "on")
            {
                string abilityName;
                GameActions.Print($"SetAbility: Turning {ability} on", hue: 0x104, type: MessageType.System);
                switch (ability)
                {
                    case "primary":
                        abilityName = Enum.GetName(typeof(Ability), World.Player.Abilities[0]);
                        GameActions.UsePrimaryAbility();  
                        break;
                    case "secondary":
                        abilityName = Enum.GetName(typeof(Ability), World.Player.Abilities[1]);
                        GameActions.UseSecondaryAbility();         
                        break;
                    case "stun":
                        GameActions.RequestStun();
                        abilityName = "Stun";
                        break;
                    case "disarm":
                        GameActions.RequestDisarm();
                        abilityName = "Disarm";
                        break;
                    default:
                        throw new ScriptSyntaxError("invalid ability type");
                }

                if(abilityName != SetAbility_LastAbility)
                {
                    SetAbility_LastAbility = abilityName;
                    GameActions.Print($"Ability: {SetAbility_LastAbility}", hue: 0x44 /*green*/, type: MessageType.System);
                }
            }
            else
            {
                GameActions.ClearAbility();
                GameActions.Print($"SetAbility: Turning {ability} on", hue: 0x104, type: MessageType.System);
            }
            return true;
        }

        private static bool Attack(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - No feedback at all if attack does not work                                      |
            // +====================================================================================+
            var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            GameActions.Print($"Attack: attacking {serial}", hue: 0x104, type: MessageType.System);
            GameActions.Attack(serial);
            return true;
        }

        private static uint[] ClearHands_Items = new uint[2] { 0, 0};
        private static bool ClearHands(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - Will ignore command (returning success) if holding/dragging an item             |
            // +====================================================================================+
            Interpreter.Timeout(5000, () => {
                ClearHands_Items = new uint[2] { 0, 0 };
                GameActions.Print($"ClearHands: TIMEOUT", hue: 0x104, type: MessageType.System);
                return true;
            });

            var backpack = World.Player.FindItemByLayer(Layer.Backpack);
            if (ClearHands_Items[0] == 0 && ClearHands_Items[1] == 0) // Try equipping only if not yet equipping
            {
                if (ItemHold.Enabled)
                {
                    GameActions.Print($"You are already holding an item", type: MessageType.Command);
                    GameActions.DropItem(ItemHold.Serial, ItemHold.X, ItemHold.Y, ItemHold.Z, ItemHold.Container);
                }
                else
                {
                    var hand = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory).ToLower();
                    var rightHand = World.Player.FindItemByLayer(Layer.HeldInHand1);
                    var leftHand = World.Player.FindItemByLayer(Layer.HeldInHand2);
                    switch (hand)
                    {
                        case "both":
                            if (rightHand != null)
                            {
                                ClearHands_Items[0] = rightHand.Serial;
                                GameActions.Print($"ClearHands: Picking up {ClearHands_Items[0]}", hue: 0x104, type: MessageType.System);
                                GameActions.PickUp(ClearHands_Items[0], 0, 0, 1);
                            }
                            if (leftHand != null)
                            {
                                ClearHands_Items[1] = leftHand.Serial;
                                GameActions.Print($"ClearHands: Picking up {ClearHands_Items[1]}", hue: 0x104, type: MessageType.System);
                                GameActions.PickUp(ClearHands_Items[1], 0, 0, 1);
                            }
                            break;
                        case "left":

                            if (leftHand != null)
                            {
                                ClearHands_Items[1] = leftHand.Serial;
                                GameActions.Print($"ClearHands: Picking up {ClearHands_Items[1]}", hue: 0x104, type: MessageType.System);
                                GameActions.PickUp(ClearHands_Items[1], 0, 0, 1);
                            }
                            break;
                        case "right":
                            if (rightHand != null)
                            {
                                ClearHands_Items[0] = rightHand.Serial;
                                GameActions.Print($"ClearHands: Picking up {ClearHands_Items[0]}", hue: 0x104, type: MessageType.System);
                                GameActions.PickUp(ClearHands_Items[0], 0, 0, 1);
                            }
                            break;
                        default:
                            throw new ScriptSyntaxError(execution.Cmd.Usage);
                    }
                }
            }
            else
            {
                // Drop items
                if (ClearHands_Items[0] != 0)
                {
                    if (ItemHold.Enabled)
                    {
                        GameActions.Print($"ClearHands: Drop {ClearHands_Items[0]}", hue: 0x104, type: MessageType.System);
                        GameActions.DropItem(ClearHands_Items[0], 0xFFFF, 0xFFFF, 0, backpack);
                    }
                    else if (World.Get(ClearHands_Items[0]) is Item rightItem && rightItem?.Container == backpack)
                        ClearHands_Items[0] = 0;
                }
                if (ClearHands_Items[1] != 0)
                {
                    if (ItemHold.Enabled)
                    {
                        GameActions.Print($"ClearHands: Drop {ClearHands_Items[1]}", hue: 0x104, type: MessageType.System);
                        GameActions.DropItem(ClearHands_Items[1], 0xFFFF, 0xFFFF, 0, backpack);
                    }
                    else if (World.Get(ClearHands_Items[1]) is Item rightItem && rightItem?.Container == backpack)
                        ClearHands_Items[1] = 0;
                } 
            }

            // Return false until items are moved to the backpack
            var cleared = (ClearHands_Items[0] == 0 && ClearHands_Items[1] == 0);
            if(cleared)
                Interpreter.ClearTimeout();
            return cleared;
        }

        private static bool ClickObject(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - Click msg appears on cursor position if object out of range (we cant do it)     |
            // +====================================================================================+
            var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            if (World.Get(serial) == null)
                throw new ScriptSyntaxError("item or mobile not found");
            else
            {
                GameActions.Print($"ClickObject: single click at {serial}", hue: 0x104, type: MessageType.System);
                GameActions.SingleClick(serial);
            }
            return true;
        }

        private static bool BandageSelf(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - Use once requires item to be in the backpack (floor or equipped is ignored)     |
            // +====================================================================================+
            Item bandage = World.Player.FindBandage();
            if (bandage != null)
            {
                GameActions.Print($"BandageSelf: using bandages", hue: 0x104, type: MessageType.System);
                ClassicUO.Network.NetClient.Socket.Send(new PTargetSelectedObject(bandage.Serial, World.Player.Serial));
            }
            else
                throw new ScriptSyntaxError("bandages not found");
            return true;
        }

        private static bool UseType(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - Serial has to be an item, Mobiles are ignored                                   |
            // |  - Use once requires item to be in the backpack (floor or equipped is ignored)     |
            // +====================================================================================+
            Item item = CmdFindItemByGraphic(
                execution.ArgList.NextAs<ushort>(ArgumentList.Expectation.Mandatory),
                execution.ArgList.NextAs<ushort>(),
                execution.ArgList.NextAs<uint>(),
                0,
                execution.ArgList.NextAs<int>()
                );
            if (item != null)
            {
                GameActions.Print($"UseType: double clicking {item.Serial}", hue: 0x104, type: MessageType.System);
                GameActions.DoubleClick(item.Serial);
            }
            else
                throw new ScriptSyntaxError("item type not found");
            return true;
        }

        private static bool UseObject(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - Item can be anywhere (equipped, floor or accessible containers)                 |
            // |  - Use object works even on mobs and matches to a double click                     |
            // +====================================================================================+
            var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            var entity = CmdFindEntityBySerial(serial);
            if (entity != null)
            {
                GameActions.Print($"UseObject: double clicking {entity.Serial}", hue: 0x104, type: MessageType.System);
                GameActions.DoubleClick(entity.Serial);
            }
            else
                throw new ScriptSyntaxError("item type not found");
            return true;
        }

        private static bool UseOnce(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - Serial has to be an item, Mobiles are ignored                                   |
            // |  - Use once requires item to be in the backpack (floor or equipped is ignored)     |
            // +====================================================================================+
            Item item = CmdFindItemByGraphic(
                execution.ArgList.NextAs<ushort>(ArgumentList.Expectation.Mandatory),
                execution.ArgList.NextAs<ushort>(),
                World.Player.FindItemByLayer(Layer.Backpack)
            );

            if (item != null)
            {
                GameActions.Print($"UseOnce: double clicking {item.Serial}", hue: 0x104, type: MessageType.System);
                GameActions.DoubleClick(item.Serial);
            }
            else
                throw new ScriptSyntaxError("item type not found");
            return true;
        }

        private static uint MoveItem_Item = 0;
        private static uint MoveItem_Destination = 0;
        private static bool MoveItem(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - If already holding an item, it will move item in hand to destination            |
            // |  - Ground is ignored as a destination                                              |
            // +====================================================================================+
            Interpreter.Timeout(5000, () => {
                MoveItem_Item = 0;
                MoveItem_Destination = 0;
                GameActions.Print($"MoveItem: TIMEOUT", hue: 0x104, type: MessageType.System);
                return true;
            });
            if (MoveItem_Item == 0)
            {
                var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
                var entity = CmdFindEntityBySerial(serial);
                if (entity == null)
                {
                    throw new ScriptSyntaxError("item not found");
                }

                var destination = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
                if (destination == 0)
                {
                    throw new ScriptSyntaxError("destination not found");
                }
                else if (destination == uint.MaxValue)
                {
                    Interpreter.ClearTimeout();
                    return true;
                }

                if (ItemHold.Enabled)
                {
                    GameActions.DropItem(ItemHold.Serial, ItemHold.X, ItemHold.Y, ItemHold.Z, destination);
                    throw new ScriptSyntaxError("You are already holding an item");
                }
                else
                {            
                    //var x = execution.ArgList.NextAs<int>();
                    //var y = execution.ArgList.NextAs<int>();
                    //var z = execution.ArgList.NextAs<int>();
                    var amount = execution.ArgList.NextAs<int>();

                    MoveItem_Item = serial;
                    MoveItem_Destination = destination;
                    GameActions.Print($"MoveItem: Picking up {serial}", hue: 0x104, type: MessageType.System);
                    GameActions.PickUp(serial, 0, 0, amount); 
                }
            }
            else
            {
                if (ItemHold.Enabled)
                {
                    GameActions.Print($"MoveItem: Drop {MoveItem_Item} in {MoveItem_Destination}", hue: 0x104, type: MessageType.System);
                    GameActions.DropItem(MoveItem_Item, 0xFFFF, 0xFFFF, 0, MoveItem_Destination);
                }
                else if (CmdFindEntityBySerial(MoveItem_Item, source: MoveItem_Destination) != null)
                {
                    MoveItem_Item = 0;
                    MoveItem_Destination = 0;
                }        
            }

            if(MoveItem_Item == 0 && MoveItem_Destination == 0)
            {
                Interpreter.ClearTimeout();
                return true;
            }
            else
                return false;
        }

        private static uint MoveItemOffset_Item = 0;
        private static uint MoveItemOffset_Destination = 0;
        private static bool MoveItemOffset(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - If already holding an item, it will move item in hand to destination            |
            // +====================================================================================+
            Interpreter.Timeout(5000, () => {
                MoveItemOffset_Item = 0;
                MoveItemOffset_Destination = 0;
                GameActions.Print($"MoveItemOffset: TIMEOUT", hue: 0x104, type: MessageType.System);
                return true;
            });
            if (MoveItemOffset_Item == 0)
            {
                var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
                var entity = CmdFindEntityBySerial(serial);
                if (entity == null)
                {
                    throw new ScriptSyntaxError("item not found");
                }

                var destination = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
                if (destination == 0)
                {
                    throw new ScriptSyntaxError("destination not found");
                }

                if (ItemHold.Enabled)
                {
                    GameActions.DropItem(ItemHold.Serial, ItemHold.X, ItemHold.Y, ItemHold.Z, destination);
                    throw new ScriptSyntaxError("You are already holding an item");
                }
                else
                {
                    //var x = execution.ArgList.NextAs<int>();
                    //var y = execution.ArgList.NextAs<int>();
                    //var z = execution.ArgList.NextAs<int>();
                    var amount = execution.ArgList.NextAs<int>();

                    MoveItemOffset_Item = serial;
                    MoveItemOffset_Destination = destination;
                    GameActions.Print($"MoveItemOffset: Picking up {serial}", hue: 0x104, type: MessageType.System);
                    GameActions.PickUp(serial, 0, 0, amount);
                }
            }
            else
            {
                if (ItemHold.Enabled)
                {
                    GameActions.Print($"MoveItemOffset: Drop {MoveItemOffset_Item} in {MoveItemOffset_Destination}", hue: 0x104, type: MessageType.System);
                    if (MoveItemOffset_Destination == uint.MaxValue)
                    {
                        var x = World.Player.X+1; // TODO: Remove +1
                        var y = World.Player.Y;
                        var z = World.Map.GetTileZ(World.Player.X, World.Player.Y);
                        GameActions.DropItem(MoveItemOffset_Item, x, y, z, MoveItemOffset_Destination);
                    }
                    else
                        GameActions.DropItem(MoveItemOffset_Item, 0xFFFF, 0xFFFF, 0, MoveItemOffset_Destination);
                }
                else if (CmdFindEntityBySerial(MoveItemOffset_Item, source: MoveItemOffset_Destination) != null)
                {
                    MoveItemOffset_Item = 0;
                    MoveItemOffset_Destination = 0;
                }
            }

            if (MoveItemOffset_Item == 0 && MoveItemOffset_Destination == 0)
            {
                Interpreter.ClearTimeout();
                return true;
            }
            else
                return false;
        }

        private static uint MoveType_Item = 0;
        private static uint MoveType_Destination = 0;
        private static bool MoveType(CommandExecution execution)
        {
            // += UO Steam =========================================================================+
            // |  - Same behavior as MoveItem                                                       |
            // +====================================================================================+
            Interpreter.Timeout(5000, () => {
                MoveType_Item = 0;
                MoveType_Destination = 0;
                GameActions.Print($"MoveType: TIMEOUT", hue: 0x104, type: MessageType.System);
                return true;
            });
            if (MoveType_Item == 0)
            {
                var graphic = execution.ArgList.NextAs<ushort>(ArgumentList.Expectation.Mandatory);
                var source = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);

                var destination = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
                if (destination == 0)
                {
                    throw new ScriptSyntaxError("destination not found");
                }
                else if (destination == uint.MaxValue)
                {
                    Interpreter.ClearTimeout();
                    return true;
                }

                var x = execution.ArgList.NextAs<int>();
                var y = execution.ArgList.NextAs<int>();
                var z = execution.ArgList.NextAs<int>();
                var color = execution.ArgList.NextAs<ushort>();
                var amount = execution.ArgList.NextAs<int>();
                var range = execution.ArgList.NextAs<int>();

                Item item = CmdFindItemByGraphic(graphic, color, source, amount, range);
                if (item == null)
                {
                    throw new ScriptSyntaxError("item not found");
                }
                
                if (ItemHold.Enabled)
                {
                    GameActions.DropItem(ItemHold.Serial, ItemHold.X, ItemHold.Y, ItemHold.Z, destination);
                    throw new ScriptSyntaxError("You are already holding an item");
                }
                else
                {
                    MoveType_Item = item.Serial;
                    MoveType_Destination = destination;
                    GameActions.Print($"MoveType: Picking up {item.Serial}", hue: 0x104, type: MessageType.System);
                    GameActions.PickUp(item.Serial, 0, 0, amount);
                }
            }
            else
            {
                if (ItemHold.Enabled)
                {
                    GameActions.Print($"MoveType: Drop {MoveType_Item} in {MoveType_Destination}", hue: 0x104, type: MessageType.System);
                    GameActions.DropItem(MoveType_Item, 0xFFFF, 0xFFFF, 0, MoveType_Destination);
                }
                else if (CmdFindEntityBySerial(MoveType_Item, source: MoveType_Destination) != null)
                {
                    MoveType_Item = 0;
                    MoveType_Destination = 0;
                }
            }

            if (MoveType_Item == 0 && MoveType_Destination == 0)
            {
                Interpreter.ClearTimeout();
                return true;
            }
            else
                return false;

            //var graphic = execution.ArgList.NextAs<ushort>(ArgumentList.Expectation.Mandatory);
            //var source = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            //var destination = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            //var x = execution.ArgList.NextAs<int>();
            //var y = execution.ArgList.NextAs<int>();
            //var z = execution.ArgList.NextAs<int>();
            //var color = execution.ArgList.NextAs<ushort>();
            //var amount = execution.ArgList.NextAs<int>();
            //var range = execution.ArgList.NextAs<int>();

            //Item item = CmdFindItemByGraphic(graphic, color, source, amount, range);
            //if (item != null)
            //{
            //    GameActions.PickUp(item.Serial, 0, 0, amount);
            //    GameActions.DropItem(item.Serial, 0xFFFF, 0xFFFF, 0, destination);
            //}
            //return true;
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
                    //Command.Queues[execution.Cmd.Attribute].Enqueue(new CommandExecution(execution.Cmd, newParams, execution.Quiet, execution.Force));
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

        private static uint EquipItem_item = 0;
        private static Layer EquipItem_layer = Layer.Invalid;
        public static bool EquipItem(CommandExecution execution)
        {
            // READY - Delete after review
            // ----------------------------------  UO Steam  --------------------------------------
            //   Never fails.
            //   It returns Usage in white if arguments are invalid
            //   If item already in hand it auto-drops saying: "You are already holding an item"
            //   BLOCK UNTIL ACTION FINISHES
            // ------------------------------------------------------------------------------------
            Interpreter.Timeout(5000, () => {
                EquipItem_item = 0;
                EquipItem_layer = Layer.Invalid;
                GameActions.Print($"EquipItem: TIMEOUT", hue: 0x104, type: MessageType.System);
                return true;
            });
            if (EquipItem_item == 0) // Pick up the item (locking it on cursor)
            {
                if (ItemHold.Enabled)
                {
                    GameActions.Print($"You are already holding an item", type: MessageType.Command);
                    GameActions.DropItem(ItemHold.Serial, ItemHold.X, ItemHold.Y, ItemHold.Z, ItemHold.Container);
                    Interpreter.ClearTimeout();
                    return true;
                }

                var item = World.Get(execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory));
                if (item == null)
                    throw new ScriptRunTimeError(null, "item not found");

                EquipItem_layer = (Layer)execution.ArgList.NextAs<int>();
                if (EquipItem_layer == Layer.Invalid)
                    EquipItem_layer = (Layer)ItemHold.ItemData.Layer;

                EquipItem_item = item.Serial;
                GameActions.PickUp(item, 0, 0, 1);
                GameActions.Print($"EquipItem: Picking up {EquipItem_item}", hue: 0x104, type: MessageType.System);
            }
            else if(EquipItem_layer != Layer.Invalid && ItemHold.Enabled) // Equip
            {
                // Equip item with given layer
                EquipItem_item = ItemHold.Serial;
                GameActions.Equip(EquipItem_layer);
                GameActions.Print($"EquipItem: Equipping up {EquipItem_item}", hue: 0x104, type: MessageType.System);
            }
            else if(World.Player.FindItemByLayer(EquipItem_layer)?.Serial == EquipItem_item)
            {
                // Is item finally equipped?
                GameActions.Print($"EquipItem: Equipped {EquipItem_item}", hue: 0x104, type: MessageType.System);
                EquipItem_item = 0;
                EquipItem_layer = Layer.Invalid;
                Interpreter.ClearTimeout();
                return true;
            }

            return false;
        }

        public static bool ToggleMounted(CommandExecution execution)
        {
            uint serial = 0;

            // If player is mounted we just double ckick ourselves to execute a dismount
            if (World.Player.IsMounted)
                serial = World.Player.Serial;
            else if (!Aliases.Read<uint>("mount", ref serial) || serial == 0) // Otherwise we go after the mount serial
            {
                // UOStream - behavior is prompting for mount if not found and requiring command to eb called again for mount to occur
                VirtualArgument promptArg = new VirtualArgument("mount");
                ArgumentList promptParams = new ArgumentList(new Argument[1] { promptArg }, Definitions["promptalias"].ArgTypes);
                //Command.Queues[execution.Cmd.Attribute].Enqueue((new CommandExecution(Definitions["promptalias"], promptParams, execution.Quiet, execution.Force)));
            }

            GameActions.DoubleClick(serial);
            return true;
        }

        public static bool EquipWand(CommandExecution execution)
        {
            // TODO This should be a system list available via script...
            ushort[] wandGraphicIds = new ushort[] { 0xDF2 };

            // Find all wands based on gaphic
            List<Item> allWands = FindAllByGraphic(wandGraphicIds);
            foreach(Item wand in allWands)
            {
                GameActions.SingleClick(wand.Serial);
                //wand.TextContainer.
                // TODO: As Jaedan mentioned the ItemProperties may not be available in UOO, waiting to sync with him
                // But after processing the propertyes we select one of the wands and call Equip
                //EquipItem(execution);
            }

            return true;
        }

        public static bool Dress(CommandExecution execution)
        {
            // Check if profile is supported
            var profile = execution.ArgList.NextAs<string>();
            if(profile == string.Empty)
                profile = "dressconfig-temporary";
            else if(!Profiles.ContainsKey(profile))
            {
                GameActions.Print($"dress: profile '{profile}' doesn't exist", type: MessageType.System);
                return false;
            }

            // Dress items in profile
            foreach(var serial in Profiles[profile])
            {
                var item = World.GetOrCreateItem(serial);
                if (item.Layer != item.StaticLayer && (Layer)item.ItemData.Layer != Layer.Invalid) // not equipped and valid
                {
                    // UOStream - behavior is queing a request to move each item
                    ArgumentList promptParams = new ArgumentList(new Argument[1] { new VirtualArgument($"0x{serial.ToString("X")}") }, Definitions["equipitem"].ArgTypes);
                    //Command.Queues[Definitions["equipitem"].Attribute].Enqueue((new CommandExecution(Definitions["equipitem"], promptParams, execution.Quiet, execution.Force)));

                    //ArgumentList promptParams = new ArgumentList(new Argument[2] { new VirtualArgument($"0x{serial.ToString("X")}"), new VirtualArgument($"0x{destintion.Serial.ToString("X")}") }, Definitions["equipitem"].ArgTypes);
                    //Command.Queues[Definitions["moveitem"].Attribute].Enqueue((new CommandExecution(Definitions["EquipItem"], promptParams, execution.Quiet, execution.Force)));
                }
            }
            return true;
        }

        public static bool Undress(CommandExecution execution)
        {
            var profile = execution.ArgList.NextAs<string>();
            if (profile == string.Empty)
            {
                // UO Steam: Should undress all when no profile is provided
                // So let's create a temporaty profile with current equiped items
                profile = "temporary-generic";
                Profiles[profile].Clear();
                for (LinkedObject i = World.Player.Items; i != null; i = i.Next)
                {
                    Item it = (Item)i;
                    // add it to the list if we found it
                    if (it.AllowedToDraw && (it.ItemData.IsWearable || it.ItemData.IsWeapon) && it.Layer != Layer.Invalid && it.Layer != Layer.Backpack && it.Layer != Layer.Bank && it.Layer != Layer.Beard && it.Layer != Layer.Face &&
                        it.Layer != Layer.Hair && it.Layer != Layer.Mount && it.Layer != Layer.ShopBuy && it.Layer != Layer.ShopBuyRestock && it.Layer != Layer.ShopSell)
                    {
                        Profiles[profile].Add(it.Serial);
                    }
                }  
            }
            else if(!Profiles.ContainsKey(profile))
            {
                // Check if provided profile is valid
                GameActions.Print($"dress: profile '{profile}' doesn't exist", type: MessageType.System);
                return false;
            }

            // Undress items in profile
            var backpack = World.Player.FindItemByLayer(Layer.Backpack);
            foreach (var serial in Profiles[profile])
            {
                // UOStream - behavior is queing a request to move each item
                ArgumentList promptParams = new ArgumentList(new Argument[2] { new VirtualArgument($"0x{serial.ToString("X")}"), new VirtualArgument("backpack") }, Definitions["moveitem"].ArgTypes);
                //Command.Queues[Definitions["moveitem"].Attribute].Enqueue((new CommandExecution(Definitions["moveitem"], promptParams, execution.Quiet, execution.Force)));
            }
            return true;
        }

        public static bool Dressconfig(CommandExecution execution)
        {
            // Set current equipped items in temporary dressing profile
            var profile = "dressconfig-temporary";
            Profiles[profile].Clear();
            for (LinkedObject i = World.Player.Items; i != null; i = i.Next)
            {
                Item it = (Item)i;
                // add it to the list if we found it
                if (it.AllowedToDraw && (it.ItemData.IsWearable || it.ItemData.IsWeapon) && it.Layer != Layer.Invalid && it.Layer != Layer.Backpack && it.Layer != Layer.Bank && it.Layer != Layer.Beard && it.Layer != Layer.Face &&
                    it.Layer != Layer.Hair && it.Layer != Layer.Mount && it.Layer != Layer.ShopBuy && it.Layer != Layer.ShopBuyRestock && it.Layer != Layer.ShopSell)
                {
                    Profiles[profile].Add(it.Serial);
                }
            }
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
                while (TargetManager.IsTargeting && DateTime.UtcNow - targetingStarted < TimeSpan.FromSeconds(50))
                {
                    Thread.Sleep(25);
                }
                Aliases.Write<uint>(
                    alias,
                    TargetManager.LastTargetInfo.Serial
                    );
                Interpreter.Unpause();
            });        
            return true;
        }

        private static bool FindAlias(CommandExecution execution)
        {
            var alias = execution.ArgList.NextAs<string>(ArgumentList.Expectation.Mandatory);
            uint value = 0;
            return Aliases.Read<uint>(alias, ref value);
        }

        private static bool WaitForGump(CommandExecution execution)
        {
            var gumpid = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory); // GumpID is also a serial
            var timeout = execution.ArgList.NextAs<int>(ArgumentList.Expectation.Mandatory); // Timeout in ms

            Interpreter.Pause(60000);
            Task.Run(() =>
            {
                // Take a snapshot of all existing gumps
                var gumpsSnapshot = UIManager.Gumps.ToArray();

                DateTime startTime = DateTime.UtcNow;
                while (DateTime.UtcNow - startTime < TimeSpan.FromMilliseconds(timeout))
                {
                    if (gumpid == 0 /*Zero value is Any*/) // any new gump was added
                    {
                        // For each existing gump
                        for (LinkedListNode<Gump> first = UIManager.Gumps.First; first != null; first = first.Next)
                        {
                            // If its new (does not exist in snapshot)
                            if(!gumpsSnapshot.Contains(first.Value))
                            {
                                // Stop waiting if its in an interactable state
                                if (first.Value.IsModal || first.Value.IsVisible || first.Value.IsEnabled)
                                {
                                    Interpreter.Unpause();
                                    return;
                                }
                            }
                        }
                    }
                    else
                    {
                        var specificGump = UIManager.GetGump(gumpid);
                        // Requested gump exists and is visible
                        if (specificGump != null && (specificGump.IsModal && !specificGump.IsModal || !specificGump.IsVisible || !specificGump.IsEnabled))
                        {
                            Interpreter.Unpause();
                            return;
                        }
                        return;  
                    }

                    Thread.Sleep(25); // keep iteration
                }
                Interpreter.Unpause();
            });
            return true;
        }

        private static bool ClickScreen(CommandExecution execution)
        {
            // Read command arguments
            var mouseX = execution.ArgList.NextAs<int>(ArgumentList.Expectation.Mandatory);
            var mouseY = execution.ArgList.NextAs<int>(ArgumentList.Expectation.Mandatory);
            Mouse.Position.X = mouseX;
            Mouse.Position.Y = mouseY;
            var clickType = execution.ArgList.NextAs<string>();
            MouseButtonType buttonType = MouseButtonType.Left;
            if (execution.ArgList.NextAs<string>() == "right")
                buttonType = MouseButtonType.Right;

            // STOP - can we use DelayedObjectClickManager.Set(ent.Serial, Mouse.Position.X, Mouse.Position.Y, Time.Ticks + Mouse.MOUSE_DELAY_DOUBLE_CLICK); ????????????

            // Get scene and inject mouse down logic
            GameScene gs = Client.Game.GetScene<GameScene>();
            if (clickType == string.Empty || clickType == "single")
            {
                UIManager.OnMouseButtonDown(buttonType);
                gs.OnMouseDown(buttonType);
            }
            else
            {
                UIManager.OnMouseDoubleClick(buttonType);
                gs.OnMouseDoubleClick(buttonType);
            }

            // Because mouse  logic is nested with draw logic, we need to force for the game to darw
            Interpreter.Pause(60000);
            Task.Run(() =>
             {
                 Mouse.Position.X = mouseX;
                 Mouse.Position.Y = mouseY;
                 Mouse.ButtonRelease(buttonType);
                 UIManager.OnMouseButtonUp(buttonType);
                 gs.OnMouseUp(buttonType);
                 Mouse.Update(); // reset mouse with real inout
                 Thread.Sleep(100); // keep iteration (next frame?)
                 Interpreter.Unpause();
             });
            return true;
        }

        private static bool WaitForTarget(CommandExecution execution)
        {
            Interpreter.Timeout(execution.ArgList.NextAs<int>(ArgumentList.Expectation.Mandatory), () => { return true; });
            if (TargetManager.IsTargeting)
            {
                Interpreter.ClearTimeout();
                return true;
            }
            else return false;
            //return !TargetManager.IsTargeting;

            //var timeout = execution.ArgList.NextAs<int>(ArgumentList.Expectation.Mandatory);

            //Interpreter.Pause(timeout);
            //Task.Run(() =>
            //{
            //    // Take a snapshot of all existing gumps
            //    var gumpsSnapshot = UIManager.Gumps.ToArray();

            //    DateTime startTime = DateTime.UtcNow;
            //    while (DateTime.UtcNow - startTime < TimeSpan.FromMilliseconds(timeout))
            //    {
            //        if (TargetManager.IsTargeting)
            //            break;
            //        else
            //            Thread.Sleep(25); // keep iteration
            //    }
            //    Interpreter.Unpause();
            //});

            //return true;
        }

        private static int Pause_Time = 0;
        private static bool Pause(CommandExecution execution)
        {
            // READY - Delete after review
            // ----------------------------------  UO Steam  --------------------------------------
            //   Never fails.
            // ------------------------------------------------------------------------------------
            if (Pause_Time == 0)
            {
                Pause_Time = execution.ArgList.NextAs<int>(ArgumentList.Expectation.Mandatory);
                Interpreter.Timeout(Pause_Time, () =>
                {
                    GameActions.Print($"Pause: END", hue: 0x104, type: MessageType.System);
                    Pause_Time = 0;
                    return true;
                });
                GameActions.Print($"Pause: START", hue: 0x104, type: MessageType.System);
            }
            return false;
        }

        private static bool ClickTarget(CommandExecution execution)
        {
            var serial = execution.ArgList.NextAs<uint>(ArgumentList.Expectation.Mandatory);
            TargetManager.Target(serial);
            return true;
        }

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

        private static bool UnsupportedCmd(CommandExecution execution)
        {
            GameActions.Print($"Command '{execution.Cmd.Keyword}' is not supported in UO Outlands", type: MessageType.System);
            return false;
        }

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
                if (item == null) // For Any, also look at the ground if not found in player belongings
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
        public static Command.Handler WaitForMs(int waitTime = 300)
        {
            return (execution) => { return (Time.Ticks - execution.LastChecked > waitTime); };
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

            return (Time.Ticks - execution.LastChecked > movementDelay);
        }

        private static List<Item> FindAllByGraphic(ushort[] graphicIds, ushort color = 0xFFFF, LinkedObject link = null)
        {
            // If no link is provided, use Player items (ecursion starting points)
            if(link == null)
                link = World.Player.Items;

            // Traverse all items in the link
            List<Item> result = new List<Item>();            
            foreach (ushort graphic in graphicIds)
            {
                for (LinkedObject i = link; i != null; i = i.Next)
                {
                    Item it = (Item)i;
                    // add it to the list if we found it
                    if (it.Graphic == graphic && it.Hue < color)
                    {
                        result.Add(it);
                    }
                    // And if a container keep recursion inside
                    if (SerialHelper.IsValid(it.Container) && !it.IsEmpty)
                    {
                        result.AddRange(FindAllByGraphic(graphicIds, color, it.Items));
                    }
                }
            }

            return result;
        }
    }
}
